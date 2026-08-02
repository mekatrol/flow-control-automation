#include <assert.h>
#include <stdio.h>
#include <string.h>

#include "controller_health.h"

/* Fixture values make the formatted health schema expectation explicit. */
enum {
    OUTPUT_SIZE = 256,
    UPTIME_MS = 9876,
    FREE_HEAP_BYTES = 123456,
    RS485_ERRORS = 4,
    RS485_QUEUE_DROPS = 2,
};

/* Stable fixture strings represent one state from each controller subsystem. */
static const char STATE_DISABLED[] = "disabled";
static const char STATE_ONLINE[] = "online";
static const char STATE_BACKOFF[] = "backoff";
static const char STATE_STOPPED[] = "stopped";
static const char EXPECTED_HEALTH[] =
    "status uptime_ms=9876 free_heap_bytes=123456 wifi=disabled "
    "ethernet=online mqtt=backoff rs485=stopped rs485_errors=4 "
    "rs485_queue_drops=2";
static const char TEST_SUCCESS_MESSAGE[] =
    "Controller health format tests passed";

/* Verifies controller health uses the stable structured status schema. */
static void test_health_formatting(void)
{
    const controller_health_snapshot_t snapshot = {
        .uptime_ms = UPTIME_MS,
        .free_heap_bytes = FREE_HEAP_BYTES,
        .wifi_state = STATE_DISABLED,
        .ethernet_state = STATE_ONLINE,
        .mqtt_state = STATE_BACKOFF,
        .rs485_state = STATE_STOPPED,
        .rs485_errors = RS485_ERRORS,
        .rs485_queue_drops = RS485_QUEUE_DROPS,
    };
    char output[OUTPUT_SIZE];
    assert(controller_health_format(output, sizeof(output), &snapshot) > 0);
    assert(strcmp(output, EXPECTED_HEALTH) == 0);
}

/* Runs the controller health formatter case and returns success on completion. */
int main(void)
{
    test_health_formatting();
    puts(TEST_SUCCESS_MESSAGE);
    return 0;
}
