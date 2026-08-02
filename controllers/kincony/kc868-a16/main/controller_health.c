#include "controller_health.h"

#include "esp_heap_caps.h"
#include "esp_timer.h"

static int64_t started_us;

void controller_health_init(void)
{
    started_us = esp_timer_get_time();
}

controller_health_snapshot_t controller_health_snapshot(void)
{
    const int64_t elapsed_us = esp_timer_get_time() - started_us;
    return (controller_health_snapshot_t) {
        .uptime_ms = elapsed_us > 0 ? (uint64_t)(elapsed_us / 1000) : 0,
        .free_heap_bytes = heap_caps_get_free_size(MALLOC_CAP_8BIT),
        .wifi_state = "disabled",
        .ethernet_state = "disabled",
        .mqtt_state = "disabled",
        .rs485_state = "disabled",
        .rs485_errors = 0,
        .rs485_queue_drops = 0,
    };
}
