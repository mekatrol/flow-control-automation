#include "controller_health.h"

#include "controller_runtime.h"
#include "platform.h"

static uint64_t started_ms;

void controller_health_init(void)
{
    started_ms = platform_monotonic_ms();
}

controller_health_snapshot_t controller_health_snapshot(void)
{
    const uint64_t now_ms = platform_monotonic_ms();
    const network_manager_t *network = controller_runtime_network_manager();
    const network_link_snapshot_t wifi = network_manager_link_snapshot(
        network, NETWORK_LINK_WIFI);
    const network_link_snapshot_t ethernet = network_manager_link_snapshot(
        network, NETWORK_LINK_ETHERNET);
    return (controller_health_snapshot_t) {
        .uptime_ms = now_ms >= started_ms ? now_ms - started_ms : 0,
        .free_heap_bytes = platform_free_heap_bytes(),
        .wifi_state = network_link_state_name(wifi.state),
        .ethernet_state = network_link_state_name(ethernet.state),
        .mqtt_state = "disabled",
        .rs485_state = "disabled",
        .rs485_errors = 0,
        .rs485_queue_drops = 0,
    };
}
