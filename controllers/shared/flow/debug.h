#ifndef CONTROLLER_FLOW_DEBUG_H
#define CONTROLLER_FLOW_DEBUG_H

#include "flow/executable.h"
#include "flow/runtime.h"

enum
{
    FLOW_DEBUG_DIGEST_BYTES         = 32,
    FLOW_DEBUG_COVERAGE_BYTES       = FLOW_EXECUTABLE_MAX_ARTIFACT_BYTES / 8,
    FLOW_DEBUG_SNAPSHOT_CAPACITY    = 16384,
    FLOW_DEBUG_LEASE_MS             = 30000,
    FLOW_DEBUG_CHUNK_LIMIT          = 180,
    FLOW_DEBUG_SNAPSHOT_CHUNK_LIMIT = 173,
};

typedef enum
{
    FLOW_DEBUG_EMPTY    = 0,
    FLOW_DEBUG_LOADING  = 1,
    FLOW_DEBUG_READY    = 2,
    FLOW_DEBUG_STEPPING = 3,
    FLOW_DEBUG_PAUSED   = 4,
    FLOW_DEBUG_FAULT    = 5,
    FLOW_DEBUG_STOPPED  = 6,
    FLOW_DEBUG_RUNNING  = 7,
} flow_debug_state_t;

typedef enum
{
    FLOW_DEBUG_OK,
    FLOW_DEBUG_INVALID_ARGUMENT,
    FLOW_DEBUG_WRONG_STATE,
    FLOW_DEBUG_NOT_FOUND,
    FLOW_DEBUG_FORBIDDEN,
    FLOW_DEBUG_CONFLICT,
    FLOW_DEBUG_DIGEST_MISMATCH,
    FLOW_DEBUG_VALIDATION_FAILED,
} flow_debug_result_t;

typedef bool (*flow_debug_get_input_t)(void *context, flow_input_frame_t *frame);
typedef uint64_t (*flow_debug_get_time_us_t)(void *context);

typedef struct
{
    uint64_t session_id;
    flow_debug_state_t state;
    uint32_t covered_bytes;
    uint32_t artifact_length;
    uint32_t flow_revision;
    uint64_t tick_number;
    uint32_t lease_remaining_ms;
    uint32_t interval_ms;
    uint32_t execution_duration_us;
    uint32_t execution_high_water_us;
    uint32_t missed_deadline_count;
    uint32_t overrun_count;
    flow_result_t last_result;
} flow_debug_status_t;

typedef struct
{
    uint64_t session_id;
    uint64_t tick_number;
    uint32_t total_length;
    uint16_t chunk_count;
    uint16_t chunk_data_limit;
    uint8_t digest[FLOW_DEBUG_DIGEST_BYTES];
} flow_debug_snapshot_header_t;

typedef struct
{
    flow_debug_state_t state;
    uint64_t session_id;
    uint64_t next_session_id;
    uint32_t owner_id;
    uint64_t lease_deadline_ms;
    uint64_t next_tick_ms;
    uint64_t published_tick_number;
    uint64_t last_snapshot_publish_ms;
    uint32_t interval_ms;
    uint32_t execution_duration_us;
    uint32_t execution_high_water_us;
    uint32_t missed_deadline_count;
    uint32_t overrun_count;
    uint32_t artifact_length;
    uint32_t covered_bytes;
    uint8_t artifact_digest[FLOW_DEBUG_DIGEST_BYTES];
    uint8_t artifact[FLOW_EXECUTABLE_MAX_ARTIFACT_BYTES];
    uint8_t coverage[FLOW_DEBUG_COVERAGE_BYTES];
    flow_executable_t executable;
    flow_runtime_t runtime;
    uint8_t snapshot[FLOW_DEBUG_SNAPSHOT_CAPACITY];
    uint32_t snapshot_length;
    uint8_t snapshot_digest[FLOW_DEBUG_DIGEST_BYTES];
    flow_result_t last_result;
    const flow_target_t *target;
    flow_debug_get_input_t get_input;
    void *input_context;
    flow_debug_get_time_us_t get_time_us;
    void *time_context;
} flow_debug_t;

/* Initializes an empty volatile debug owner with immutable target and input adapters. */
bool flow_debug_init(flow_debug_t *debug, const flow_target_t *target, flow_debug_get_input_t get_input, void *input_context);

/* Installs an optional monotonic microsecond source used to measure evaluator duration and high-water time. */
void flow_debug_set_time_source(flow_debug_t *debug, flow_debug_get_time_us_t get_time_us, void *time_context);

/* Starts a bounded load, optionally replacing an existing session owned by any authenticated peer. */
flow_debug_result_t flow_debug_begin(flow_debug_t *debug, uint32_t owner_id, bool replace_existing, uint32_t artifact_length,
                                     const uint8_t digest[FLOW_DEBUG_DIGEST_BYTES], uint64_t now_ms, uint64_t *session_id);

/* Writes an idempotent artifact chunk and renews the owning session lease. */
flow_debug_result_t flow_debug_write(flow_debug_t *debug, uint32_t owner_id, uint64_t session_id, uint32_t offset,
                                     const uint8_t *data, size_t size, uint64_t now_ms);

/* Validates and prepares one fully covered artifact without touching durable flow state. */
flow_debug_result_t flow_debug_prepare(flow_debug_t *debug, uint32_t owner_id, uint64_t session_id, uint64_t now_ms);

/* Samples physical inputs and atomically evaluates one complete shadow tick. */
flow_debug_result_t flow_debug_step(flow_debug_t *debug, uint32_t owner_id, uint64_t session_id, uint64_t now_ms);

/* Starts fixed-interval shadow execution from ready or paused state without overlapping evaluator ticks. */
flow_debug_result_t flow_debug_run(flow_debug_t *debug, uint32_t owner_id, uint64_t session_id, uint32_t interval_ms,
                                   uint64_t now_ms);

/* Pauses continuous execution while preserving memory and samples fresh inputs on the next step or run tick. */
flow_debug_result_t flow_debug_pause(flow_debug_t *debug, uint32_t owner_id, uint64_t session_id, uint64_t now_ms);

/* Gets status for the owning session and renews its fixed lease. */
flow_debug_result_t flow_debug_get_status(flow_debug_t *debug, uint32_t owner_id, uint64_t session_id, uint64_t now_ms,
                                          flow_debug_status_t *status);

/* Gets metadata for the latest immutable snapshot for one exact tick. */
flow_debug_result_t flow_debug_get_snapshot_header(flow_debug_t *debug, uint32_t owner_id, uint64_t session_id,
                                                   uint64_t tick_number, uint64_t now_ms, flow_debug_snapshot_header_t *header);

/* Copies one indexed immutable snapshot chunk and returns its absolute offset and size. */
flow_debug_result_t flow_debug_read_snapshot_chunk(flow_debug_t *debug, uint32_t owner_id, uint64_t session_id,
                                                   uint64_t tick_number, uint16_t chunk_index, uint64_t now_ms, uint8_t *output,
                                                   size_t capacity, uint32_t *absolute_offset, size_t *size);

/* Renews the fixed lease for an owned non-empty session. */
flow_debug_result_t flow_debug_renew(flow_debug_t *debug, uint32_t owner_id, uint64_t session_id, uint64_t now_ms);

/* Stops and securely clears one owned volatile session. */
flow_debug_result_t flow_debug_stop(flow_debug_t *debug, uint32_t owner_id, uint64_t session_id);

/* Expires and clears a session whose authenticated lease deadline has elapsed. */
void flow_debug_process(flow_debug_t *debug, uint64_t now_ms);

#endif
