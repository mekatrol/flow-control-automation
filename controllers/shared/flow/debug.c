#include "flow/debug.h"

#include "flow/sha256.h"

#include <stdio.h>
#include <string.h>

/* Tests session identity and ownership without revealing another peer's session. */
static flow_debug_result_t get_access(const flow_debug_t *debug, uint32_t owner_id, uint64_t session_id);

/* Renews an authenticated owner's lease from the current monotonic time. */
static void renew_lease(flow_debug_t *debug, uint64_t now_ms);

/* Relinquishes every command owned by this volatile session before any unsafe lifecycle transition. */
static void relinquish_live_outputs(flow_debug_t *debug)
{
    if (!debug->is_live_output_enabled || debug->relinquish_output == NULL)
    {
        return;
    }

    for (uint16_t index = 0; index < debug->executable.node_count; index++)
    {
        const flow_node_t *node = &debug->executable.nodes[index];

        if (node->kind == FLOW_NODE_PROPOSED_OUTPUT)
        {
            debug->relinquish_output(debug->output_context, debug->executable.points[node->point_index].id);
        }
    }
}

/* Applies one complete tick's proposed outputs through bounded, expiring arbitration commands. */
static bool apply_live_outputs(flow_debug_t *debug, uint64_t now_ms)
{
    if (!debug->is_live_output_enabled)
    {
        return true;
    }
    const flow_tick_snapshot_t *snapshot = get_flow_runtime_snapshot(&debug->runtime);
    const uint64_t expires_at_ms =
        now_ms > UINT64_MAX - FLOW_DEBUG_LIVE_OUTPUT_HOLD_MS ? UINT64_MAX : now_ms + FLOW_DEBUG_LIVE_OUTPUT_HOLD_MS;

    for (uint16_t index = 0; snapshot != NULL && index < snapshot->output_count; index++)
    {
        bool is_effective = false;

        if (!debug->command_output(debug->output_context, snapshot->outputs[index].point_id, snapshot->outputs[index].value,
                                   FLOW_DEBUG_LIVE_OUTPUT_PRIORITY, expires_at_ms, &is_effective))
        {
            relinquish_live_outputs(debug);
            return false;
        }

        if (!is_effective && debug->arbitration_loss_count < UINT32_MAX)
        {
            debug->arbitration_loss_count++;
        }
    }
    return snapshot != NULL;
}

enum
{
    SNAPSHOT_SCHEMA              = 3,
    SNAPSHOT_MODE_MANUAL         = 1,
    SNAPSHOT_MODE_FIXED          = 2,
    SNAPSHOT_STATE_EVALUATED     = 1,
    SNAPSHOT_VALUE_DIGITAL       = 2,
    SNAPSHOT_PUBLISH_INTERVAL_MS = 500,
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
    memcpy(&debug->snapshot[*offset], value, size);
    *offset += size;
    return true;
}

/* Securely clears all session-owned bytes while preserving adapters and session monotonicity. */
static void clear_session(flow_debug_t *debug)
{
    relinquish_live_outputs(debug);
    const flow_target_t *target                            = debug->target;
    const flow_debug_get_input_t get_input                 = debug->get_input;
    void *input_context                                    = debug->input_context;
    const flow_debug_get_time_us_t get_time_us             = debug->get_time_us;
    void *time_context                                     = debug->time_context;
    const flow_debug_command_output_t command_output       = debug->command_output;
    const flow_debug_relinquish_output_t relinquish_output = debug->relinquish_output;
    void *output_context                                   = debug->output_context;
    const uint64_t next_session_id                         = debug->next_session_id;
    memset(debug, 0, sizeof(*debug));
    debug->target            = target;
    debug->get_input         = get_input;
    debug->input_context     = input_context;
    debug->get_time_us       = get_time_us;
    debug->time_context      = time_context;
    debug->command_output    = command_output;
    debug->relinquish_output = relinquish_output;
    debug->output_context    = output_context;
    debug->next_session_id   = next_session_id;
    debug->state             = FLOW_DEBUG_EMPTY;
}

/* Installs the optional physical-output arbitration adapter; live mode remains disabled until explicitly enabled. */
void flow_debug_set_output_adapter(flow_debug_t *debug, flow_debug_command_output_t command_output,
                                   flow_debug_relinquish_output_t relinquish_output, void *output_context)
{
    if (debug != NULL && debug->state == FLOW_DEBUG_EMPTY)
    {
        debug->command_output    = command_output;
        debug->relinquish_output = relinquish_output;
        debug->output_context    = output_context;
    }
}

