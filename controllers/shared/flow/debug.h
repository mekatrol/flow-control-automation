#ifndef CONTROLLER_FLOW_DEBUG_H
#define CONTROLLER_FLOW_DEBUG_H

/*
 * Purpose: Define the public contract and fixed-capacity state for one volatile
 * controller flow-debug session, including its control plane, snapshot data
 * plane, lifecycle, platform adapters, diagnostics, and live-output policy.
 *
 * Why this contract exists: Debugging must evaluate real controller semantics
 * without deploying, activating, replacing, or removing the durable production
 * generation. Ownership, lease expiry, immutable tick identity, bounded memory,
 * and explicit live confirmation make that temporary capability safe to expose
 * through authenticated FCP operations.
 *
 * How callers use it: Firmware installs coherent-input, monotonic-time, and
 * optional output-arbitration adapters, then routes authenticated lifecycle
 * requests through the functions below. Artifact and snapshot bytes remain in
 * caller-owned fixed arrays. The service retains one latest complete snapshot
 * for chunked reads and clears all session-owned state and commands on every
 * terminal safety path; installed platform adapters and session monotonicity
 * survive that clearing operation.
 */

#include "flow/executable.h"
#include "flow/runtime.h"

enum
{
    /* Protocol-sized limits bound RAM, transfer work, and snapshot chunking independently of transport speed. */
    FLOW_DEBUG_DIGEST_BYTES         = 32,
    FLOW_DEBUG_COVERAGE_BYTES       = FLOW_EXECUTABLE_MAX_ARTIFACT_BYTES / 8,
    FLOW_DEBUG_SNAPSHOT_CAPACITY    = 16384,
    FLOW_DEBUG_LEASE_MS             = 30000,
    FLOW_DEBUG_CHUNK_LIMIT          = 180,
    FLOW_DEBUG_SNAPSHOT_CHUNK_LIMIT = 173,
    FLOW_DEBUG_LIVE_OUTPUT_PRIORITY = 8,
    FLOW_DEBUG_LIVE_OUTPUT_HOLD_MS  = 1000,
};

