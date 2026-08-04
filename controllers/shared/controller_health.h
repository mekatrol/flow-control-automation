#pragma once

#include <stddef.h>
#include <stdint.h>

typedef struct
{
    uint64_t uptime_ms;
    uint64_t free_heap_bytes;
    const char *wifi_state;
    const char *ethernet_state;
    const char *mqtt_state;
    const char *mqtt_error;
    const char *mqtt_transport;
    uint32_t mqtt_reconnect_count;
    size_t mqtt_queue_depth;
    const char *rs485_state;
    uint32_t rs485_errors;
    uint32_t rs485_queue_drops;
    const char *terminal_state;
    uint32_t terminal_authenticated_sessions;
    uint32_t terminal_failed_logins;
    uint32_t terminal_output_drops;
} controller_health_snapshot_t;

/* Initializes health timing before the controller runtime begins work. */
void controller_health_init(void);

/* Gets a point-in-time, read-only snapshot of controller subsystem health. */
controller_health_snapshot_t get_controller_health_snapshot(void);

/* Formats a health snapshot as one bounded structured status line. */
int controller_health_format(char *output, size_t output_size, const controller_health_snapshot_t *snapshot);
