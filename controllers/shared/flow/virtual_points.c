#include "flow/virtual_points.h"

#include <math.h>
#include <stdio.h>
#include <string.h>

/* The retained wire image is fixed-width and copied field-by-field to avoid platform padding. */
enum
{
    RETAINED_IMAGE_VERSION = 1,
    RETAINED_HEADER_SIZE = 8,
    RETAINED_ENTRY_SIZE = FLOW_VIRTUAL_POINT_ID_CAPACITY + 1 + 8 + 8 + 8,
};

static const uint8_t RETAINED_MAGIC[4] = {'F', 'V', 'P', 'S'};

/* Checks a required bounded identity without scanning beyond its public capacity. */
static bool is_identity_valid(const char *identity)
{
    if (identity == NULL || identity[0] == '\0')
    {
        return false;
    }

    return memchr(identity, '\0', FLOW_VIRTUAL_POINT_ID_CAPACITY) != NULL;
}

/* Checks that a declaration has supported type, persistence, bounded key, and finite analog default. */
static bool is_declaration_valid(const flow_virtual_point_declaration_t *declaration)
{
    if (declaration == NULL || !is_identity_valid(declaration->key) ||
        (declaration->type != FLOW_VIRTUAL_POINT_DIGITAL && declaration->type != FLOW_VIRTUAL_POINT_ANALOG) ||
        (declaration->persistence != FLOW_VIRTUAL_POINT_VOLATILE &&
         declaration->persistence != FLOW_VIRTUAL_POINT_RETAINED))
    {
        return false;
    }

    return declaration->type != FLOW_VIRTUAL_POINT_ANALOG || !declaration->has_default ||
           isfinite(declaration->analog_default);
}

/* Gets the allocated cell index for one exact key, or capacity when absent. */
static size_t get_cell_index(const flow_virtual_point_store_t *store, const char *key)
{
    for (size_t index = 0; index < FLOW_VIRTUAL_POINT_CAPACITY; index++)
    {
        if (store->cells[index].is_used && strcmp(store->cells[index].declaration.key, key) == 0)
        {
            return index;
        }
    }

    return FLOW_VIRTUAL_POINT_CAPACITY;
}

/* Gets the first unused cell index, or capacity when fixed storage is exhausted. */
static size_t get_free_cell_index(const flow_virtual_point_store_t *store)
{
    for (size_t index = 0; index < FLOW_VIRTUAL_POINT_CAPACITY; index++)
    {
        if (!store->cells[index].is_used)
        {
            return index;
        }
    }

    return FLOW_VIRTUAL_POINT_CAPACITY;
}

/* Tests immutable contract fields; readable capability is enforced by artifact resolution before allocation. */
static bool is_contract_compatible(const flow_virtual_point_declaration_t *left,
                                   const flow_virtual_point_declaration_t *right)
{
    if (left->type != right->type || left->persistence != right->persistence ||
        left->has_default != right->has_default)
    {
        return false;
    }

    if (!left->has_default)
    {
        return true;
    }

    return left->type == FLOW_VIRTUAL_POINT_DIGITAL ? left->digital_default == right->digital_default
                                                    : left->analog_default == right->analog_default;
}

/* Checks that one request identity targets this concrete controller. */
static bool is_instance_match(const flow_virtual_point_store_t *store, const char *execution_instance_id)
{
    return is_identity_valid(execution_instance_id) &&
           strcmp(store->execution_instance_id, execution_instance_id) == 0;
}

/* Writes an unsigned 64-bit field in little-endian wire order. */
static void write_u64(uint8_t *output, uint64_t value)
{
    for (size_t index = 0; index < sizeof(value); index++)
    {
        output[index] = (uint8_t)(value >> (index * 8U));
    }
}

/* Gets an unsigned 64-bit field from little-endian wire order. */
static uint64_t get_u64(const uint8_t *input)
{
    uint64_t value = 0;

    for (size_t index = 0; index < sizeof(value); index++)
    {
        value |= (uint64_t)input[index] << (index * 8U);
    }

    return value;
}

/* Initializes a newly allocated cell from its typed declaration default. */
static void initialize_cell(flow_virtual_point_cell_t *cell, const flow_virtual_point_declaration_t *declaration)
{
    *cell = (flow_virtual_point_cell_t){.declaration = *declaration, .is_used = true};
    snprintf(cell->value.key, sizeof(cell->value.key), "%s", declaration->key);
    cell->value.type = declaration->type;
    cell->value.is_initialized = declaration->has_default;

    if (declaration->type == FLOW_VIRTUAL_POINT_DIGITAL)
    {
        cell->value.digital_value = declaration->digital_default;
    }
    else
    {
        cell->value.analog_value = declaration->analog_default;
    }
}