typedef enum
{
    /* Lifecycle values are externally observable and encode which control-plane operations are currently legal. */
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
    /* Access, lifecycle, integrity, and validation failures remain distinct so protocol mapping is deterministic. */
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

/* Platform adapters isolate coherent sampling, monotonic timing, and arbitration from the portable session state machine. */
typedef uint64_t (*flow_debug_get_time_us_t)(void *context);
typedef bool (*flow_debug_command_output_t)(void *context, const char *point_id, bool value, uint8_t priority,
                                            uint64_t expires_at_ms, bool *is_effective);
typedef void (*flow_debug_relinquish_output_t)(void *context, const char *point_id);

typedef struct
{
    /* Status is a small control-plane view; bulk immutable node values are retrieved through snapshot chunks. */
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
    uint32_t arbitration_loss_count;
    flow_result_t last_result;
} flow_debug_status_t;

typedef struct
{
    /* Header identity and digest let a backend reject mixed ticks, sessions, missing chunks, or corrupted assembly. */
    uint64_t session_id;
    uint64_t tick_number;
    uint32_t total_length;
    uint16_t chunk_count;
    uint16_t chunk_data_limit;
    uint8_t digest[FLOW_DEBUG_DIGEST_BYTES];
} flow_debug_snapshot_header_t;

typedef struct
{
    /* Session-owned bytes are fixed-capacity and volatile; installed platform adapters survive session clearing. */
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
    uint32_t arbitration_loss_count;
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
    flow_debug_command_output_t command_output;
    flow_debug_relinquish_output_t relinquish_output;
    void *output_context;
    bool is_live_output_enabled;
} flow_debug_t;

/* What: Initializes an empty service. Why: Platform contracts must precede all sessions. How: Stores the immutable target/input
 * adapter and clears volatile ownership. */
bool flow_debug_init(flow_debug_t *debug, const flow_target_t *target, flow_debug_get_input_t get_input, void *input_context);

/* What: Installs tick timing. Why: Duration must be observable without a platform clock dependency. How: Stores an optional
 * monotonic callback and context. */
void flow_debug_set_time_source(flow_debug_t *debug, flow_debug_get_time_us_t get_time_us, void *time_context);

/* What: Installs physical-output arbitration callbacks. Why: Portable debug code cannot own hardware policy. How: Records
 * command/relinquish adapters while live mode remains disabled. */
void flow_debug_set_output_adapter(flow_debug_t *debug, flow_debug_command_output_t command_output,
                                   flow_debug_relinquish_output_t relinquish_output, void *output_context);

/* What: Enables live commands for one prepared session. Why: Shadow results must never imply physical authority. How: Requires
 * owner access and exact canonical affected-point confirmation. */
flow_debug_result_t flow_debug_enable_live_output(flow_debug_t *debug, uint32_t owner_id, uint64_t session_id,
                                                  const char *const *confirmed_point_ids, size_t point_count, uint64_t now_ms);

/* What: Opens a volatile artifact upload. Why: Debug bytes must remain separate from deployment and replacement must be explicit.
 * How: Clears authorized prior state and allocates a monotonic session ID. */
flow_debug_result_t flow_debug_begin(flow_debug_t *debug, uint32_t owner_id, bool replace_existing, uint32_t artifact_length,
                                     const uint8_t digest[FLOW_DEBUG_DIGEST_BYTES], uint64_t now_ms, uint64_t *session_id);

/* What: Merges one upload chunk. Why: Transport retries must be safe while conflicting bytes are rejected. How: Tracks byte
 * coverage and renews the authenticated owner's lease. */
flow_debug_result_t flow_debug_write(flow_debug_t *debug, uint32_t owner_id, uint64_t session_id, uint32_t offset,
                                     const uint8_t *data, size_t size, uint64_t now_ms);

/* What: Prepares the uploaded executable. Why: Ticks may consume only complete, compatible controller semantics. How: Verifies
 * coverage/digest, delegates schema validation, and initializes runtime state. */
flow_debug_result_t flow_debug_prepare(flow_debug_t *debug, uint32_t owner_id, uint64_t session_id, uint64_t now_ms);

/* What: Performs one complete manual tick. Why: Node-level visibility would violate atomic memory/snapshot semantics. How:
 * Captures one coherent frame, evaluates, publishes, and forces live commands safe. */
flow_debug_result_t flow_debug_step(flow_debug_t *debug, uint32_t owner_id, uint64_t session_id, uint64_t now_ms);

/* What: Arms continuous execution. Why: Controller-owned scheduling avoids client latency and overlapping ticks. How: Records a
 * bounded interval and monotonic next deadline. */
flow_debug_result_t flow_debug_run(flow_debug_t *debug, uint32_t owner_id, uint64_t session_id, uint32_t interval_ms,
                                   uint64_t now_ms);

/* What: Pauses run mode. Why: Paused memory must remain inspectable while physical commands become safe. How: Stops scheduling,
 * preserves committed evaluator state, and relinquishes outputs. */
flow_debug_result_t flow_debug_pause(flow_debug_t *debug, uint32_t owner_id, uint64_t session_id, uint64_t now_ms);

/* What: Retrieves bounded lifecycle and diagnostic status. Why: Control-plane inspection should not require bulk snapshot
 * transfer. How: Verifies ownership, copies coherent counters, and renews the lease. */
flow_debug_result_t flow_debug_get_status(flow_debug_t *debug, uint32_t owner_id, uint64_t session_id, uint64_t now_ms,
                                          flow_debug_status_t *status);

/* What: Retrieves the latest snapshot transfer header. Why: Chunk consumers must bind data to one session and tick. How: Requires
 * exact identity and returns length, chunk contract, and digest. */
flow_debug_result_t flow_debug_get_snapshot_header(flow_debug_t *debug, uint32_t owner_id, uint64_t session_id,
                                                   uint64_t tick_number, uint64_t now_ms, flow_debug_snapshot_header_t *header);

/* What: Reads one snapshot chunk. Why: Large immutable snapshots exceed one bounded FCP frame. How: Validates
 * session/tick/index/capacity and returns the exact absolute byte range. */
flow_debug_result_t flow_debug_read_snapshot_chunk(flow_debug_t *debug, uint32_t owner_id, uint64_t session_id,
                                                   uint64_t tick_number, uint16_t chunk_index, uint64_t now_ms, uint8_t *output,
                                                   size_t capacity, uint32_t *absolute_offset, size_t *size);

/* What: Sends a session keepalive. Why: Lost owners must expire even when transport connection state is ambiguous. How:
 * Authenticates identity and moves the fixed dead-man deadline. */
flow_debug_result_t flow_debug_renew(flow_debug_t *debug, uint32_t owner_id, uint64_t session_id, uint64_t now_ms);

/* What: Stops the session. Why: Explicit termination must release all debug state and commands without deployment effects. How:
 * Verifies ownership, relinquishes outputs, and clears volatile storage. */
flow_debug_result_t flow_debug_stop(flow_debug_t *debug, uint32_t owner_id, uint64_t session_id);

/* What: Advances supervisor-owned expiry and run scheduling. Why: Safety cleanup and ticks cannot depend on client polling. How:
 * Clears expired state or executes at most one due tick while skipping missed deadlines. */
void flow_debug_process(flow_debug_t *debug, uint64_t now_ms);

#endif
