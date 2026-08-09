#include "controller/io.h"

#include <stdio.h>
#include <string.h>

/* Stable point metadata makes field I/O discoverable without numeric register aliases. */
enum
{
    POINT_REVISION       = 1,
    INPUT_SERVICE_FLAGS  = 0x07,
    OUTPUT_SERVICE_FLAGS = 0x07,
};

static const char INPUT_ID_FORMAT[]  = "input-%02u";
static const char OUTPUT_ID_FORMAT[] = "output-%02u";

/* Initializes a digital I/O cache with explicitly unavailable field data. */
void controller_io_init(controller_io_t *io)
{
    if (io != NULL)
    {
        *io = (controller_io_t){0};
    }
}

/* Enables bounded physical output writes through the platform-owned board driver. */
void controller_io_set_writer(controller_io_t *io, bool (*write_outputs)(uint16_t outputs))
{
    if (io != NULL)
    {
        io->write_outputs = write_outputs;
    }
}

/* Replaces the complete cached input and output sample after one bounded hardware poll. */
void controller_io_update(controller_io_t *io, uint16_t inputs, bool are_inputs_valid, uint16_t outputs, bool are_outputs_valid,
                          int64_t sampled_at_ms)
{
    if (io == NULL)
    {
        return;
    }
    io->snapshot.inputs            = inputs;
    io->snapshot.outputs           = outputs;
    io->snapshot.are_inputs_valid  = are_inputs_valid;
    io->snapshot.are_outputs_valid = are_outputs_valid;
    io->snapshot.sampled_at_ms     = sampled_at_ms;
    io->snapshot.sequence++;
}

/* Gets one coherent copy of all sixteen inputs and outputs. */
controller_io_snapshot_t controller_io_get_snapshot(const controller_io_t *io)
{
    return io != NULL ? io->snapshot : (controller_io_snapshot_t){0};
}

/* Gets the fixed number of board digital points exposed by this provider. */
static controller_protocol_provider_result_t get_point_count(void *context, size_t *count)
{
    if (context == NULL || count == NULL)
    {
        return CONTROLLER_PROTOCOL_PROVIDER_FAILED;
    }
    *count = CONTROLLER_IO_POINT_COUNT;
    return CONTROLLER_PROTOCOL_PROVIDER_OK;
}

/* Builds stable point metadata for one input-first provider index. */
static controller_protocol_provider_result_t get_point_definition(void *context, size_t index,
                                                                  controller_protocol_point_definition_t *definition)
{
    if (context == NULL || definition == NULL || index >= CONTROLLER_IO_POINT_COUNT)
    {
        return CONTROLLER_PROTOCOL_PROVIDER_NOT_FOUND;
    }
    const bool is_input    = index < CONTROLLER_IO_INPUT_COUNT;
    const unsigned channel = (unsigned)(is_input ? index : index - CONTROLLER_IO_INPUT_COUNT) + 1U;
    *definition =
        (controller_protocol_point_definition_t){.revision      = POINT_REVISION,
                                                 .type          = CONTROLLER_PROTOCOL_POINT_DIGITAL,
                                                 .service_flags = is_input ? INPUT_SERVICE_FLAGS : OUTPUT_SERVICE_FLAGS};
    snprintf(definition->id, sizeof(definition->id), is_input ? INPUT_ID_FORMAT : OUTPUT_ID_FORMAT, channel);
    return CONTROLLER_PROTOCOL_PROVIDER_OK;
}

/* Parses a stable point ID and returns its provider index without accepting aliases. */
static bool is_point_id_valid(void *context, const char *point_id, size_t *index)
{
    for (size_t candidate = 0; candidate < CONTROLLER_IO_POINT_COUNT; candidate++)
    {
        controller_protocol_point_definition_t definition;

        if (get_point_definition(context, candidate, &definition) == CONTROLLER_PROTOCOL_PROVIDER_OK &&
            strcmp(point_id, definition.id) == 0)
        {
            *index = candidate;
            return true;
        }
    }
    return false;
}