/* Initializes empty bounded state after validating the concrete instance identity and protocol version. */
bool flow_virtual_points_init(flow_virtual_point_store_t *store, const char *execution_instance_id,
                              uint32_t protocol_version)
{
    if (store == NULL)
    {
        return false;
    }

    memset(store, 0, sizeof(*store));

    if (!is_identity_valid(execution_instance_id) || protocol_version == 0)
    {
        return false;
    }

    snprintf(store->execution_instance_id, sizeof(store->execution_instance_id), "%s", execution_instance_id);
    store->protocol_version = protocol_version;

    return true;
}

/* Validates the complete activation against a temporary copy so conflicts cannot partially allocate cells or writer leases. */
flow_virtual_point_result_t flow_virtual_points_activate(flow_virtual_point_store_t *store,
                                                         const char *execution_instance_id, const char *deployment_id,
                                                         const flow_virtual_point_declaration_t *declarations,
                                                         size_t declaration_count)
{
    if (store == NULL || !is_identity_valid(deployment_id) || declaration_count > FLOW_VIRTUAL_POINT_CAPACITY ||
        (declarations == NULL && declaration_count != 0))
    {
        return FLOW_VIRTUAL_POINT_INVALID_ARGUMENT;
    }

    if (!is_instance_match(store, execution_instance_id))
    {
        return FLOW_VIRTUAL_POINT_INSTANCE_MISMATCH;
    }

    flow_virtual_point_store_t proposed = *store;

    for (size_t declaration_index = 0; declaration_index < declaration_count; declaration_index++)
    {
        const flow_virtual_point_declaration_t *declaration = &declarations[declaration_index];

        if (!is_declaration_valid(declaration))
        {
            return FLOW_VIRTUAL_POINT_INVALID_ARGUMENT;
        }

        size_t cell_index = get_cell_index(&proposed, declaration->key);

        if (cell_index == FLOW_VIRTUAL_POINT_CAPACITY)
        {
            cell_index = get_free_cell_index(&proposed);

            if (cell_index == FLOW_VIRTUAL_POINT_CAPACITY)
            {
                return FLOW_VIRTUAL_POINT_STORAGE_FULL;
            }

            initialize_cell(&proposed.cells[cell_index], declaration);
        }
        else if (!is_contract_compatible(&proposed.cells[cell_index].declaration, declaration))
        {
            return FLOW_VIRTUAL_POINT_CONTRACT_CONFLICT;
        }

        flow_virtual_point_cell_t *cell = &proposed.cells[cell_index];

        if (declaration->is_writer && cell->writer_deployment_id[0] != '\0' &&
            strcmp(cell->writer_deployment_id, deployment_id) != 0)
        {
            return FLOW_VIRTUAL_POINT_WRITER_CONFLICT;
        }

        if (declaration->is_writer)
        {
            snprintf(cell->writer_deployment_id, sizeof(cell->writer_deployment_id), "%s", deployment_id);
        }
    }

    *store = proposed;

    return FLOW_VIRTUAL_POINT_OK;
}

/* Releases only matching writer leases so readers and committed shared values survive undeployment. */
flow_virtual_point_result_t flow_virtual_points_deactivate(flow_virtual_point_store_t *store,
                                                           const char *execution_instance_id,
                                                           const char *deployment_id)
{
    if (store == NULL || !is_identity_valid(deployment_id))
    {
        return FLOW_VIRTUAL_POINT_INVALID_ARGUMENT;
    }

    if (!is_instance_match(store, execution_instance_id))
    {
        return FLOW_VIRTUAL_POINT_INSTANCE_MISMATCH;
    }

    for (size_t index = 0; index < FLOW_VIRTUAL_POINT_CAPACITY; index++)
    {
        if (strcmp(store->cells[index].writer_deployment_id, deployment_id) == 0)
        {
            store->cells[index].writer_deployment_id[0] = '\0';
        }
    }

    return FLOW_VIRTUAL_POINT_OK;
}

