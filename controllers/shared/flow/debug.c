#include "flow/debug.h"

#include "flow/sha256.h"

#include <stdio.h>
#include <string.h>

enum
{
    SNAPSHOT_SCHEMA          = 1,
    SNAPSHOT_MODE_MANUAL     = 1,
    SNAPSHOT_STATE_EVALUATED = 1,
    SNAPSHOT_VALUE_DIGITAL   = 2,
};

/* Tests whether one artifact byte was already supplied by a prior chunk. */
static bool is_covered(const flow_debug_t *debug, size_t offset)
{
    return (debug->coverage[offset / 8U] & (uint8_t)(1U << (offset % 8U))) != 0U;
}

/* Records first-time byte coverage so prepare can reject partial artifacts. */
static void set_covered(flow_debug_t *debug, size_t offset)
{
    debug->coverage[offset / 8U] |= (uint8_t)(1U << (offset % 8U));
    debug->covered_bytes++;
}

/* Writes one little-endian unsigned 16-bit field into bounded snapshot storage. */
static bool append_u16(flow_debug_t *debug, size_t *offset, uint16_t value)
{
    if (*offset + sizeof(value) > sizeof(debug->snapshot))
    {
        return false;
    }
    debug->snapshot[(*offset)++] = (uint8_t)value;
    debug->snapshot[(*offset)++] = (uint8_t)(value >> 8U);
    return true;
}

/* Writes one little-endian unsigned 32-bit field into bounded snapshot storage. */
static bool append_u32(flow_debug_t *debug, size_t *offset, uint32_t value)
{
    if (*offset + sizeof(value) > sizeof(debug->snapshot))
    {
        return false;
    }
    for (size_t index = 0; index < sizeof(value); index++)
    {
        debug->snapshot[(*offset)++] = (uint8_t)(value >> (index * 8U));
    }
    return true;
}

/* Writes one little-endian unsigned 64-bit field into bounded snapshot storage. */
static bool append_u64(flow_debug_t *debug, size_t *offset, uint64_t value)
{
    if (*offset + sizeof(value) > sizeof(debug->snapshot))
    {
        return false;
    }
    for (size_t index = 0; index < sizeof(value); index++)
    {
        debug->snapshot[(*offset)++] = (uint8_t)(value >> (index * 8U));
    }
    return true;
}

/* Writes one byte into bounded snapshot storage. */
static bool append_u8(flow_debug_t *debug, size_t *offset, uint8_t value)
{
    if (*offset >= sizeof(debug->snapshot))
    {
        return false;
    }
    debug->snapshot[(*offset)++] = value;
    return true;
}

/* Writes one contract-bounded string8 into snapshot storage. */
static bool append_string(flow_debug_t *debug, size_t *offset, const char *value)
{
    size_t size = 0;
    while (size <= FLOW_EXECUTABLE_MAX_ID_BYTES && value[size] != '\0')
    {
        size++;
    }
    if (size == 0 || size > FLOW_EXECUTABLE_MAX_ID_BYTES || *offset + 1U + size > sizeof(debug->snapshot))
    {
        return false;
    }
    debug->snapshot[(*offset)++] = (uint8_t)size;
    (void)memcpy(&debug->snapshot[*offset], value, size);
    *offset += size;
    return true;
}

/* Securely clears all session-owned bytes while preserving adapters and session monotonicity. */
static void clear_session(flow_debug_t *debug)
{
    const flow_target_t *target            = debug->target;
    const flow_debug_get_input_t get_input = debug->get_input;
    void *input_context                    = debug->input_context;
    const uint64_t next_session_id         = debug->next_session_id;
    (void)memset(debug, 0, sizeof(*debug));
    debug->target          = target;
    debug->get_input       = get_input;
    debug->input_context   = input_context;
    debug->next_session_id = next_session_id;
    debug->state           = FLOW_DEBUG_EMPTY;
}

