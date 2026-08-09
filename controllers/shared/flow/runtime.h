#ifndef CONTROLLER_FLOW_RUNTIME_H
#define CONTROLLER_FLOW_RUNTIME_H

/*
 * Purpose: Define the portable input-frame, evaluator-state, quality, and tick-
 * snapshot contracts used to execute a prepared flow.
 *
 * Why this contract exists: Evaluation needs a platform-neutral boundary that
 * guarantees one coherent input image and one immutable result per successful
 * complete tick. Sampling hardware, scheduling sessions, transferring bytes,
 * and arbitrating physical outputs belong outside the evaluator because those
 * operations would make tick timing and semantics platform dependent.
 *
 * How callers use it: A platform adapter captures flow_input_frame_t once, and
 * flow_runtime_step() evaluates the executable's fixed schedule without I/O or
 * allocation. flow_runtime_t holds separate current and next memory images and
 * publishes flow_tick_snapshot_t only at the success boundary. Stable node and
 * point IDs allow the backend and designer to correlate values with the graph.
 */

#include "flow/executable.h"

typedef enum
{
    /* Quality travels with values so missing or stale field data cannot silently become a Boolean command. */
    FLOW_QUALITY_GOOD        = 0,
    FLOW_QUALITY_UNCERTAIN   = 1,
    FLOW_QUALITY_BAD         = 2,
    FLOW_QUALITY_UNAVAILABLE = 3,
} flow_quality_t;

typedef struct
{
    /* Stable point identity joins the sampled hardware value to the point binding validated during preparation. */
    char point_id[FLOW_EXECUTABLE_MAX_ID_BYTES + 1];
    bool value;
    flow_quality_t quality;
} flow_input_sample_t;

typedef struct
{
    /* Every sample in a coherent frame belongs to one platform sampling boundary and timestamp. */
    const flow_input_sample_t *samples;
    size_t sample_count;
    uint64_t sampled_at_ms;
    bool is_coherent;
} flow_input_frame_t;

typedef struct
{
    /* Stable node identity lets the backend and designer correlate values without depending on schedule position. */
    char node_id[FLOW_EXECUTABLE_MAX_ID_BYTES + 1];
    bool value;
    flow_quality_t quality;
} flow_node_snapshot_t;

typedef struct
{
    char point_id[FLOW_EXECUTABLE_MAX_ID_BYTES + 1];
    bool value;
    flow_quality_t quality;
} flow_output_snapshot_t;

typedef struct
{
    /* A snapshot is a complete immutable observation of one successful tick, correlated by stable IDs. */
    uint64_t tick_number;
    uint64_t sampled_at_ms;
    uint8_t input_validity;
    uint16_t node_count;
    uint16_t output_count;
    flow_node_snapshot_t nodes[FLOW_EXECUTABLE_MAX_NODES];
    flow_output_snapshot_t outputs[FLOW_EXECUTABLE_MAX_OUTPUTS];
    uint32_t evaluation_failure_count;
    flow_result_t last_result;
} flow_tick_snapshot_t;

typedef struct
{
    /* Separate current/next memory arrays implement explicit one-tick delay and provide the atomic commit boundary. */
    const flow_executable_t *executable;
    bool current_memory[FLOW_EXECUTABLE_MAX_NODES];
    bool next_memory[FLOW_EXECUTABLE_MAX_NODES];
    bool values[FLOW_EXECUTABLE_MAX_NODES];
    flow_tick_snapshot_t snapshot;
    uint64_t tick_number;
    uint32_t evaluation_failure_count;
} flow_runtime_t;

/* What: Initializes evaluator state. Why: Every session starts from artifact-defined memory. How: Retains the prepared executable
 * and restores bounded current/next images. */
bool flow_runtime_init(flow_runtime_t *runtime, const flow_executable_t *executable);

/* What: Evaluates one complete tick. Why: Partial node or memory results must never escape. How: Uses private working images and
 * commits values, memory, and snapshot only on total success. */
flow_result_t flow_runtime_step(flow_runtime_t *runtime, const flow_input_frame_t *input);

/* What: Resets evaluator history. Why: Reuse must not expose prior session values. How: Clears counters/snapshot and reapplies
 * every encoded memory initial value. */
void flow_runtime_reset(flow_runtime_t *runtime);

/* What: Retrieves the committed snapshot. Why: Failed or absent ticks must not appear publishable. How: Returns runtime-owned
 * storage only after a successful tick number exists. */
const flow_tick_snapshot_t *get_flow_runtime_snapshot(const flow_runtime_t *runtime);

#endif
