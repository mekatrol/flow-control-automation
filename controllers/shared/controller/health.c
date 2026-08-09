#include "controller/health.h"

#include "controller/runtime.h"
#include "platform/core.h"

static uint64_t started_ms;

static const char LINK_NONE[] = "none";

/* Initializes health timing before the controller runtime begins work. */
void controller_health_init(void)
{
    started_ms = platform_get_monotonic_ms();
}

/* Gets a point-in-time, read-only snapshot of controller subsystem health. */
controller_health_snapshot_t get_controller_health_snapshot(void)
{
    const uint64_t now_ms                       = platform_get_monotonic_ms();
    const network_manager_t *network            = get_controller_runtime_network_manager();
    const network_link_snapshot_t wifi          = network_manager_get_link_snapshot(network, NETWORK_LINK_WIFI);
    const network_link_snapshot_t ethernet      = network_manager_get_link_snapshot(network, NETWORK_LINK_ETHERNET);
    const mqtt_session_health_t mqtt            = get_controller_runtime_mqtt_health();
    const terminal_health_t terminal            = get_controller_runtime_terminal_health();
    const rs485_health_t rs485                  = get_controller_runtime_rs485_health();
    const controller_protocol_health_t protocol = get_controller_runtime_protocol_health();
    const uint32_t rs485_errors                 = rs485.framing_error_count + rs485.parity_error_count + rs485.overflow_count +
                                  rs485.timeout_count + rs485.collision_count + rs485.protocol_error_count;
    const uint32_t protocol_errors = protocol.magic_error_count + protocol.version_error_count + protocol.flag_error_count +
                                     protocol.length_error_count + protocol.crc_error_count +
                                     protocol.unsupported_operation_count + protocol.provider_error_count;

    return (controller_health_snapshot_t){
        .uptime_ms                       = now_ms >= started_ms ? now_ms - started_ms : 0,
        .free_heap_bytes                 = platform_get_free_heap_bytes(),
        .wifi_state                      = network_get_link_state_name(wifi.state),
        .ethernet_state                  = network_get_link_state_name(ethernet.state),
        .mqtt_state                      = mqtt_get_session_state_name(mqtt.state),
        .mqtt_error                      = mqtt_get_error_category_name(mqtt.last_error_category),
        .mqtt_transport                  = mqtt.is_transport_selected ? mqtt.selected_transport.name : LINK_NONE,
        .mqtt_reconnect_count            = mqtt.reconnect_count,
        .mqtt_queue_depth                = mqtt.queued_event_count,
        .rs485_state                     = rs485_get_state_name(rs485.state),
        .rs485_errors                    = rs485_errors,
        .rs485_queue_drops               = rs485.transmit_queue_drop_count + rs485.receive_queue_drop_count,
        .protocol_errors                 = protocol_errors,
        .protocol_response_drops         = protocol.response_drop_count,
        .terminal_state                  = terminal_get_state_name(terminal.state),
        .terminal_authenticated_sessions = terminal.authenticated_session_count,
        .terminal_failed_logins          = terminal.failed_login_count,
        .terminal_output_drops           = terminal.output_drop_count,
    };
}