/* Tests session identity and ownership without revealing another peer's session. */
static flow_debug_result_t get_access(const flow_debug_t *debug, uint32_t owner_id, uint64_t session_id)
{
    if (debug->state == FLOW_DEBUG_EMPTY || debug->session_id != session_id || session_id == 0U)
    {
        return FLOW_DEBUG_NOT_FOUND;
    }
    return debug->owner_id == owner_id ? FLOW_DEBUG_OK : FLOW_DEBUG_FORBIDDEN;
}

/* Renews an authenticated owner's lease from the current monotonic time. */
static void renew_lease(flow_debug_t *debug, uint64_t now_ms)
{
    debug->lease_deadline_ms = now_ms > UINT64_MAX - FLOW_DEBUG_LEASE_MS ? UINT64_MAX : now_ms + FLOW_DEBUG_LEASE_MS;
}

/* Encodes the latest complete runtime snapshot into one immutable byte stream. */
static bool encode_snapshot(flow_debug_t *debug, uint64_t completed_at_ms)
{
    const flow_tick_snapshot_t *snapshot = get_flow_runtime_snapshot(&debug->runtime);
    if (snapshot == NULL)
    {
        return false;
    }
    size_t offset = 0;
    const bool header_ok =
        append_u16(debug, &offset, SNAPSHOT_SCHEMA) && append_u64(debug, &offset, debug->session_id) &&
        append_string(debug, &offset, debug->executable.flow_id) && append_u32(debug, &offset, debug->executable.revision) &&
        append_u8(debug, &offset, (uint8_t)FLOW_DEBUG_PAUSED) && append_u8(debug, &offset, SNAPSHOT_MODE_MANUAL) &&
        append_u64(debug, &offset, snapshot->tick_number) && append_u64(debug, &offset, snapshot->sampled_at_ms) &&
        append_u64(debug, &offset, completed_at_ms) && append_u32(debug, &offset, 0) &&
        append_u8(debug, &offset, snapshot->input_validity) && append_u16(debug, &offset, snapshot->node_count) &&
        append_u16(debug, &offset, snapshot->output_count) && append_u32(debug, &offset, 0) &&
        append_u32(debug, &offset, snapshot->evaluation_failure_count) &&
        append_u16(debug, &offset, (uint16_t)snapshot->last_result.code) && append_u8(debug, &offset, 0);
    if (!header_ok)
    {
        return false;
    }
    for (uint16_t index = 0; index < snapshot->node_count; index++)
    {
        const flow_node_snapshot_t *node = &snapshot->nodes[index];
        if (!append_string(debug, &offset, node->node_id) || !append_u8(debug, &offset, SNAPSHOT_STATE_EVALUATED) ||
            !append_u8(debug, &offset, (uint8_t)node->quality) || !append_u8(debug, &offset, SNAPSHOT_VALUE_DIGITAL) ||
            !append_u8(debug, &offset, 1) || !append_u8(debug, &offset, node->value ? 1U : 0U))
        {
            return false;
        }
    }
    for (uint16_t index = 0; index < snapshot->output_count; index++)
    {
        const flow_output_snapshot_t *output = &snapshot->outputs[index];
        if (!append_string(debug, &offset, output->point_id) || !append_u8(debug, &offset, SNAPSHOT_STATE_EVALUATED) ||
            !append_u8(debug, &offset, (uint8_t)output->quality) || !append_u8(debug, &offset, output->value ? 1U : 0U))
        {
            return false;
        }
    }
    debug->snapshot_length = (uint32_t)offset;
    flow_sha256(debug->snapshot, offset, debug->snapshot_digest);
    return true;
}

/* Initializes an empty volatile debug owner with immutable target and input adapters. */
bool flow_debug_init(flow_debug_t *debug, const flow_target_t *target, flow_debug_get_input_t get_input, void *input_context)
{
    if (debug == NULL || target == NULL || get_input == NULL)
    {
        return false;
    }
    (void)memset(debug, 0, sizeof(*debug));
    debug->target          = target;
    debug->get_input       = get_input;
    debug->input_context   = input_context;
    debug->next_session_id = 1;
    return true;
}

