#include "flow/host.h"

#include <stdio.h>
#include <string.h>

static const flow_vm_target_t TARGET = {.abi_version            = FLOW_VM_ABI_VERSION,
                                        .capabilities           = FLOW_VM_CAPABILITIES_ALL,
                                        .maximum_artifact_bytes = FLOW_VM_MAX_ARTIFACT,
                                        .maximum_work_per_scan  = FLOW_VM_MAX_INSTRUCTIONS,
                                        .maximum_snapshot_bytes = FLOW_VM_MAX_SNAPSHOT_BYTES};

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

/* Installs bounded instance and deployment identity used for every shared-point snapshot and commit. */
bool flow_host_set_virtual_points(flow_host_t *host, flow_virtual_point_store_t *store, const char *execution_instance_id,
                                  const char *deployment_id)
{
    if (host == NULL || store == NULL || execution_instance_id == NULL || deployment_id == NULL ||
        execution_instance_id[0] == '\0' || deployment_id[0] == '\0' ||
        memchr(execution_instance_id, '\0', FLOW_VIRTUAL_POINT_ID_CAPACITY) == NULL ||
        memchr(deployment_id, '\0', FLOW_VIRTUAL_POINT_ID_CAPACITY) == NULL ||
        strcmp(store->execution_instance_id, execution_instance_id) != 0)
    {
        return false;
    }

    host->virtual_points                = store;
    host->virtual_point_snapshot_source = store;
    snprintf(host->execution_instance_id, sizeof(host->execution_instance_id), "%s", execution_instance_id);
    snprintf(host->deployment_id, sizeof(host->deployment_id), "%s", deployment_id);

    return true;
}

/* Appends one coherent instance-global snapshot for every virtual binding in the active artifact. */
static bool append_virtual_inputs(flow_host_t *host, flow_vm_t *vm, flow_vm_input_sample_t *samples, size_t *sample_count)
{
    if (host->virtual_points == NULL)
    {
        return true;
    }

    for (size_t point_index = 0; point_index < vm->point_count; point_index++)
    {
        const flow_vm_point_t *point = &vm->points[point_index];

        if (point->binding_kind != 1U)
        {
            continue;
        }

        if (*sample_count >= FLOW_VM_MAX_POINTS)
        {
            return false;
        }

        const char *keys[] = {point->id};
        flow_virtual_point_snapshot_t snapshot;

        if (flow_virtual_points_snapshot(host->virtual_point_snapshot_source, keys, 1U, &snapshot) != FLOW_VIRTUAL_POINT_OK)
        {
            return false;
        }

        flow_vm_input_sample_t *sample = &samples[*sample_count];
        snprintf(sample->point_id, sizeof(sample->point_id), "%s", point->id);
        sample->type         = (uint8_t)snapshot.type;
        sample->binding_kind = 1U;
        sample->quality      = snapshot.is_initialized ? 0U : 3U;
        sample->value        = snapshot.digital_value;
        sample->number       = snapshot.analog_value;
        (*sample_count)++;
    }

    return true;
}

/* Prepares an immutable artifact into the inactive slot and switches only after VM and virtual-contract validation succeed. */
bool flow_host_prepare_artifact(flow_host_t *host, const uint8_t *artifact, size_t artifact_size, uint32_t revision)
{
    if (host == NULL || artifact == NULL || artifact_size == 0U || artifact_size > FLOW_VM_MAX_ARTIFACT || revision == 0U)
    {
        return false;
    }

    if (host->is_running && host->active_revision == revision)
    {
        return true;
    }

    const uint8_t replacement = (uint8_t)(host->active_instance ^ 1U);
    flow_vm_clear(&host->instances[replacement]);
    host->last_result = flow_vm_prepare(artifact, artifact_size, &TARGET, &host->instances[replacement]);

    if (host->last_result.code != FLOW_VM_OK)
    {
        return false;
    }

    host->last_result = flow_vm_initialize(&host->instances[replacement], NULL, 0U);

    if (host->last_result.code != FLOW_VM_OK)
    {
        flow_vm_clear(&host->instances[replacement]);

        return false;
    }

    flow_vm_clear(&host->instances[host->active_instance]);
    host->active_instance = replacement;
    host->active_revision = revision;
    host->is_running      = true;

    return true;
}

