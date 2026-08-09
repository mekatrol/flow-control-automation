#include "flow/runtime.h"

#include <stdio.h>
#include <string.h>

enum
{
    INPUT_VALID_COHERENT    = 1,
    INPUT_VALID_ALL_PRESENT = 2,
    INPUT_VALID_ALL_GOOD    = 4,
};

/* Creates one stable runtime result with an optional bounded node path. */
static flow_result_t get_runtime_result(flow_reason_code_t code, const char *node_id)
{
    flow_result_t result = {.code = code};
    if (node_id != NULL)
    {
        static const char PREFIX[] = "/nodes/";
        const size_t prefix_size   = sizeof(PREFIX) - 1U;
        const size_t available     = sizeof(result.path) - prefix_size - 1U;
        const size_t node_id_size  = strlen(node_id) < available ? strlen(node_id) : available;
        (void)memcpy(result.path, PREFIX, prefix_size);
        (void)memcpy(&result.path[prefix_size], node_id, node_id_size);
        result.path[prefix_size + node_id_size] = '\0';
    }
    return result;
}

/* Gets the connection driving one validated input port. */
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

/* Gets a node's named input value through the prepared connection table. */
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

/* Gets one coherent, present, good input sample by stable point ID. */
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

/* Initializes a prepared runtime and restores every memory node's encoded initial value. */
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

/* Restores initial memory and clears all tick, snapshot, and fault counters. */
void flow_runtime_reset(flow_runtime_t *runtime)
{
    if (runtime == NULL || runtime->executable == NULL)
    {
        return;
    }
    const flow_executable_t *flow = runtime->executable;
    (void)memset(runtime->current_memory, 0, sizeof(runtime->current_memory));
    (void)memset(runtime->next_memory, 0, sizeof(runtime->next_memory));
    (void)memset(runtime->values, 0, sizeof(runtime->values));
    (void)memset(&runtime->snapshot, 0, sizeof(runtime->snapshot));
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

/* Evaluates one all-or-nothing tick without allocation and atomically publishes snapshot and memory state on success. */
flow_result_t flow_runtime_step(flow_runtime_t *runtime, const flow_input_frame_t *input)
{
    if (runtime == NULL || runtime->executable == NULL || input == NULL || (input->sample_count > 0U && input->samples == NULL))
    {
        return get_runtime_result(FLOW_REASON_EVALUATION_FAILED, NULL);
    }
    const flow_executable_t *flow                  = runtime->executable;
    bool working_values[FLOW_EXECUTABLE_MAX_NODES] = {false};
    bool working_memory[FLOW_EXECUTABLE_MAX_NODES];
    (void)memcpy(working_memory, runtime->current_memory, sizeof(working_memory));
    if (!input->is_coherent)
    {
        runtime->evaluation_failure_count++;
        return get_runtime_result(FLOW_REASON_INPUT_QUALITY_REJECTED, NULL);
    }
    /* Evaluate into local fixed-capacity buffers so a failed tick cannot expose partial values or memory. */
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
        (void)snprintf(next.nodes[node_index].node_id, sizeof(next.nodes[node_index].node_id), "%s", flow->nodes[node_index].id);
        next.nodes[node_index].value   = working_values[node_index];
        next.nodes[node_index].quality = FLOW_QUALITY_GOOD;
        if (flow->nodes[node_index].kind == FLOW_NODE_PROPOSED_OUTPUT)
        {
            flow_output_snapshot_t *output = &next.outputs[output_index++];
            (void)snprintf(output->point_id, sizeof(output->point_id), "%s",
                           flow->points[flow->nodes[node_index].point_index].id);
            output->value   = working_values[node_index];
            output->quality = FLOW_QUALITY_GOOD;
        }
    }
    (void)memcpy(runtime->values, working_values, sizeof(runtime->values));
    (void)memcpy(runtime->current_memory, working_memory, sizeof(runtime->current_memory));
    (void)memcpy(runtime->next_memory, working_memory, sizeof(runtime->next_memory));
    runtime->snapshot    = next;
    runtime->tick_number = next.tick_number;
    return get_runtime_result(FLOW_REASON_OK, NULL);

evaluation_failed:
    runtime->evaluation_failure_count++;
    return get_runtime_result(FLOW_REASON_EVALUATION_FAILED, NULL);
}

/* Returns the latest immutable runtime-owned snapshot, or NULL before the first successful tick. */
const flow_tick_snapshot_t *get_flow_runtime_snapshot(const flow_runtime_t *runtime)
{
    return runtime != NULL && runtime->tick_number > 0U ? &runtime->snapshot : NULL;
}
