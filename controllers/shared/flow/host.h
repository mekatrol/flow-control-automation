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
#include "flow/virtual_points.h"

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
    flow_virtual_point_store_t *virtual_points;
    char execution_instance_id[FLOW_VIRTUAL_POINT_ID_CAPACITY];
    char deployment_id[FLOW_VIRTUAL_POINT_ID_CAPACITY];
} flow_host_t;

/* Initializes an empty production host with bounded controller I/O adapters. */
bool flow_host_init(flow_host_t *host, flow_host_read_inputs_t read_inputs, flow_host_publish_commands_t publish_commands,
                    void *adapter_context);

/**
 * Connects this program host to the instance-global virtual-point store.
 * @param host Non-NULL initialized program host that is not concurrently scanning.
 * @param store Non-NULL store serialized by the same controller runtime task.
 * @param execution_instance_id Stable identity equal to the store identity.
 * @param deployment_id Active deployment identity that owns this program's writer leases.
 * @return true when identities are bounded and the instance matches; false leaves virtual routing disabled.
 */
bool flow_host_set_virtual_points(flow_host_t *host, flow_virtual_point_store_t *store,
                                  const char *execution_instance_id, const char *deployment_id);

/* Prepares an active committed generation before atomically replacing the running VM. */
bool flow_host_synchronize(flow_host_t *host, const controller_flow_t *deployment);

/* Runs one complete PLC scan and publishes commands only after successful VM commit. */
bool flow_host_scan(flow_host_t *host, uint64_t now_ms, flow_vm_snapshot_t *snapshot);

/* Stops execution and clears both bounded VM instances. */
void flow_host_stop(flow_host_t *host);
