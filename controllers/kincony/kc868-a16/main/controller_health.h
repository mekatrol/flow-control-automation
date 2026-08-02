#pragma once

#include <stddef.h>
#include <stdint.h>

typedef struct {
    uint64_t uptime_ms;
    uint32_t free_heap_bytes;
    const char *wifi_state;
    const char *ethernet_state;
    const char *mqtt_state;
    const char *rs485_state;
    uint32_t rs485_errors;
    uint32_t rs485_queue_drops;
} controller_health_snapshot_t;

void controller_health_init(void);
controller_health_snapshot_t controller_health_snapshot(void);
int controller_health_format(char *output, size_t output_size,
                             const controller_health_snapshot_t *snapshot);
