#include "controller_health.h"

#include <inttypes.h>
#include <stdio.h>

int controller_health_format(char *output, size_t output_size,
                             const controller_health_snapshot_t *snapshot)
{
    if (output == NULL || output_size == 0 || snapshot == NULL ||
        snapshot->wifi_state == NULL || snapshot->ethernet_state == NULL ||
        snapshot->mqtt_state == NULL || snapshot->rs485_state == NULL) {
        return -1;
    }
    return snprintf(output, output_size,
                    "status uptime_ms=%" PRIu64 " free_heap_bytes=%" PRIu32
                    " wifi=%s ethernet=%s mqtt=%s rs485=%s"
                    " rs485_errors=%" PRIu32 " rs485_queue_drops=%" PRIu32,
                    snapshot->uptime_ms, snapshot->free_heap_bytes,
                    snapshot->wifi_state, snapshot->ethernet_state,
                    snapshot->mqtt_state, snapshot->rs485_state,
                    snapshot->rs485_errors, snapshot->rs485_queue_drops);
}