/* Gets one digital value from the latest coherent hardware sample. */
static controller_protocol_provider_result_t get_point_value(void *context, const char *point_id,
                                                             controller_protocol_point_value_t *value)
{
    controller_io_t *io = context;
    size_t index        = 0;

    if (io == NULL || point_id == NULL || value == NULL || !is_point_id_valid(context, point_id, &index))
    {
        return CONTROLLER_PROTOCOL_PROVIDER_NOT_FOUND;
    }
    const bool is_input = index < CONTROLLER_IO_INPUT_COUNT;

    if ((is_input && !io->snapshot.are_inputs_valid) || (!is_input && !io->snapshot.are_outputs_valid))
    {
        return CONTROLLER_PROTOCOL_PROVIDER_NOT_READY;
    }

    if (get_point_definition(io, index, &value->definition) != CONTROLLER_PROTOCOL_PROVIDER_OK)
    {
        return CONTROLLER_PROTOCOL_PROVIDER_FAILED;
    }
    const size_t bit     = is_input ? index : index - CONTROLLER_IO_INPUT_COUNT;
    value->value.digital = (((is_input ? io->snapshot.inputs : io->snapshot.outputs) >> bit) & 1U) != 0U;
    value->quality       = CONTROLLER_PROTOCOL_QUALITY_GOOD;
    /* The platform currently has no trusted wall clock, so normative timestamps remain explicitly absent. */
    value->source_timestamp_ms = INT64_MIN;
    value->updated_at_ms       = INT64_MIN;
    value->sequence            = io->snapshot.sequence;
    return CONTROLLER_PROTOCOL_PROVIDER_OK;
}

/* Gets a protocol point provider backed only by the non-blocking I/O cache. */
controller_protocol_point_provider_t controller_io_get_point_provider(controller_io_t *io)
{
    return (controller_protocol_point_provider_t){
        .get_count = get_point_count, .get_definition = get_point_definition, .get_value = get_point_value, .context = io};
}

/* Copies the complete cached digital I/O state into the protocol block contract. */
controller_protocol_provider_result_t controller_io_get_protocol_block(void *context, controller_protocol_io_block_t *block)
{
    const controller_io_t *io = context;

    if (io == NULL || block == NULL)
    {
        return CONTROLLER_PROTOCOL_PROVIDER_FAILED;
    }

    if (!io->snapshot.are_inputs_valid && !io->snapshot.are_outputs_valid)
    {
        return CONTROLLER_PROTOCOL_PROVIDER_NOT_READY;
    }
    *block = (controller_protocol_io_block_t){.inputs         = io->snapshot.inputs,
                                              .outputs        = io->snapshot.outputs,
                                              .validity_flags = (io->snapshot.are_inputs_valid ? 1U : 0U) |
                                                                (io->snapshot.are_outputs_valid ? 2U : 0U),
                                              .sampled_at_ms = io->snapshot.sampled_at_ms,
                                              .sequence      = io->snapshot.sequence};
    return CONTROLLER_PROTOCOL_PROVIDER_OK;
}

/* Writes all sixteen logical output states as one requested block. */
controller_protocol_provider_result_t controller_io_set_protocol_output_block(void *context, uint16_t outputs)
{
    controller_io_t *io = context;

    if (io == NULL || io->write_outputs == NULL)
    {
        return CONTROLLER_PROTOCOL_PROVIDER_NOT_READY;
    }

    if (!io->write_outputs(outputs))
    {
        return CONTROLLER_PROTOCOL_PROVIDER_FAILED;
    }
    /* Publish an acknowledged command immediately rather than waiting for the next field poll. */
    io->snapshot.outputs           = outputs;
    io->snapshot.are_outputs_valid = true;
    io->snapshot.sequence++;
    return CONTROLLER_PROTOCOL_PROVIDER_OK;
}

/* Writes one named output while preserving every other cached output state. */
controller_protocol_provider_result_t controller_io_set_protocol_output(void *context, const char *point_id, bool value)
{
    controller_io_t *io = context;
    size_t index        = 0;

    if (io == NULL || point_id == NULL || !is_point_id_valid(context, point_id, &index) || index < CONTROLLER_IO_INPUT_COUNT)
    {
        return CONTROLLER_PROTOCOL_PROVIDER_NOT_FOUND;
    }

    if (!io->snapshot.are_outputs_valid)
    {
        return CONTROLLER_PROTOCOL_PROVIDER_NOT_READY;
    }
    const uint16_t mask    = (uint16_t)(1U << (index - CONTROLLER_IO_INPUT_COUNT));
    const uint16_t outputs = value ? (uint16_t)(io->snapshot.outputs | mask) : (uint16_t)(io->snapshot.outputs & ~mask);
    return controller_io_set_protocol_output_block(context, outputs);
}
