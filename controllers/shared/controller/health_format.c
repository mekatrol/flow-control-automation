#include "controller/health.h"

#include <inttypes.h>
#include <stdio.h>

/* Stable status schema consumed by diagnostic tools and future runtime services. */
static const char HEALTH_FORMAT[] =
    "status uptime_ms=%" PRIu64 " free_heap_bytes=%" PRIu64 " wifi=%s ethernet=%s mqtt=%s mqtt_transport=%s mqtt_error=%s"
    " mqtt_reconnect_count=%" PRIu32 " mqtt_queue_depth=%zu rs485=%s"
    " rs485_errors=%" PRIu32 " rs485_queue_drops=%" PRIu32 " protocol_errors=%" PRIu32 " protocol_response_drops=%" PRIu32
    " terminal=%s terminal_sessions=%" PRIu32 " terminal_failed_logins=%" PRIu32 " terminal_output_drops=%" PRIu32;

/* Formats a health snapshot as one bounded structured status line. */
int controller_health_format(char *output, size_t output_size, const controller_health_snapshot_t *snapshot)
{
    if (output == NULL || output_size == 0 || snapshot == NULL || snapshot->wifi_state == NULL ||
        snapshot->ethernet_state == NULL || snapshot->mqtt_state == NULL || snapshot->mqtt_transport == NULL ||
        snapshot->mqtt_error == NULL || snapshot->rs485_state == NULL || snapshot->terminal_state == NULL)
    {
        return -1;
    }

    return snprintf(output, output_size, HEALTH_FORMAT, snapshot->uptime_ms, snapshot->free_heap_bytes, snapshot->wifi_state,
                    snapshot->ethernet_state, snapshot->mqtt_state, snapshot->mqtt_transport, snapshot->mqtt_error,
                    snapshot->mqtt_reconnect_count, snapshot->mqtt_queue_depth, snapshot->rs485_state, snapshot->rs485_errors,
                    snapshot->rs485_queue_drops, snapshot->protocol_errors, snapshot->protocol_response_drops,
                    snapshot->terminal_state, snapshot->terminal_authenticated_sessions, snapshot->terminal_failed_logins,
                    snapshot->terminal_output_drops);
}
