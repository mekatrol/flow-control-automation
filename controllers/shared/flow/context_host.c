#include "flow/context_host.h"

#include <stdio.h>
#include <string.h>

static const flow_vm_target_t CONTEXT_TARGET = {.abi_version            = FLOW_VM_ABI_VERSION,
                                                .capabilities           = FLOW_VM_CAPABILITIES_ALL,
                                                .maximum_artifact_bytes = FLOW_VM_MAX_ARTIFACT,
                                                .maximum_work_per_scan  = FLOW_VM_MAX_INSTRUCTIONS,
                                                .maximum_snapshot_bytes = FLOW_VM_MAX_SNAPSHOT_BYTES};

/* Checks a non-empty context identity without scanning beyond its fixed public capacity. */
static bool is_context_identity_valid(const char *identity)
{
    return identity != NULL && identity[0] != '\0' && memchr(identity, '\0', FLOW_VIRTUAL_POINT_ID_CAPACITY) != NULL;
}

/* Builds the distinct writer lease identity for one program within a context deployment. */
static bool get_program_owner(const flow_context_host_t *context, const char *program_id,
                              char owner[FLOW_VIRTUAL_POINT_ID_CAPACITY])
{
    const int size = snprintf(owner, FLOW_VIRTUAL_POINT_ID_CAPACITY, "%s/%s", context->deployment_id, program_id);

    return size > 0 && size < FLOW_VIRTUAL_POINT_ID_CAPACITY;
}

/* Merges one prepared VM's virtual bindings into bounded declarations for ownership validation. */
static bool get_program_declarations(const flow_vm_t *vm, flow_virtual_point_declaration_t *declarations, size_t *count)
{
    *count = 0U;

    for (size_t point_index = 0; point_index < vm->point_count; point_index++)
    {
        const flow_vm_point_t *point = &vm->points[point_index];

        if (point->binding_kind != 1U)
        {
            continue;
        }

        size_t declaration_index = 0U;

        while (declaration_index < *count && strcmp(declarations[declaration_index].key, point->id) != 0)
        {
            declaration_index++;
        }

        if (declaration_index == *count)
        {
            if (*count >= FLOW_VIRTUAL_POINT_CAPACITY)
            {
                return false;
            }

            declarations[declaration_index] = (flow_virtual_point_declaration_t){
                .type        = (flow_virtual_point_type_t)point->type,
                .persistence = FLOW_VIRTUAL_POINT_VOLATILE,
            };
            snprintf(declarations[declaration_index].key, sizeof(declarations[declaration_index].key), "%s", point->id);
            (*count)++;
        }

        declarations[declaration_index].is_writer |= point->direction == 2U;
    }

    return true;
}

/* Initializes bounded context identity and adapters without starting any program. */
bool flow_context_host_init(flow_context_host_t *context, flow_virtual_point_store_t *virtual_points,
                            const char *execution_instance_id, const char *deployment_id, flow_host_read_inputs_t read_inputs,
                            flow_host_publish_commands_t publish_commands, void *adapter_context)
{
    if (context == NULL || virtual_points == NULL || !is_context_identity_valid(execution_instance_id) ||
        !is_context_identity_valid(deployment_id) || read_inputs == NULL || publish_commands == NULL ||
        strcmp(virtual_points->execution_instance_id, execution_instance_id) != 0)
    {
        return false;
    }

    memset(context, 0, sizeof(*context));
    context->virtual_points   = virtual_points;
    context->read_inputs      = read_inputs;
    context->publish_commands = publish_commands;
    context->adapter_context  = adapter_context;
    snprintf(context->execution_instance_id, sizeof(context->execution_instance_id), "%s", execution_instance_id);
    snprintf(context->deployment_id, sizeof(context->deployment_id), "%s", deployment_id);

    return true;
}