/* Enables live output only when the authenticated caller confirms every affected point in canonical node order. */
flow_debug_result_t flow_debug_enable_live_output(flow_debug_t *debug, uint32_t owner_id, uint64_t session_id,
                                                  const char *const *confirmed_point_ids, size_t point_count, uint64_t now_ms)
{
    if (debug == NULL || confirmed_point_ids == NULL || debug->command_output == NULL || debug->relinquish_output == NULL)
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
    size_t confirmed_index = 0;

    for (uint16_t index = 0; index < debug->executable.node_count; index++)
    {
        const flow_node_t *node = &debug->executable.nodes[index];

        if (node->kind == FLOW_NODE_PROPOSED_OUTPUT)
        {
            if (confirmed_index >= point_count || confirmed_point_ids[confirmed_index] == NULL ||
                strcmp(debug->executable.points[node->point_index].id, confirmed_point_ids[confirmed_index]) != 0)
            {
                return FLOW_DEBUG_FORBIDDEN;
            }
            confirmed_index++;
        }
    }

    if (confirmed_index == 0U || confirmed_index != point_count)
    {
        return FLOW_DEBUG_FORBIDDEN;
    }
    debug->is_live_output_enabled = true;
    renew_lease(debug, now_ms);
    return FLOW_DEBUG_OK;
}

/* Installs an optional monotonic microsecond source used to measure evaluator duration and high-water time. */
void flow_debug_set_time_source(flow_debug_t *debug, flow_debug_get_time_us_t get_time_us, void *time_context)
{
    if (debug != NULL)
    {
        debug->get_time_us  = get_time_us;
        debug->time_context = time_context;
    }
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
        append_u8(debug, &offset, (uint8_t)debug->state) &&
        append_u8(debug, &offset, debug->state == FLOW_DEBUG_RUNNING ? SNAPSHOT_MODE_FIXED : SNAPSHOT_MODE_MANUAL) &&
        append_u64(debug, &offset, snapshot->tick_number) && append_u64(debug, &offset, snapshot->sampled_at_ms) &&
        append_u64(debug, &offset, completed_at_ms) && append_u32(debug, &offset, debug->execution_duration_us) &&
        append_u8(debug, &offset, snapshot->input_validity) && append_u16(debug, &offset, snapshot->node_count) &&
        append_u16(debug, &offset, snapshot->output_count) && append_u32(debug, &offset, debug->overrun_count) &&
        append_u32(debug, &offset, snapshot->evaluation_failure_count) &&
        append_u16(debug, &offset, (uint16_t)snapshot->last_result.code) && append_u8(debug, &offset, 0) &&
        append_u32(debug, &offset, debug->execution_high_water_us) && append_u32(debug, &offset, debug->missed_deadline_count) &&
        append_u32(debug, &offset, debug->arbitration_loss_count);

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
    memset(debug, 0, sizeof(*debug));
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
    memcpy(debug->artifact_digest, digest, FLOW_DEBUG_DIGEST_BYTES);
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
    debug->state              = FLOW_DEBUG_STEPPING;
    const uint64_t started_us = debug->get_time_us == NULL ? 0U : debug->get_time_us(debug->time_context);
    flow_input_frame_t input  = {0};

    if (!debug->get_input(debug->input_context, &input))
    {
        debug->last_result = (flow_result_t){.code = FLOW_REASON_INPUT_QUALITY_REJECTED};
        debug->state       = FLOW_DEBUG_FAULT;
        relinquish_live_outputs(debug);
        return FLOW_DEBUG_VALIDATION_FAILED;
    }
    debug->last_result           = flow_runtime_step(&debug->runtime, &input);
    const uint64_t completed_us  = debug->get_time_us == NULL ? started_us : debug->get_time_us(debug->time_context);
    const uint64_t duration_us   = completed_us >= started_us ? completed_us - started_us : 0U;
    debug->execution_duration_us = duration_us > UINT32_MAX ? UINT32_MAX : (uint32_t)duration_us;

    if (debug->execution_duration_us > debug->execution_high_water_us)
    {
        debug->execution_high_water_us = debug->execution_duration_us;
    }

    if (debug->last_result.code != FLOW_REASON_OK)
    {
        debug->state = FLOW_DEBUG_FAULT;
        relinquish_live_outputs(debug);
        return FLOW_DEBUG_VALIDATION_FAILED;
    }

    if (!apply_live_outputs(debug, now_ms))
    {
        debug->state = FLOW_DEBUG_FAULT;
        return FLOW_DEBUG_VALIDATION_FAILED;
    }
    debug->state = FLOW_DEBUG_PAUSED;

    if (!encode_snapshot(debug, now_ms))
    {
        debug->state = FLOW_DEBUG_FAULT;
        relinquish_live_outputs(debug);
        return FLOW_DEBUG_VALIDATION_FAILED;
    }
    debug->published_tick_number    = debug->runtime.tick_number;
    debug->last_snapshot_publish_ms = now_ms;
    /* Manual stepping uses forced-safe behaviour: the evaluated command is applied and immediately relinquished. */
    relinquish_live_outputs(debug);
    renew_lease(debug, now_ms);
    return FLOW_DEBUG_OK;
}

/* Starts fixed-interval shadow execution from ready or paused state without overlapping evaluator ticks. */
flow_debug_result_t flow_debug_run(flow_debug_t *debug, uint32_t owner_id, uint64_t session_id, uint32_t interval_ms,
                                   uint64_t now_ms)
{
    enum
    {
        MINIMUM_INTERVAL_MS = 10,
        MAXIMUM_INTERVAL_MS = 60000,
    };

    if (debug == NULL || interval_ms < MINIMUM_INTERVAL_MS || interval_ms > MAXIMUM_INTERVAL_MS)
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
    debug->interval_ms  = interval_ms;
    debug->next_tick_ms = now_ms;
    debug->state        = FLOW_DEBUG_RUNNING;
    renew_lease(debug, now_ms);
    return FLOW_DEBUG_OK;
}

