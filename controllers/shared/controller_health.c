#include "controller_health.h"

#include "platform.h"

static uint64_t started_ms;

void controller_health_init(void)
{
    started_ms = platform_monotonic_ms();
}

controller_health_snapshot_t controller_health_snapshot(void)
{
    const uint64_t now_ms = platform_monotonic_ms();
    return (controller_health_snapshot_t) {
        .uptime_ms = now_ms >= started_ms ? now_ms - started_ms : 0,
        .free_heap_bytes = platform_free_heap_bytes(),
        .wifi_state = "disabled",
        .ethernet_state = "disabled",
        .mqtt_state = "disabled",
        .rs485_state = "disabled",
        .rs485_errors = 0,
        .rs485_queue_drops = 0,
    };
}