/* Prepares every inactive VM and validates all leases against a copied store before publishing any program or contract. */
bool flow_context_host_load(flow_context_host_t *context, const flow_context_program_t *programs, size_t program_count)
{
    if (context == NULL || programs == NULL || program_count == 0U || program_count > FLOW_CONTEXT_MAX_PROGRAMS)
    {
        return false;
    }

    flow_virtual_point_store_t proposed_store = *context->virtual_points;
    uint8_t replacements[FLOW_CONTEXT_MAX_PROGRAMS];

    for (size_t program_index = 0; program_index < context->program_count; program_index++)
    {
        flow_virtual_points_deactivate(&proposed_store, context->execution_instance_id,
                                       context->programs[program_index].deployment_id);
    }

    for (size_t program_index = 0; program_index < program_count; program_index++)
    {
        const flow_context_program_t *program = &programs[program_index];

        if (!is_context_identity_valid(program->program_id) || program->revision == 0U || program->artifact == NULL ||
            program->artifact_size == 0U || program->artifact_size > FLOW_VM_MAX_ARTIFACT)
        {
            return false;
        }

        for (size_t previous = 0; previous < program_index; previous++)
        {
            if (strcmp(programs[previous].program_id, program->program_id) == 0)
            {
                return false;
            }
        }

        flow_host_t *host = &context->programs[program_index];

        if (host->read_inputs == NULL &&
            !flow_host_init(host, context->read_inputs, context->publish_commands, context->adapter_context))
        {
            return false;
        }

        const uint8_t replacement   = (uint8_t)(host->active_instance ^ 1U);
        replacements[program_index] = replacement;
        flow_vm_clear(&host->instances[replacement]);
        host->last_result =
            flow_vm_prepare(program->artifact, program->artifact_size, &CONTEXT_TARGET, &host->instances[replacement]);

        if (host->last_result.code != FLOW_VM_OK)
        {
            return false;
        }

        host->last_result = flow_vm_initialize(&host->instances[replacement], NULL, 0U);

        if (host->last_result.code != FLOW_VM_OK)
        {
            return false;
        }

        char owner[FLOW_VIRTUAL_POINT_ID_CAPACITY];
        flow_virtual_point_declaration_t declarations[FLOW_VIRTUAL_POINT_CAPACITY];
        size_t declaration_count = 0U;

        if (!get_program_owner(context, program->program_id, owner) ||
            !get_program_declarations(&host->instances[replacement], declarations, &declaration_count) ||
            flow_virtual_points_activate(&proposed_store, context->execution_instance_id, owner, declarations,
                                         declaration_count) != FLOW_VIRTUAL_POINT_OK)
        {
            return false;
        }
    }

    for (size_t program_index = 0; program_index < program_count; program_index++)
    {
        flow_host_t *host = &context->programs[program_index];
        flow_vm_clear(&host->instances[host->active_instance]);
        host->active_instance = replacements[program_index];
        host->active_revision = programs[program_index].revision;
        host->is_running      = true;
        char owner[FLOW_VIRTUAL_POINT_ID_CAPACITY];
        get_program_owner(context, programs[program_index].program_id, owner);
        flow_host_set_virtual_points(host, context->virtual_points, context->execution_instance_id, owner);
    }

    for (size_t program_index = program_count; program_index < context->program_count; program_index++)
    {
        flow_host_stop(&context->programs[program_index]);
    }

    *context->virtual_points = proposed_store;
    context->program_count   = program_count;

    return true;
}

/* Uses one copied store for every read while each successful program commits outputs to the live store. */
bool flow_context_host_scan(flow_context_host_t *context, uint64_t now_ms, flow_vm_snapshot_t *snapshots)
{
    if (context == NULL || snapshots == NULL || context->program_count == 0U)
    {
        return false;
    }

    const flow_virtual_point_store_t snapshot_store = *context->virtual_points;

    for (size_t program_index = 0; program_index < context->program_count; program_index++)
    {
        context->programs[program_index].virtual_point_snapshot_source = &snapshot_store;

        if (!flow_host_scan(&context->programs[program_index], now_ms, &snapshots[program_index]))
        {
            context->programs[program_index].virtual_point_snapshot_source = context->virtual_points;

            return false;
        }

        context->programs[program_index].virtual_point_snapshot_source = context->virtual_points;
    }

    return true;
}

/* Releases every program-scoped writer identity and clears all VM execution state. */
void flow_context_host_stop(flow_context_host_t *context)
{
    if (context == NULL)
    {
        return;
    }

    for (size_t program_index = 0; program_index < context->program_count; program_index++)
    {
        flow_virtual_points_deactivate(context->virtual_points, context->execution_instance_id,
                                       context->programs[program_index].deployment_id);
        flow_host_stop(&context->programs[program_index]);
    }

    context->program_count = 0U;
}