/* Copies each complete logical record while the single-owner runtime task excludes concurrent commits. */
flow_virtual_point_result_t flow_virtual_points_snapshot(const flow_virtual_point_store_t *store, const char *const *keys,
                                                         size_t key_count, flow_virtual_point_snapshot_t *output)
{
    if (store == NULL || key_count > FLOW_VIRTUAL_POINT_CAPACITY ||
        ((keys == NULL || output == NULL) && key_count != 0))
    {
        return FLOW_VIRTUAL_POINT_INVALID_ARGUMENT;
    }

    for (size_t index = 0; index < key_count; index++)
    {
        if (!is_identity_valid(keys[index]))
        {
            return FLOW_VIRTUAL_POINT_INVALID_ARGUMENT;
        }

        const size_t cell_index = get_cell_index(store, keys[index]);

        if (cell_index == FLOW_VIRTUAL_POINT_CAPACITY)
        {
            return FLOW_VIRTUAL_POINT_NOT_FOUND;
        }

        output[index] = store->cells[cell_index].value;
    }

    return FLOW_VIRTUAL_POINT_OK;
}

/* Validates identity, uniqueness, ownership, type, and analog finiteness before changing any shared cell. */
flow_virtual_point_result_t flow_virtual_points_commit(flow_virtual_point_store_t *store,
                                                       const char *execution_instance_id, const char *deployment_id,
                                                       const flow_virtual_point_command_t *commands, size_t command_count,
                                                       uint64_t timestamp_ms)
{
    if (store == NULL || !is_identity_valid(deployment_id) || command_count > FLOW_VIRTUAL_POINT_COMMAND_CAPACITY ||
        (commands == NULL && command_count != 0))
    {
        return FLOW_VIRTUAL_POINT_INVALID_ARGUMENT;
    }

    if (!is_instance_match(store, execution_instance_id))
    {
        return FLOW_VIRTUAL_POINT_INSTANCE_MISMATCH;
    }

    size_t cell_indices[FLOW_VIRTUAL_POINT_COMMAND_CAPACITY];

    for (size_t command_index = 0; command_index < command_count; command_index++)
    {
        const flow_virtual_point_command_t *command = &commands[command_index];

        if (!is_identity_valid(command->key) ||
            (command->type == FLOW_VIRTUAL_POINT_ANALOG && !isfinite(command->analog_value)))
        {
            return FLOW_VIRTUAL_POINT_INVALID_ARGUMENT;
        }

        const size_t cell_index = get_cell_index(store, command->key);

        if (cell_index == FLOW_VIRTUAL_POINT_CAPACITY)
        {
            return FLOW_VIRTUAL_POINT_NOT_FOUND;
        }

        const flow_virtual_point_cell_t *cell = &store->cells[cell_index];

        if (cell->declaration.type != command->type)
        {
            return FLOW_VIRTUAL_POINT_CONTRACT_CONFLICT;
        }

        if (strcmp(cell->writer_deployment_id, deployment_id) != 0)
        {
            return FLOW_VIRTUAL_POINT_WRITER_CONFLICT;
        }

        for (size_t previous = 0; previous < command_index; previous++)
        {
            if (cell_indices[previous] == cell_index)
            {
                return FLOW_VIRTUAL_POINT_INVALID_ARGUMENT;
            }
        }

        cell_indices[command_index] = cell_index;
    }

    for (size_t command_index = 0; command_index < command_count; command_index++)
    {
        flow_virtual_point_cell_t *cell = &store->cells[cell_indices[command_index]];
        const flow_virtual_point_command_t *command = &commands[command_index];
        cell->value.digital_value = command->digital_value;
        cell->value.analog_value = command->analog_value;
        cell->value.is_initialized = true;
        cell->value.timestamp_ms = timestamp_ms;
        cell->value.version++;
    }

    if (command_count != 0)
    {
        store->generation++;
    }

    return FLOW_VIRTUAL_POINT_OK;
}

