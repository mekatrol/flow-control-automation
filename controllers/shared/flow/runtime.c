#include "flow/runtime.h"

/*
 * Purpose: Implement deterministic evaluation of one prepared executable over
 * one coherent physical-input frame and publish the resulting runtime snapshot.
 * This file owns node evaluation, explicit one-tick memory behavior, input
 * quality enforcement, tick numbering, and the evaluator's atomic commit.
 *
 * Why this file exists: Manual Step and continuous Run must use exactly the same
 * controller semantics, and one step must represent a complete tick rather than
 * a sequence of externally visible node operations. A failed input or node must
 * not partially advance memory or replace only part of the visible snapshot.
 *
 * How it works: The prepared fixed schedule is evaluated into local value and
 * next-memory images using only the supplied coherent frame. A complete next
 * snapshot is assembled separately. Values, memory, snapshot, and tick number
 * are copied into runtime-owned state only after every operation succeeds;
 * otherwise the previous committed tick remains intact and a stable error is
 * returned to the debug-session layer.
 */

#include <stdio.h>
#include <string.h>

enum
{
    /* Validity is a bit image so downstream contracts retain which input guarantees were proven for this tick. */
    INPUT_VALID_COHERENT    = 1,
    INPUT_VALID_ALL_PRESENT = 2,
    INPUT_VALID_ALL_GOOD    = 4,
};

/* What: Creates a stable evaluation result with an optional node path. Why: Runtime faults must correlate to the source graph
 * across FCP and UI layers. How: It prefixes the bounded stable node ID and truncates safely to the frozen path capacity. */
static flow_result_t get_runtime_result(flow_reason_code_t code, const char *node_id)
{
    flow_result_t result = {.code = code};

    if (node_id != NULL)
    {
        static const char PREFIX[] = "/nodes/";
        const size_t prefix_size   = sizeof(PREFIX) - 1U;
        const size_t available     = sizeof(result.path) - prefix_size - 1U;
        const size_t node_id_size  = strlen(node_id) < available ? strlen(node_id) : available;
        memcpy(result.path, PREFIX, prefix_size);
        memcpy(&result.path[prefix_size], node_id, node_id_size);
        result.path[prefix_size + node_id_size] = '\0';
    }

    return result;
}

/*
 * Gets the sole connection driving one validated input port. Preparation has
 * already rejected ambiguity, so this bounded lookup cannot choose between
 * competing graph semantics during a tick.
 */
static const flow_connection_t *get_driver(const flow_executable_t *flow, uint16_t port_index)
{
    for (uint16_t index = 0; index < flow->connection_count; index++)
    {
        if (flow->connections[index].target_port_index == port_index)
        {
            return &flow->connections[index];
        }
    }

    return NULL;
}

/*
 * Resolves a named input through prepared port and connection indices, then
 * reads its source from the working tick image. The deterministic schedule is
 * why the source value is available before its consumer executes.
 */
static bool get_input_value(const flow_executable_t *flow, const bool values[FLOW_EXECUTABLE_MAX_NODES], uint16_t node_index,
                            const char *port_id, bool *value)
{
    for (uint16_t port_index = 0; port_index < flow->port_count; port_index++)
    {
        const flow_port_t *port = &flow->ports[port_index];

        if (port->node_index == node_index && port->direction == 1U && strcmp(port->id, port_id) == 0)
        {
            const flow_connection_t *driver = get_driver(flow, port_index);

            if (driver == NULL)
            {
                return false;
            }

            *value = values[driver->source_node_index];
            return true;
        }
    }

    return false;
}

/*
 * Gets one coherent, present, good sample by stable point ID. Evaluation reads
 * only this captured image—never a field bus—so input nodes share one sampling
 * boundary and blocking I/O cannot make tick timing unpredictable.
 */
static const flow_input_sample_t *get_sample(const flow_input_frame_t *input, const char *point_id)
{
    for (size_t index = 0; index < input->sample_count; index++)
    {
        if (strcmp(input->samples[index].point_id, point_id) == 0)
        {
            return &input->samples[index];
        }
    }

    return NULL;
}