/* Starts a bounded load, optionally replacing an existing session owned by any authenticated peer. */
flow_debug_result_t flow_debug_begin(flow_debug_t *debug, uint32_t owner_id, bool replace_existing, uint32_t artifact_length,
                                     const uint8_t digest[FLOW_DEBUG_DIGEST_BYTES], uint64_t now_ms, uint64_t *session_id)
{
    if (debug == NULL || digest == NULL || session_id == NULL || owner_id == 0U || artifact_length == 0U ||
        artifact_length > FLOW_EXECUTABLE_MAX_ARTIFACT_BYTES)
    {
        return FLOW_DEBUG_INVALID_ARGUMENT;
    }
    flow_debug_process(debug, now_ms);
    if (debug->state != FLOW_DEBUG_EMPTY && !replace_existing)
    {
        return FLOW_DEBUG_CONFLICT;
    }
    if (debug->state != FLOW_DEBUG_EMPTY)
    {
        clear_session(debug);
    }
    debug->session_id = debug->next_session_id++;
    if (debug->session_id == 0U)
    {
        debug->session_id = debug->next_session_id++;
    }
    debug->owner_id        = owner_id;
    debug->artifact_length = artifact_length;
    (void)memcpy(debug->artifact_digest, digest, FLOW_DEBUG_DIGEST_BYTES);
    debug->state = FLOW_DEBUG_LOADING;
    renew_lease(debug, now_ms);
    *session_id = debug->session_id;
    return FLOW_DEBUG_OK;
}

/* Writes an idempotent artifact chunk and renews the owning session lease. */
flow_debug_result_t flow_debug_write(flow_debug_t *debug, uint32_t owner_id, uint64_t session_id, uint32_t offset,
                                     const uint8_t *data, size_t size, uint64_t now_ms)
{
    if (debug == NULL || data == NULL || size == 0U || size > FLOW_DEBUG_CHUNK_LIMIT)
    {
        return FLOW_DEBUG_INVALID_ARGUMENT;
    }
    flow_debug_process(debug, now_ms);
    const flow_debug_result_t access = get_access(debug, owner_id, session_id);
    if (access != FLOW_DEBUG_OK)
    {
        return access;
    }
    if (debug->state != FLOW_DEBUG_LOADING)
    {
        return FLOW_DEBUG_WRONG_STATE;
    }
    if (offset > debug->artifact_length || size > debug->artifact_length - offset)
    {
        return FLOW_DEBUG_INVALID_ARGUMENT;
    }
    for (size_t index = 0; index < size; index++)
    {
        if (is_covered(debug, offset + index) && debug->artifact[offset + index] != data[index])
        {
            return FLOW_DEBUG_INVALID_ARGUMENT;
        }
    }
    for (size_t index = 0; index < size; index++)
    {
        if (!is_covered(debug, offset + index))
        {
            debug->artifact[offset + index] = data[index];
            set_covered(debug, offset + index);
        }
    }
    renew_lease(debug, now_ms);
    return FLOW_DEBUG_OK;
}

/* Validates and prepares one fully covered artifact without touching durable flow state. */
flow_debug_result_t flow_debug_prepare(flow_debug_t *debug, uint32_t owner_id, uint64_t session_id, uint64_t now_ms)
{
    if (debug == NULL)
    {
        return FLOW_DEBUG_INVALID_ARGUMENT;
    }
    flow_debug_process(debug, now_ms);
    const flow_debug_result_t access = get_access(debug, owner_id, session_id);
    if (access != FLOW_DEBUG_OK)
    {
        return access;
    }
    if (debug->state != FLOW_DEBUG_LOADING || debug->covered_bytes != debug->artifact_length)
    {
        return FLOW_DEBUG_WRONG_STATE;
    }
    uint8_t digest[FLOW_DEBUG_DIGEST_BYTES];
    flow_sha256(debug->artifact, debug->artifact_length, digest);
    if (memcmp(digest, debug->artifact_digest, sizeof(digest)) != 0)
    {
        debug->state = FLOW_DEBUG_FAULT;
        return FLOW_DEBUG_DIGEST_MISMATCH;
    }
    debug->last_result = flow_executable_prepare(debug->artifact, debug->artifact_length, debug->target, &debug->executable);
    if (debug->last_result.code != FLOW_REASON_OK || !flow_runtime_init(&debug->runtime, &debug->executable))
    {
        debug->state = FLOW_DEBUG_FAULT;
        return FLOW_DEBUG_VALIDATION_FAILED;
    }
    debug->state = FLOW_DEBUG_READY;
    renew_lease(debug, now_ms);
    return FLOW_DEBUG_OK;
}

