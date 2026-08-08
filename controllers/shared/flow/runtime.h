#ifndef CONTROLLER_FLOW_RUNTIME_H
#define CONTROLLER_FLOW_RUNTIME_H

#include "flow/executable.h"

typedef enum
{
    FLOW_QUALITY_GOOD = 0,
    FLOW_QUALITY_UNCERTAIN = 1,
    FLOW_QUALITY_BAD = 2,
    FLOW_QUALITY_UNAVAILABLE = 3,
} flow_quality_t;

typedef struct
{
    char point_id[FLOW_EXECUTABLE_MAX_ID_BYTES + 1];
    bool value;
    flow_quality_t quality;
} flow_input_sample_t;

typedef struct
{
    const flow_input_sample_t *samples;
    size_t sample_count;
    uint64_t sampled_at_ms;
    bool is_coherent;
} flow_input_frame_t;

typedef struct
{
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
    const flow_executable_t *executable;
    bool current_memory[FLOW_EXECUTABLE_MAX_NODES];
    bool next_memory[FLOW_EXECUTABLE_MAX_NODES];
    bool values[FLOW_EXECUTABLE_MAX_NODES];
    flow_tick_snapshot_t snapshot;
    uint64_t tick_number;
    uint32_t evaluation_failure_count;
} flow_runtime_t;

/* Initializes a prepared runtime and restores every memory node's encoded initial value. */
bool flow_runtime_init(flow_runtime_t *runtime, const flow_executable_t *executable);

/* Evaluates one all-or-nothing tick without allocation and atomically publishes snapshot and memory state on success. */
flow_result_t flow_runtime_step(flow_runtime_t *runtime, const flow_input_frame_t *input);

/* Restores initial memory and clears all tick, snapshot, and fault counters. */
void flow_runtime_reset(flow_runtime_t *runtime);

/* Returns the latest immutable runtime-owned snapshot, or NULL before the first successful tick. */
const flow_tick_snapshot_t *get_flow_runtime_snapshot(const flow_runtime_t *runtime);

#endif