/*
 * Initializes from a fully prepared executable and restores encoded memory
 * initial values. The executable must outlive the runtime; retaining a pointer
 * avoids allocation and keeps the evaluator footprint fixed.
 */
bool flow_runtime_init(flow_runtime_t *runtime, const flow_executable_t *executable)
{
    if (runtime == NULL || executable == NULL || executable->node_count == 0U)
    {
        return false;
    }
    *runtime = (flow_runtime_t){.executable = executable};
    flow_runtime_reset(runtime);

    return true;
}

/*
 * Restores initial memory and removes all published history. Reset is a
 * lifecycle boundary rather than a tick, so no initial-value image can be
 * mistaken for a physical-input evaluation.
 */
void flow_runtime_reset(flow_runtime_t *runtime)
{
    if (runtime == NULL || runtime->executable == NULL)
    {
        return;
    }
    const flow_executable_t *flow = runtime->executable;
    memset(runtime->current_memory, 0, sizeof(runtime->current_memory));
    memset(runtime->next_memory, 0, sizeof(runtime->next_memory));
    memset(runtime->values, 0, sizeof(runtime->values));
    memset(&runtime->snapshot, 0, sizeof(runtime->snapshot));
    runtime->tick_number              = 0;
    runtime->evaluation_failure_count = 0;

    for (uint16_t index = 0; index < flow->node_count; index++)
    {
        if (flow->nodes[index].kind == FLOW_NODE_MEMORY)
        {
            runtime->current_memory[index] = flow->nodes[index].initial_value;
            runtime->next_memory[index]    = flow->nodes[index].initial_value;
        }
    }
}

/*
 * Evaluates one complete schedule against a coherent frame. Values, next
 * memory, and the snapshot are constructed privately; only total success
 * advances runtime state, while any quality or graph-contract failure preserves
 * the previously committed tick.
 */
