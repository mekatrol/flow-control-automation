#pragma once

#include <stdbool.h>
#include <stddef.h>
#include <stdint.h>

#include "controller/protocol.h"

/* Fixed board point counts define the version-one discrete I/O contract. */
enum
{
    CONTROLLER_IO_INPUT_COUNT  = 16,
    CONTROLLER_IO_OUTPUT_COUNT = 16,
    CONTROLLER_IO_POINT_COUNT  = 32,
};

typedef struct
{
    uint16_t inputs;
    uint16_t outputs;
    bool are_inputs_valid;
    bool are_outputs_valid;
    int64_t sampled_at_ms;
    uint32_t sequence;
} controller_io_snapshot_t;

typedef struct
{
    controller_io_snapshot_t snapshot;
    bool (*write_outputs)(uint16_t outputs);
} controller_io_t;

/* Initializes a digital I/O cache with explicitly unavailable field data. */
void controller_io_init(controller_io_t *io);

/* Enables bounded physical output writes through the platform-owned board driver. */
void controller_io_set_writer(controller_io_t *io, bool (*write_outputs)(uint16_t outputs));

/* Replaces the complete cached input and output sample after one bounded hardware poll. */
void controller_io_update(controller_io_t *io, uint16_t inputs, bool are_inputs_valid, uint16_t outputs, bool are_outputs_valid,
                          int64_t sampled_at_ms);

/* Gets one coherent copy of all sixteen inputs and outputs. */
controller_io_snapshot_t controller_io_get_snapshot(const controller_io_t *io);

/* Gets a protocol point provider backed only by the non-blocking I/O cache. */
controller_protocol_point_provider_t controller_io_get_point_provider(controller_io_t *io);

/* Copies the complete cached digital I/O state into the protocol block contract. */
controller_protocol_provider_result_t controller_io_get_protocol_block(void *context, controller_protocol_io_block_t *block);

/* Writes one named output while preserving every other cached output state. */
controller_protocol_provider_result_t controller_io_set_protocol_output(void *context, const char *point_id, bool value);

/* Writes all sixteen logical output states as one requested block. */
controller_protocol_provider_result_t controller_io_set_protocol_output_block(void *context, uint16_t outputs);