/* Splits VM proposals and commits all virtual commands as one instance-global transaction before physical publication. */
static bool publish_host_commands(flow_host_t *host, const flow_vm_command_t *commands, size_t command_count, uint64_t now_ms)
{
    flow_virtual_point_command_t virtual_commands[FLOW_VIRTUAL_POINT_COMMAND_CAPACITY];
    flow_vm_command_t physical_commands[FLOW_VM_MAX_OUTPUTS];
    size_t virtual_count  = 0;
    size_t physical_count = 0;

    for (size_t index = 0; index < command_count; index++)
    {
        if (commands[index].binding_kind == 1U)
        {
            if (host->virtual_points == NULL || virtual_count >= FLOW_VIRTUAL_POINT_COMMAND_CAPACITY)
            {
                return false;
            }

            flow_virtual_point_command_t *command = &virtual_commands[virtual_count++];
            snprintf(command->key, sizeof(command->key), "%s", commands[index].point_id);
            command->type          = (flow_virtual_point_type_t)commands[index].type;
            command->digital_value = commands[index].value;
            command->analog_value  = commands[index].number;
        }

        else
        {
            physical_commands[physical_count++] = commands[index];
        }
    }

    if (virtual_count != 0U && flow_virtual_points_commit(host->virtual_points, host->execution_instance_id, host->deployment_id,
                                                          virtual_commands, virtual_count, now_ms) != FLOW_VIRTUAL_POINT_OK)
    {
        return false;
    }

    return host->publish_commands(host->adapter_context, physical_commands, physical_count, now_ms);
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

    if (host->virtual_points != NULL)
    {
        flow_virtual_point_declaration_t declarations[FLOW_VIRTUAL_POINT_CAPACITY];
        size_t declaration_count = 0;

        for (size_t point_index = 0; point_index < host->instances[replacement].point_count; point_index++)
        {
            const flow_vm_point_t *point = &host->instances[replacement].points[point_index];

            if (point->binding_kind != 1U)
            {
                continue;
            }

            size_t declaration_index = 0;

            while (declaration_index < declaration_count && strcmp(declarations[declaration_index].key, point->id) != 0)
            {
                declaration_index++;
            }

            if (declaration_index == declaration_count)
            {
                if (declaration_count >= FLOW_VIRTUAL_POINT_CAPACITY)
                {
                    flow_vm_clear(&host->instances[replacement]);

                    return false;
                }

                declarations[declaration_index] = (flow_virtual_point_declaration_t){
                    .type        = (flow_virtual_point_type_t)point->type,
                    .persistence = FLOW_VIRTUAL_POINT_VOLATILE,
                };
                snprintf(declarations[declaration_index].key, sizeof(declarations[declaration_index].key), "%s", point->id);
                declaration_count++;
            }

            /* Direction two is the canonical output binding encoded by Flow IL. */
            declarations[declaration_index].is_writer |= point->direction == 2U;
        }

        if (flow_virtual_points_activate(host->virtual_points, host->execution_instance_id, host->deployment_id, declarations,
                                         declaration_count) != FLOW_VIRTUAL_POINT_OK)
        {
            flow_vm_clear(&host->instances[replacement]);

            return false;
        }
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

    flow_vm_t *vm = &host->instances[host->active_instance];

    if (!append_virtual_inputs(host, vm, samples, &sample_count))
    {
        return false;
    }

    const flow_vm_input_frame_t frame = {
        .samples = samples, .sample_count = sample_count, .sampled_at_ms = sampled_at_ms, .is_coherent = true};
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

    return publish_host_commands(host, commands, command_count, now_ms);
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