flow_result_t flow_runtime_step(flow_runtime_t *runtime, const flow_input_frame_t *input)
{
    if (runtime == NULL || runtime->executable == NULL || input == NULL || (input->sample_count > 0U && input->samples == NULL))
    {
        return get_runtime_result(FLOW_REASON_EVALUATION_FAILED, NULL);
    }
    const flow_executable_t *flow = runtime->executable;
    /* Working images isolate partial computation from memory and snapshots visible to callers. */
    bool working_values[FLOW_EXECUTABLE_MAX_NODES] = {false};
    bool working_memory[FLOW_EXECUTABLE_MAX_NODES];
    memcpy(working_memory, runtime->current_memory, sizeof(working_memory));

    if (!input->is_coherent)
    {
        runtime->evaluation_failure_count++;

        return get_runtime_result(FLOW_REASON_INPUT_QUALITY_REJECTED, NULL);
    }

    /* Evaluate into local fixed-capacity buffers so a failed tick cannot expose partial values or memory. */
    /* Prepared order places same-tick dependencies first; memory nodes read only the prior committed image. */
    for (uint16_t position = 0; position < flow->node_count; position++)
    {
        const uint16_t node_index = flow->schedule[position];
        const flow_node_t *node   = &flow->nodes[node_index];
        bool left;
        bool right;

        switch (node->kind)
        {
            case FLOW_NODE_DIGITAL_INPUT: {
                const flow_input_sample_t *sample = get_sample(input, flow->points[node->point_index].id);

                if (sample == NULL || sample->quality != FLOW_QUALITY_GOOD)
                {
                    runtime->evaluation_failure_count++;

                    return get_runtime_result(FLOW_REASON_INPUT_QUALITY_REJECTED, node->id);
                }
                working_values[node_index] = sample->value;
                break;
            }
            case FLOW_NODE_DIGITAL_CONSTANT:
                working_values[node_index] = node->initial_value;
                break;
            case FLOW_NODE_MEMORY:
                working_values[node_index] = runtime->current_memory[node_index];
                break;
            case FLOW_NODE_NOT:

                if (!get_input_value(flow, working_values, node_index, "in", &left))
                {
                    goto evaluation_failed;
                }
                working_values[node_index] = !left;
                break;
            case FLOW_NODE_AND:

                if (!get_input_value(flow, working_values, node_index, "a", &left) ||
                    !get_input_value(flow, working_values, node_index, "b", &right))
                {
                    goto evaluation_failed;
                }
                working_values[node_index] = left && right;
                break;
            case FLOW_NODE_OR:

                if (!get_input_value(flow, working_values, node_index, "a", &left) ||
                    !get_input_value(flow, working_values, node_index, "b", &right))
                {
                    goto evaluation_failed;
                }
                working_values[node_index] = left || right;
                break;
            case FLOW_NODE_PROPOSED_OUTPUT:

                if (!get_input_value(flow, working_values, node_index, "in", &left))
                {
                    goto evaluation_failed;
                }
                working_values[node_index] = left;
                break;
            default:
                goto evaluation_failed;
        }
    }

    /* Derive every memory write after visible values exist, implementing an explicit one-tick feedback delay. */
    for (uint16_t node_index = 0; node_index < flow->node_count; node_index++)
    {
        if (flow->nodes[node_index].kind == FLOW_NODE_MEMORY)
        {
            if (!get_input_value(flow, working_values, node_index, "in", &working_memory[node_index]))
            {
                goto evaluation_failed;
            }
        }
    }
    /* Build a complete replacement snapshot locally so readers cannot observe nodes from different ticks. */
    flow_tick_snapshot_t next = {.tick_number    = runtime->tick_number + 1U,
                                 .sampled_at_ms  = input->sampled_at_ms,
                                 .input_validity = INPUT_VALID_COHERENT | INPUT_VALID_ALL_PRESENT | INPUT_VALID_ALL_GOOD,
                                 .node_count     = flow->node_count,
                                 .output_count   = flow->output_count,
                                 .evaluation_failure_count = runtime->evaluation_failure_count,
                                 .last_result              = {.code = FLOW_REASON_OK}};
    uint16_t output_index     = 0;

    for (uint16_t node_index = 0; node_index < flow->node_count; node_index++)
    {
        snprintf(next.nodes[node_index].node_id, sizeof(next.nodes[node_index].node_id), "%s", flow->nodes[node_index].id);
        next.nodes[node_index].value   = working_values[node_index];
        next.nodes[node_index].quality = FLOW_QUALITY_GOOD;

        if (flow->nodes[node_index].kind == FLOW_NODE_PROPOSED_OUTPUT)
        {
            flow_output_snapshot_t *output = &next.outputs[output_index++];
            snprintf(output->point_id, sizeof(output->point_id), "%s", flow->points[flow->nodes[node_index].point_index].id);
            output->value   = working_values[node_index];
            output->quality = FLOW_QUALITY_GOOD;
        }
    }
    /* This final group is the publication boundary for values, memory, snapshot identity, and tick number. */
    memcpy(runtime->values, working_values, sizeof(runtime->values));
    memcpy(runtime->current_memory, working_memory, sizeof(runtime->current_memory));
    memcpy(runtime->next_memory, working_memory, sizeof(runtime->next_memory));
    runtime->snapshot    = next;
    runtime->tick_number = next.tick_number;

    return get_runtime_result(FLOW_REASON_OK, NULL);

evaluation_failed:
    runtime->evaluation_failure_count++;

    return get_runtime_result(FLOW_REASON_EVALUATION_FAILED, NULL);
}

/* What: Returns the last successfully committed tick snapshot. Why: Callers must never treat reset or failed working state as
 * published data. How: It exposes runtime-owned storage only after the tick counter proves a successful commit. */
const flow_tick_snapshot_t *get_flow_runtime_snapshot(const flow_runtime_t *runtime)
{
    return runtime != NULL && runtime->tick_number > 0U ? &runtime->snapshot : NULL;
}
