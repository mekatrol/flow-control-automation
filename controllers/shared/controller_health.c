#include "controller_health.h"

#include "controller_runtime.h"
#include "platform.h"

static uint64_t started_ms;

/* Disabled state is shared by subsystems that do not yet have an implementation. */
static const char STATE_DISABLED[] = "disabled";

/* Initializes health timing before the controller runtime begins work. */
void controller_health_init(void)
{
    started_ms = platform_get_monotonic_ms();
}

/* Gets a point-in-time, read-only snapshot of controller subsystem health. */
controller_health_snapshot_t get_controller_health_snapshot(void)
{
    const uint64_t now_ms                  = platform_get_monotonic_ms();
    const network_manager_t *network       = get_controller_runtime_network_manager();
    const network_link_snapshot_t wifi     = network_manager_get_link_snapshot(network, NETWORK_LINK_WIFI);
    const network_link_snapshot_t ethernet = network_manager_get_link_snapshot(network, NETWORK_LINK_ETHERNET);
    return (controller_health_snapshot_t){
        .uptime_ms         = now_ms >= started_ms ? now_ms - started_ms : 0,
        .free_heap_bytes   = platform_get_free_heap_bytes(),
        .wifi_state        = network_get_link_state_name(wifi.state),
        .ethernet_state    = network_get_link_state_name(ethernet.state),
        .mqtt_state        = STATE_DISABLED,
        .rs485_state       = STATE_DISABLED,
        .rs485_errors      = 0,
        .rs485_queue_drops = 0,
    };
}