/* Encodes only initialized retained cells with explicit type, value bits, timestamp, and cell version. */
flow_virtual_point_result_t flow_virtual_points_export_retained(const flow_virtual_point_store_t *store, uint8_t *output,
                                                                size_t capacity, size_t *size)
{
    if (store == NULL || size == NULL || (output == NULL && capacity != 0))
    {
        return FLOW_VIRTUAL_POINT_INVALID_ARGUMENT;
    }

    uint32_t count = 0;

    for (size_t index = 0; index < FLOW_VIRTUAL_POINT_CAPACITY; index++)
    {
        if (store->cells[index].is_used && store->cells[index].declaration.persistence == FLOW_VIRTUAL_POINT_RETAINED &&
            store->cells[index].value.is_initialized)
        {
            count++;
        }
    }

    const size_t required = RETAINED_HEADER_SIZE + (size_t)count * RETAINED_ENTRY_SIZE;
    *size = required;

    if (capacity < required)
    {
        return FLOW_VIRTUAL_POINT_STORAGE_FULL;
    }

    memcpy(output, RETAINED_MAGIC, sizeof(RETAINED_MAGIC));
    output[4] = RETAINED_IMAGE_VERSION;
    output[5] = 0;
    output[6] = 0;
    output[7] = (uint8_t)count;
    size_t offset = RETAINED_HEADER_SIZE;

    for (size_t index = 0; index < FLOW_VIRTUAL_POINT_CAPACITY; index++)
    {
        const flow_virtual_point_cell_t *cell = &store->cells[index];

        if (!cell->is_used || cell->declaration.persistence != FLOW_VIRTUAL_POINT_RETAINED ||
            !cell->value.is_initialized)
        {
            continue;
        }

        memset(output + offset, 0, FLOW_VIRTUAL_POINT_ID_CAPACITY);
        memcpy(output + offset, cell->declaration.key, strlen(cell->declaration.key));
        offset += FLOW_VIRTUAL_POINT_ID_CAPACITY;
        output[offset++] = (uint8_t)cell->declaration.type;
        uint64_t value_bits = cell->value.digital_value ? 1U : 0U;

        if (cell->declaration.type == FLOW_VIRTUAL_POINT_ANALOG)
        {
            memcpy(&value_bits, &cell->value.analog_value, sizeof(value_bits));
        }

        write_u64(output + offset, value_bits);
        offset += sizeof(value_bits);
        write_u64(output + offset, cell->value.timestamp_ms);
        offset += sizeof(cell->value.timestamp_ms);
        write_u64(output + offset, cell->value.version);
        offset += sizeof(cell->value.version);
    }

    return FLOW_VIRTUAL_POINT_OK;
}

/* Validates the entire image against a temporary copy so malformed recovery cannot partially restore controller state. */
flow_virtual_point_result_t flow_virtual_points_restore_retained(flow_virtual_point_store_t *store, const uint8_t *image,
                                                                 size_t size)
{
    if (store == NULL || image == NULL || size < RETAINED_HEADER_SIZE ||
        memcmp(image, RETAINED_MAGIC, sizeof(RETAINED_MAGIC)) != 0 || image[4] != RETAINED_IMAGE_VERSION)
    {
        return FLOW_VIRTUAL_POINT_RETAINED_INCOMPATIBLE;
    }

    const size_t count = image[7];

    if (count > FLOW_VIRTUAL_POINT_CAPACITY || size != RETAINED_HEADER_SIZE + count * RETAINED_ENTRY_SIZE)
    {
        return FLOW_VIRTUAL_POINT_RETAINED_INCOMPATIBLE;
    }

    flow_virtual_point_store_t proposed = *store;
    size_t offset = RETAINED_HEADER_SIZE;

    for (size_t entry = 0; entry < count; entry++)
    {
        const char *key = (const char *)(image + offset);

        if (!is_identity_valid(key))
        {
            return FLOW_VIRTUAL_POINT_RETAINED_INCOMPATIBLE;
        }

        const size_t cell_index = get_cell_index(&proposed, key);
        offset += FLOW_VIRTUAL_POINT_ID_CAPACITY;
        const flow_virtual_point_type_t type = (flow_virtual_point_type_t)image[offset++];
        const uint64_t value_bits = get_u64(image + offset);
        offset += sizeof(value_bits);
        const uint64_t timestamp_ms = get_u64(image + offset);
        offset += sizeof(timestamp_ms);
        const uint64_t version = get_u64(image + offset);
        offset += sizeof(version);

        if (cell_index == FLOW_VIRTUAL_POINT_CAPACITY ||
            proposed.cells[cell_index].declaration.persistence != FLOW_VIRTUAL_POINT_RETAINED ||
            proposed.cells[cell_index].declaration.type != type)
        {
            return FLOW_VIRTUAL_POINT_RETAINED_INCOMPATIBLE;
        }

        flow_virtual_point_snapshot_t *value = &proposed.cells[cell_index].value;
        value->is_initialized = true;
        value->timestamp_ms = timestamp_ms;
        value->version = version;

        if (type == FLOW_VIRTUAL_POINT_DIGITAL)
        {
            if (value_bits > 1U)
            {
                return FLOW_VIRTUAL_POINT_RETAINED_INCOMPATIBLE;
            }

            value->digital_value = value_bits != 0U;
        }
        else
        {
            memcpy(&value->analog_value, &value_bits, sizeof(value->analog_value));

            if (!isfinite(value->analog_value))
            {
                return FLOW_VIRTUAL_POINT_RETAINED_INCOMPATIBLE;
            }
        }
    }

    *store = proposed;

    return FLOW_VIRTUAL_POINT_OK;
}
