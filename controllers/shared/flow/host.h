#pragma once

/*
 * Purpose: Host one durably activated Flow IL v1 artifact on a controller.
 *
 * Why this contract exists: Durable deployment and volatile debugging must use
 * the normative portable VM without allowing transport code to own execution.
 *
 * How callers use it: Supply coherent input and command adapters, synchronize
 * after activation changes, and invoke non-overlapping scans from the scheduler.
 */

#include "flow/service.h"
#include "flow/vm.h"

typedef bool (*flow_host_read_inputs_t)(void *context, flow_vm_input_sample_t *samples, size_t capacity, size_t *count,
                                        uint64_t *sampled_at_ms);
typedef bool (*flow_host_publish_commands_t)(void *context, const flow_vm_command_t *commands, size_t count, uint64_t now_ms);

typedef struct
{
    flow_vm_t instances[2];
    uint8_t active_instance;
    bool is_running;
    uint32_t active_revision;
    flow_host_read_inputs_t read_inputs;
    flow_host_publish_commands_t publish_commands;
    void *adapter_context;
    flow_vm_result_t last_result;
} flow_host_t;

/* Initializes an empty production host with bounded controller I/O adapters. */
bool flow_host_init(flow_host_t *host, flow_host_read_inputs_t read_inputs, flow_host_publish_commands_t publish_commands,
                    void *adapter_context);

/* Prepares an active committed generation before atomically replacing the running VM. */
bool flow_host_synchronize(flow_host_t *host, const controller_flow_t *deployment);

/* Runs one complete PLC scan and publishes commands only after successful VM commit. */
bool flow_host_scan(flow_host_t *host, uint64_t now_ms, flow_vm_snapshot_t *snapshot);

/* Stops execution and clears both bounded VM instances. */
void flow_host_stop(flow_host_t *host);
