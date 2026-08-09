#include <assert.h>
#include <limits.h>
#include <stdio.h>
#include <string.h>

#include "controller/io.h"

static const char TEST_SUCCESS_MESSAGE[] = "Controller I/O tests passed";
static uint16_t written_outputs;

/* Captures one logical output bitmap without platform hardware. */
static bool write_outputs(uint16_t outputs)
{
    written_outputs = outputs;

    return true;
}

/* Verifies all 32 stable point definitions use input-first ordering and digital types. */
static void test_point_definitions(void)
{
    controller_io_t io;
    controller_io_init(&io);
    const controller_protocol_point_provider_t provider = controller_io_get_point_provider(&io);
    size_t count                                        = 0;
    assert(provider.get_count(provider.context, &count) == CONTROLLER_PROTOCOL_PROVIDER_OK);
    assert(count == CONTROLLER_IO_POINT_COUNT);
    controller_protocol_point_definition_t definition;
    assert(provider.get_definition(provider.context, 0, &definition) == CONTROLLER_PROTOCOL_PROVIDER_OK);
    assert(strcmp(definition.id, "input-01") == 0 && definition.type == CONTROLLER_PROTOCOL_POINT_DIGITAL);
    assert(provider.get_definition(provider.context, 31, &definition) == CONTROLLER_PROTOCOL_PROVIDER_OK);
    assert(strcmp(definition.id, "output-16") == 0 && definition.type == CONTROLLER_PROTOCOL_POINT_DIGITAL);
}

/* Verifies individual and block reads share one logical active-high cached sample. */
static void test_cached_values(void)
{
    controller_io_t io;
    controller_io_init(&io);
    controller_io_update(&io, UINT16_C(0x8001), true, UINT16_C(0x4002), true, 1234);
    const controller_protocol_point_provider_t provider = controller_io_get_point_provider(&io);
    controller_protocol_point_value_t value             = {0};
    assert(provider.get_value(provider.context, "input-01", &value) == CONTROLLER_PROTOCOL_PROVIDER_OK);
    assert(value.value.digital && value.source_timestamp_ms == INT64_MIN);
    assert(provider.get_value(provider.context, "output-02", &value) == CONTROLLER_PROTOCOL_PROVIDER_OK);
    assert(value.value.digital);
    controller_protocol_io_block_t block;
    assert(controller_io_get_protocol_block(&io, &block) == CONTROLLER_PROTOCOL_PROVIDER_OK);
    assert(block.inputs == UINT16_C(0x8001) && block.outputs == UINT16_C(0x4002));
    assert(block.validity_flags == 3 && block.sequence == 1);
}

/* Verifies unavailable hardware samples cannot be mistaken for inactive points. */
static void test_unavailable_values(void)
{
    controller_io_t io;
    controller_io_init(&io);
    const controller_protocol_point_provider_t provider = controller_io_get_point_provider(&io);
    controller_protocol_point_value_t value;
    controller_protocol_io_block_t block;
    assert(provider.get_value(provider.context, "input-01", &value) == CONTROLLER_PROTOCOL_PROVIDER_NOT_READY);
    assert(controller_io_get_protocol_block(&io, &block) == CONTROLLER_PROTOCOL_PROVIDER_NOT_READY);
    assert(provider.get_value(provider.context, "input-17", &value) == CONTROLLER_PROTOCOL_PROVIDER_NOT_FOUND);
}

/* Verifies single writes preserve other channels and block writes replace all channels. */
static void test_output_writes(void)
{
    controller_io_t io;
    controller_io_init(&io);
    controller_io_set_writer(&io, write_outputs);
    controller_io_update(&io, 0, true, UINT16_C(0x0002), true, 0);
    assert(controller_io_set_protocol_output(&io, "output-01", true) == CONTROLLER_PROTOCOL_PROVIDER_OK);
    assert(written_outputs == UINT16_C(0x0003));
    assert(controller_io_set_protocol_output_block(&io, UINT16_C(0xa55a)) == CONTROLLER_PROTOCOL_PROVIDER_OK);
    assert(written_outputs == UINT16_C(0xa55a));
    assert(controller_io_set_protocol_output(&io, "input-01", true) == CONTROLLER_PROTOCOL_PROVIDER_NOT_FOUND);
}

/* Runs portable digital I/O provider tests and returns success. */
int main(void)
{
    test_point_definitions();
    test_cached_values();
    test_unavailable_values();
    test_output_writes();
    puts(TEST_SUCCESS_MESSAGE);

    return 0;
}