/* Samples physical inputs and atomically evaluates one complete shadow tick. */
flow_debug_result_t flow_debug_step(flow_debug_t *debug, uint32_t owner_id, uint64_t session_id, uint64_t now_ms)
{
    if (debug == NULL)
    {
        return FLOW_DEBUG_INVALID_ARGUMENT;
    }
    flow_debug_process(debug, now_ms);
    const flow_debug_result_t access = get_access(debug, owner_id, session_id);
    if (access != FLOW_DEBUG_OK)
    {
        return access;
    }
    if (debug->state != FLOW_DEBUG_READY && debug->state != FLOW_DEBUG_PAUSED)
    {
        return FLOW_DEBUG_WRONG_STATE;
    }
    debug->state             = FLOW_DEBUG_STEPPING;
    flow_input_frame_t input = {0};
    if (!debug->get_input(debug->input_context, &input))
    {
        debug->last_result = (flow_result_t){.code = FLOW_REASON_INPUT_QUALITY_REJECTED};
        debug->state       = FLOW_DEBUG_FAULT;
        return FLOW_DEBUG_VALIDATION_FAILED;
    }
    debug->last_result = flow_runtime_step(&debug->runtime, &input);
    if (debug->last_result.code != FLOW_REASON_OK || !encode_snapshot(debug, now_ms))
    {
        debug->state = FLOW_DEBUG_FAULT;
        return FLOW_DEBUG_VALIDATION_FAILED;
    }
    debug->state = FLOW_DEBUG_PAUSED;
    renew_lease(debug, now_ms);
    return FLOW_DEBUG_OK;
}

/* Gets status for the owning session and renews its fixed lease. */
flow_debug_result_t flow_debug_get_status(flow_debug_t *debug, uint32_t owner_id, uint64_t session_id, uint64_t now_ms,
                                          flow_debug_status_t *status)
{
    if (debug == NULL || status == NULL)
    {
        return FLOW_DEBUG_INVALID_ARGUMENT;
    }
    flow_debug_process(debug, now_ms);
    const flow_debug_result_t access = get_access(debug, owner_id, session_id);
    if (access != FLOW_DEBUG_OK)
    {
        return access;
    }
    renew_lease(debug, now_ms);
    *status = (flow_debug_status_t){.session_id         = debug->session_id,
                                    .state              = debug->state,
                                    .covered_bytes      = debug->covered_bytes,
                                    .artifact_length    = debug->artifact_length,
                                    .flow_revision      = debug->executable.revision,
                                    .tick_number        = debug->runtime.tick_number,
                                    .lease_remaining_ms = FLOW_DEBUG_LEASE_MS,
                                    .last_result        = debug->last_result};
    return FLOW_DEBUG_OK;
}

