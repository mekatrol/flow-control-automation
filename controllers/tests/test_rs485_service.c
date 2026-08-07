#include <assert.h>
#include <stdio.h>
#include <string.h>

#include "rs485/service.h"

/* Fixture values exercise timeout framing and both bounded queues. */
enum
{
    TEST_TIMEOUT_MS  = 10,
    TEST_FRAME_SIZE  = 4,
    TEST_QUEUE_DEPTH = 2,
};

static uint8_t written_data[RS485_FRAME_CAPACITY];
static size_t written_size;
static const char TEST_SUCCESS_MESSAGE[] = "RS485 service tests passed";

/* Returns one valid raw-mode configuration for focused mutation by validation tests. */
static rs485_config_t get_valid_config(void)
{
    return (rs485_config_t){.enabled              = true,
                            .transmit_gpio        = 16,
                            .receive_gpio         = 17,
                            .baud_rate            = 115200,
                            .data_bits            = RS485_DATA_BITS_8,
                            .parity               = RS485_PARITY_NONE,
                            .stop_bits            = RS485_STOP_BITS_1,
                            .receive_timeout_ms   = TEST_TIMEOUT_MS,
                            .maximum_frame_size   = TEST_FRAME_SIZE,
                            .transmit_queue_depth = TEST_QUEUE_DEPTH,
                            .receive_queue_depth  = TEST_QUEUE_DEPTH,
                            .protocol             = RS485_PROTOCOL_RAW};
}

/* Captures a platform-owned write for ordering assertions. */
static bool write_transport(const uint8_t *data, size_t size)
{
    written_size = size;
    (void)memcpy(written_data, data, size);
    return true;
}

/* Verifies invalid pins, sizes, and queue depths are rejected. */
static void test_configuration_validation(void)
{
    rs485_config_t config = get_valid_config();
    assert(is_rs485_config_valid(&config));
    config.receive_gpio = config.transmit_gpio;
    assert(!is_rs485_config_valid(&config));
    config                    = get_valid_config();
    config.maximum_frame_size = RS485_FRAME_CAPACITY + 1U;
    assert(!is_rs485_config_valid(&config));
    config                     = get_valid_config();
    config.receive_queue_depth = 0;
    assert(!is_rs485_config_valid(&config));
}

/* Verifies an inter-byte timeout creates one owned raw frame and increments its counter. */
static void test_timeout_frame_boundary(void)
{
    rs485_service_t service;
    const rs485_config_t config = get_valid_config();
    const uint8_t first[]       = {1, 2};
    const uint8_t second[]      = {3, 4};
    const uint8_t expected[]    = {1, 2, 3, 4};
    assert(rs485_service_init(&service, &config, write_transport));
    rs485_service_receive_bytes(&service, first, sizeof(first), 1);
    rs485_service_receive_bytes(&service, second, sizeof(second), 2);
    rs485_service_process(&service, 12);
    rs485_frame_t frame;
    assert(rs485_service_get_received(&service, &frame));
    assert(frame.size == TEST_FRAME_SIZE);
    assert(memcmp(frame.data, expected, sizeof(expected)) == 0);
    assert(rs485_service_get_health(&service).timeout_count == 1);
}

/* Verifies oversized partial input is discarded and reported without memory growth. */
static void test_receive_overflow(void)
{
    rs485_service_t service;
    const rs485_config_t config = get_valid_config();
    const uint8_t data[]        = {1, 2, 3, 4, 5};
    assert(rs485_service_init(&service, &config, write_transport));
    rs485_service_receive_bytes(&service, data, sizeof(data), 0);
    assert(rs485_service_get_health(&service).overflow_count == 1);
    rs485_frame_t frame;
    assert(!rs485_service_get_received(&service, &frame));
}

/* Verifies transmit saturation rejects deterministically and retained frames drain in order. */
static void test_transmit_queue_saturation(void)
{
    rs485_service_t service;
    const rs485_config_t config = get_valid_config();
    const uint8_t first[]       = {1};
    const uint8_t second[]      = {2};
    const uint8_t third[]       = {3};
    assert(rs485_service_init(&service, &config, write_transport));
    assert(rs485_service_send(&service, first, sizeof(first)));
    assert(rs485_service_send(&service, second, sizeof(second)));
    assert(!rs485_service_send(&service, third, sizeof(third)));
    assert(rs485_service_get_health(&service).transmit_queue_drop_count == 1);
    rs485_service_process(&service, 0);
    assert(written_size == sizeof(first) && written_data[0] == first[0]);
    rs485_service_process(&service, 1);
    assert(written_size == sizeof(second) && written_data[0] == second[0]);
}

/* Verifies receive saturation retains old frames and counts rejected newest frames. */
static void test_receive_queue_saturation(void)
{
    rs485_service_t service;
    rs485_config_t config      = get_valid_config();
    config.receive_queue_depth = 1;
    const uint8_t first[]      = {1};
    const uint8_t second[]     = {2};
    assert(rs485_service_init(&service, &config, write_transport));
    rs485_service_receive_bytes(&service, first, sizeof(first), 0);
    rs485_service_process(&service, TEST_TIMEOUT_MS);
    rs485_service_receive_bytes(&service, second, sizeof(second), TEST_TIMEOUT_MS + 1U);
    rs485_service_process(&service, (TEST_TIMEOUT_MS * 2U) + 1U);
    assert(rs485_service_get_health(&service).receive_queue_drop_count == 1);
}

/* Verifies UART error categories remain independently observable. */
static void test_error_counters(void)
{
    rs485_service_t service;
    const rs485_config_t config = get_valid_config();
    assert(rs485_service_init(&service, &config, write_transport));
    rs485_service_report_error(&service, RS485_TRANSPORT_ERROR_FRAMING);
    rs485_service_report_error(&service, RS485_TRANSPORT_ERROR_PARITY);
    rs485_service_report_error(&service, RS485_TRANSPORT_ERROR_COLLISION);
    rs485_service_report_error(&service, RS485_TRANSPORT_ERROR_PROTOCOL);
    rs485_service_report_queue_drops(&service, 1);
    const rs485_health_t health = rs485_service_get_health(&service);
    assert(health.framing_error_count == 1 && health.parity_error_count == 1);
    assert(health.collision_count == 1 && health.protocol_error_count == 1);
    assert(health.receive_queue_drop_count == 1);
}

/* Runs all portable RS485 service cases and returns success on completion. */
int main(void)
{
    test_configuration_validation();
    test_timeout_frame_boundary();
    test_receive_overflow();
    test_transmit_queue_saturation();
    test_receive_queue_saturation();
    test_error_counters();
    puts(TEST_SUCCESS_MESSAGE);
    return 0;
}
