#include <assert.h>
#include <stdio.h>
#include <string.h>

#include "controller_health.h"

/* Fixture values make the formatted health schema expectation explicit. */
enum
{
    OUTPUT_SIZE       = 384,
    UPTIME_MS         = 9876,
    FREE_HEAP_BYTES   = 123456,
    RS485_ERRORS      = 4,
    RS485_QUEUE_DROPS = 2,
    PROTOCOL_ERRORS   = 5,
    PROTOCOL_DROPS    = 1,
    MQTT_RECONNECTS   = 3,
    MQTT_QUEUE_DEPTH  = 1,
};

/* Stable fixture strings represent one state from each controller subsystem. */
static const char STATE_DISABLED[]       = "disabled";
static const char STATE_ONLINE[]         = "online";
static const char STATE_BACKOFF[]        = "backoff";
static const char STATE_STOPPED[]        = "stopped";
static const char ERROR_BROKER[]         = "broker";
static const char LINK_ETHERNET[]        = "ethernet";
static const char STATE_MAIN_MENU[]      = "main_menu";
static const char EXPECTED_HEALTH[]      = "status uptime_ms=9876 free_heap_bytes=123456 wifi=disabled "
                                           "ethernet=online mqtt=backoff mqtt_transport=ethernet mqtt_error=broker "
                                           "mqtt_reconnect_count=3 mqtt_queue_depth=1 rs485=stopped rs485_errors=4 "
                                           "rs485_queue_drops=2 protocol_errors=5 protocol_response_drops=1 "
                                           "terminal=main_menu terminal_sessions=1 "
                                           "terminal_failed_logins=2 terminal_output_drops=3";
static const char TEST_SUCCESS_MESSAGE[] = "Controller health format tests passed";

/* Verifies controller health uses the stable structured status schema. */
static void test_health_formatting(void)
{
    const controller_health_snapshot_t snapshot = {
        .uptime_ms                       = UPTIME_MS,
        .free_heap_bytes                 = FREE_HEAP_BYTES,
        .wifi_state                      = STATE_DISABLED,
        .ethernet_state                  = STATE_ONLINE,
        .mqtt_state                      = STATE_BACKOFF,
        .mqtt_error                      = ERROR_BROKER,
        .mqtt_transport                  = LINK_ETHERNET,
        .mqtt_reconnect_count            = MQTT_RECONNECTS,
        .mqtt_queue_depth                = MQTT_QUEUE_DEPTH,
        .rs485_state                     = STATE_STOPPED,
        .rs485_errors                    = RS485_ERRORS,
        .rs485_queue_drops               = RS485_QUEUE_DROPS,
        .protocol_errors                 = PROTOCOL_ERRORS,
        .protocol_response_drops         = PROTOCOL_DROPS,
        .terminal_state                  = STATE_MAIN_MENU,
        .terminal_authenticated_sessions = 1,
        .terminal_failed_logins          = 2,
        .terminal_output_drops           = 3,
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