/* Pauses continuous execution while preserving memory and samples fresh inputs on the next step or run tick. */
flow_debug_result_t flow_debug_pause(flow_debug_t *debug, uint32_t owner_id, uint64_t session_id, uint64_t now_ms)
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

    if (debug->state != FLOW_DEBUG_RUNNING)
    {
        return FLOW_DEBUG_WRONG_STATE;
    }
    debug->state = FLOW_DEBUG_PAUSED;
    relinquish_live_outputs(debug);
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
    *status = (flow_debug_status_t){.session_id              = debug->session_id,
                                    .state                   = debug->state,
                                    .covered_bytes           = debug->covered_bytes,
                                    .artifact_length         = debug->artifact_length,
                                    .flow_revision           = debug->executable.revision,
                                    .tick_number             = debug->published_tick_number,
                                    .lease_remaining_ms      = FLOW_DEBUG_LEASE_MS,
                                    .interval_ms             = debug->interval_ms,
                                    .execution_duration_us   = debug->execution_duration_us,
                                    .execution_high_water_us = debug->execution_high_water_us,
                                    .missed_deadline_count   = debug->missed_deadline_count,
                                    .overrun_count           = debug->overrun_count,
                                    .arbitration_loss_count  = debug->arbitration_loss_count,
                                    .last_result             = debug->last_result};
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
    const flow_debug_result_t access = get_access(debug, owner_id, session_id);

    if (access != FLOW_DEBUG_OK)
    {
        return access;
    }

    if (debug->snapshot_length == 0U || debug->published_tick_number != tick_number)
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
    memcpy(header->digest, debug->snapshot_digest, sizeof(header->digest));
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
    memcpy(output, &debug->snapshot[offset], chunk_size);
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
    if (debug == NULL)
    {
        return;
    }

    if (debug->state != FLOW_DEBUG_EMPTY && now_ms >= debug->lease_deadline_ms)
    {
        debug->state = FLOW_DEBUG_STOPPED;
        clear_session(debug);
        return;
    }

    if (debug->state == FLOW_DEBUG_RUNNING && now_ms >= debug->next_tick_ms)
    {
        const uint64_t scheduled_ms = debug->next_tick_ms;

        if (now_ms > scheduled_ms)
        {
            const uint64_t late_intervals = (now_ms - scheduled_ms) / debug->interval_ms;
            debug->missed_deadline_count += late_intervals > UINT32_MAX - debug->missed_deadline_count
                                                ? UINT32_MAX - debug->missed_deadline_count
                                                : (uint32_t)late_intervals;
            debug->overrun_count += late_intervals > 0U && debug->overrun_count < UINT32_MAX ? 1U : 0U;
        }
        const uint64_t started_us = debug->get_time_us == NULL ? 0U : debug->get_time_us(debug->time_context);
        flow_input_frame_t input  = {0};

        if (!debug->get_input(debug->input_context, &input))
        {
            debug->last_result = (flow_result_t){.code = FLOW_REASON_INPUT_QUALITY_REJECTED};
            debug->state       = FLOW_DEBUG_FAULT;
            relinquish_live_outputs(debug);
            return;
        }
        debug->last_result = flow_runtime_step(&debug->runtime, &input);

        if (debug->last_result.code != FLOW_REASON_OK)
        {
            debug->state = FLOW_DEBUG_FAULT;
            relinquish_live_outputs(debug);
            return;
        }

        if (!apply_live_outputs(debug, now_ms))
        {
            debug->state = FLOW_DEBUG_FAULT;
            return;
        }
        const uint64_t completed_us  = debug->get_time_us == NULL ? started_us : debug->get_time_us(debug->time_context);
        const uint64_t duration_us   = completed_us >= started_us ? completed_us - started_us : 0U;
        debug->execution_duration_us = duration_us > UINT32_MAX ? UINT32_MAX : (uint32_t)duration_us;

        if (debug->execution_duration_us > debug->execution_high_water_us)
        {
            debug->execution_high_water_us = debug->execution_duration_us;
        }
        debug->next_tick_ms = scheduled_ms + ((now_ms - scheduled_ms) / debug->interval_ms + 1U) * debug->interval_ms;
        /* Encoding only the latest state bounds publication work; evaluation never waits for snapshot transfer. */
        const bool is_publish_due =
            debug->last_snapshot_publish_ms == 0U || now_ms - debug->last_snapshot_publish_ms >= SNAPSHOT_PUBLISH_INTERVAL_MS;

        if (is_publish_due && !encode_snapshot(debug, now_ms))
        {
            debug->state = FLOW_DEBUG_FAULT;
            relinquish_live_outputs(debug);
            return;
        }

        if (is_publish_due)
        {
            debug->published_tick_number    = debug->runtime.tick_number;
            debug->last_snapshot_publish_ms = now_ms;
        }
    }
}
