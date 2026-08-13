#include "flow/host.h"

#include <string.h>

static const flow_vm_target_t TARGET = {.abi_version            = FLOW_VM_ABI_VERSION,
                                        .capabilities           = UINT64_C(0x1f),
                                        .maximum_artifact_bytes = FLOW_VM_MAX_ARTIFACT,
                                        .maximum_work_per_scan  = FLOW_VM_MAX_INSTRUCTIONS,
                                        .maximum_snapshot_bytes = sizeof(flow_vm_snapshot_t)};

/* Initializes caller-owned host state without preparing an artifact. */
bool flow_host_init(flow_host_t *host, flow_host_read_inputs_t read_inputs, flow_host_publish_commands_t publish_commands,
                    void *adapter_context)
{
    if (host == NULL || read_inputs == NULL || publish_commands == NULL)
    {
        return false;
    }

    memset(host, 0, sizeof(*host));
    host->read_inputs      = read_inputs;
    host->publish_commands = publish_commands;
    host->adapter_context  = adapter_context;

    return true;
}

/* Prepares into the inactive slot so malformed replacements cannot disturb production. */
bool flow_host_synchronize(flow_host_t *host, const controller_flow_t *deployment)
{
    if (host == NULL || deployment == NULL)
    {
        return false;
    }

    if (!deployment->has_committed || !deployment->committed.is_active)
    {
        flow_host_stop(host);

        return true;
    }

    if (host->is_running && host->active_revision == deployment->committed.revision)
    {
        return true;
    }

    const uint8_t replacement = (uint8_t)(host->active_instance ^ 1U);
    flow_vm_clear(&host->instances[replacement]);
    host->last_result =
        flow_vm_prepare(deployment->committed_artifact, deployment->committed.size, &TARGET, &host->instances[replacement]);

    if (host->last_result.code != FLOW_VM_OK)
    {
        return false;
    }

    host->last_result = flow_vm_initialize(&host->instances[replacement], NULL, 0);

    if (host->last_result.code != FLOW_VM_OK)
    {
        flow_vm_clear(&host->instances[replacement]);

        return false;
    }

    flow_vm_clear(&host->instances[host->active_instance]);
    host->active_instance = replacement;
    host->active_revision = deployment->committed.revision;
    host->is_running      = true;

    return true;
}

/* Captures coherent inputs, executes the VM, then publishes the committed batch. */
bool flow_host_scan(flow_host_t *host, uint64_t now_ms, flow_vm_snapshot_t *snapshot)
{
    flow_vm_input_sample_t samples[FLOW_VM_MAX_POINTS];
    flow_vm_command_t commands[FLOW_VM_MAX_OUTPUTS];
    size_t sample_count  = 0;
    size_t command_count = 0;
    uint64_t sampled_at_ms;

    if (host == NULL || snapshot == NULL || !host->is_running ||
        !host->read_inputs(host->adapter_context, samples, FLOW_VM_MAX_POINTS, &sample_count, &sampled_at_ms))
    {
        return false;
    }

    const flow_vm_input_frame_t frame = {
        .samples = samples, .sample_count = sample_count, .sampled_at_ms = sampled_at_ms, .is_coherent = true};
    flow_vm_t *vm     = &host->instances[host->active_instance];
    host->last_result = flow_vm_begin_tick(vm, &frame);

    if (host->last_result.code != FLOW_VM_OK)
    {
        return false;
    }

    host->last_result = flow_vm_commit_tick(vm, commands, FLOW_VM_MAX_OUTPUTS, &command_count, snapshot);

    if (host->last_result.code != FLOW_VM_OK)
    {
        flow_vm_abort_tick(vm);

        return false;
    }

    return host->publish_commands(host->adapter_context, commands, command_count, now_ms);
}

/* Clears all execution state and leaves the host safe and inactive. */
void flow_host_stop(flow_host_t *host)
{
    if (host == NULL)
    {
        return;
    }

    flow_vm_clear(&host->instances[0]);
    flow_vm_clear(&host->instances[1]);
    host->is_running      = false;
    host->active_revision = 0;
}