/* Gets metadata for the latest immutable snapshot for one exact tick. */
flow_debug_result_t flow_debug_get_snapshot_header(flow_debug_t *debug, uint32_t owner_id, uint64_t session_id,
                                                   uint64_t tick_number, uint64_t now_ms, flow_debug_snapshot_header_t *header)
{
    if (debug == NULL || header == NULL)
    {
        return FLOW_DEBUG_INVALID_ARGUMENT;
    }
    flow_debug_process(debug, now_ms);
    const flow_debug_result_t access = get_access(debug, owner_id, session_id);
    if (access != FLOW_DEBUG_OK)
    {
        return access;
    }
    if (debug->snapshot_length == 0U || debug->runtime.tick_number != tick_number)
    {
        return FLOW_DEBUG_NOT_FOUND;
    }
    renew_lease(debug, now_ms);
    *header =
        (flow_debug_snapshot_header_t){.session_id   = session_id,
                                       .tick_number  = tick_number,
                                       .total_length = debug->snapshot_length,
                                       .chunk_count = (uint16_t)((debug->snapshot_length + FLOW_DEBUG_SNAPSHOT_CHUNK_LIMIT - 1U) /
                                                                 FLOW_DEBUG_SNAPSHOT_CHUNK_LIMIT),
                                       .chunk_data_limit = FLOW_DEBUG_SNAPSHOT_CHUNK_LIMIT};
    (void)memcpy(header->digest, debug->snapshot_digest, sizeof(header->digest));
    return FLOW_DEBUG_OK;
}

/* Copies one indexed immutable snapshot chunk and returns its absolute offset and size. */
flow_debug_result_t flow_debug_read_snapshot_chunk(flow_debug_t *debug, uint32_t owner_id, uint64_t session_id,
                                                   uint64_t tick_number, uint16_t chunk_index, uint64_t now_ms, uint8_t *output,
                                                   size_t capacity, uint32_t *absolute_offset, size_t *size)
{
    flow_debug_snapshot_header_t header;
    const flow_debug_result_t result = flow_debug_get_snapshot_header(debug, owner_id, session_id, tick_number, now_ms, &header);
    if (result != FLOW_DEBUG_OK)
    {
        return result;
    }
    if (output == NULL || absolute_offset == NULL || size == NULL || chunk_index >= header.chunk_count)
    {
        return FLOW_DEBUG_INVALID_ARGUMENT;
    }
    const uint32_t offset   = (uint32_t)chunk_index * FLOW_DEBUG_SNAPSHOT_CHUNK_LIMIT;
    const size_t remaining  = debug->snapshot_length - offset;
    const size_t chunk_size = remaining < FLOW_DEBUG_SNAPSHOT_CHUNK_LIMIT ? remaining : FLOW_DEBUG_SNAPSHOT_CHUNK_LIMIT;
    if (capacity < chunk_size)
    {
        return FLOW_DEBUG_INVALID_ARGUMENT;
    }
    (void)memcpy(output, &debug->snapshot[offset], chunk_size);
    *absolute_offset = offset;
    *size            = chunk_size;
    return FLOW_DEBUG_OK;
}

/* Renews the fixed lease for an owned non-empty session. */
flow_debug_result_t flow_debug_renew(flow_debug_t *debug, uint32_t owner_id, uint64_t session_id, uint64_t now_ms)
{
    if (debug == NULL)
    {
        return FLOW_DEBUG_INVALID_ARGUMENT;
    }
    flow_debug_process(debug, now_ms);
    const flow_debug_result_t access = get_access(debug, owner_id, session_id);
    if (access == FLOW_DEBUG_OK)
    {
        renew_lease(debug, now_ms);
    }
    return access;
}

/* Stops and securely clears one owned volatile session. */
flow_debug_result_t flow_debug_stop(flow_debug_t *debug, uint32_t owner_id, uint64_t session_id)
{
    if (debug == NULL)
    {
        return FLOW_DEBUG_INVALID_ARGUMENT;
    }
    const flow_debug_result_t access = get_access(debug, owner_id, session_id);
    if (access != FLOW_DEBUG_OK)
    {
        return access;
    }
    debug->state = FLOW_DEBUG_STOPPED;
    clear_session(debug);
    return FLOW_DEBUG_OK;
}

/* Expires and clears a session whose authenticated lease deadline has elapsed. */
void flow_debug_process(flow_debug_t *debug, uint64_t now_ms)
{
    if (debug != NULL && debug->state != FLOW_DEBUG_EMPTY && now_ms >= debug->lease_deadline_ms)
    {
        debug->state = FLOW_DEBUG_STOPPED;
        clear_session(debug);
    }
}
