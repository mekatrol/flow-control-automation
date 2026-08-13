#include "controller/runtime.h"

#include <inttypes.h>
#include <stdio.h>
#include <string.h>

#include "board/config.h"
#include "controller/auth.h"
#include "controller/health.h"
#include "controller/io.h"
#include "controller/points.h"
#include "controller/protocol.h"
#include "diagnostics/service.h"
#include "ethernet/link.h"
#include "flow/host.h"
#include "flow/service.h"
#include "network/manager.h"
#include "platform/auth.h"
#include "platform/core.h"
#include "platform/flow.h"
#include "platform/io.h"
#include "platform/mqtt.h"
#include "platform/rs485.h"
#include "platform/settings.h"
#include "platform/terminal.h"
#include "terminal/service.h"

/* Runtime scheduling values balance responsive supervision with bounded CPU use. */
enum
{
    CONTROLLER_TASK_STACK_SIZE      = 49152,
    CONTROLLER_TASK_PRIORITY        = 5,
    STATUS_INTERVAL_MS              = 5000,
    CONTROLLER_TICK_MS              = 100,
    STATUS_BUFFER_SIZE              = 256,
    ETHERNET_ROUTE_PRIORITY         = 10,
    ETHERNET_INITIAL_BACKOFF_MS     = 1000,
    ETHERNET_MAXIMUM_BACKOFF_MS     = 60000,
    ETHERNET_BACKOFF_JITTER_PERCENT = 20,
    ETHERNET_STABLE_ONLINE_MS       = 30000,
    MAXIMUM_MQTT_EVENTS_PER_TICK    = 8,
    MQTT_EVENT_RATE_WINDOW_MS       = 10000,
    MAXIMUM_MQTT_EVENTS_PER_WINDOW  = 4,
    TERMINAL_IDLE_TIMEOUT_MS        = 300000,
    TERMINAL_LOGIN_BACKOFF_MS       = 2000,
    TERMINAL_READ_CAPACITY          = 64,
    SETTINGS_REBOOT_SETTLE_DELAY_MS = 20,
    MQTT_HEALTH_PAYLOAD_CAPACITY    = 192,
    MAXIMUM_RS485_EVENTS_PER_TICK   = 8,
    PROTOCOL_DEVICE_ID_CAPACITY     = 40,
    IO_POLL_INTERVAL_MS             = 100,
    AUTH_CHALLENGE_LIFETIME_MS      = 30000,
    AUTH_SESSION_LIFETIME_MS        = 900000,
    AUTH_MAXIMUM_ATTEMPTS           = 3,
};

/* Runtime diagnostic identifiers define the stable heartbeat event schema. */
static const char CONTROLLER_TASK_NAME[]           = "controller_runtime";
static const char COMPONENT_RUNTIME[]              = "runtime";
static const char EVENT_HEARTBEAT[]                = "heartbeat";
static const char FORMAT_STATUS[]                  = "%s";
static const char COMPONENT_MQTT[]                 = "mqtt";
static const char EVENT_MQTT_CONFIG[]              = "configuration_invalid";
static const char EVENT_MQTT_STATE[]               = "state_change";
static const char MESSAGE_MQTT_CONFIG[]            = "MQTT host requires a valid client ID and reconnect limits";
static const char FORMAT_MQTT_STATE[]              = "state=%s transport=%s error=%s reconnect_count=%u queue_depth=%u";
static const char TRANSPORT_NONE[]                 = "none";
static const char MQTT_AVAILABILITY_TOPIC_FORMAT[] = "controllers/%s/status/availability";
static const char MQTT_HEALTH_TOPIC_FORMAT[]       = "controllers/%s/status/health";
static const char MQTT_ONLINE_PAYLOAD[]            = "online";
static const char MQTT_OFFLINE_PAYLOAD[]           = "offline";
static const char MQTT_AVAILABILITY_CORRELATION[]  = "runtime-availability";
static const char MQTT_HEALTH_CORRELATION[]        = "runtime-health";
static const char FORMAT_MQTT_HEALTH_PAYLOAD[] =
    "{\"schema\":1,\"uptime_ms\":%" PRIu64 ",\"free_heap_bytes\":%" PRIu64 ",\"mqtt_reconnect_count\":%" PRIu32
    ",\"publish_queue\":%u,\"receive_queue\":%u}";
static const char FORMAT_MQTT_TERMINAL_STATUS[] =
    "state=%s\r\ntransport=%s\r\nerror=%s\r\nreconnect_count=%u\r\npublish_queue=%u\r\nreceive_queue=%u\r\n"
    "publish_rejections=%u\r\nreceive_rejections=%u";
static const char COMPONENT_SETTINGS[]    = "settings";
static const char EVENT_SETTINGS_STATE[]  = "state";
static const char FORMAT_SETTINGS_STATE[] = "state=%s schema=%u generation=%u";
static const char COMPONENT_RS485[]       = "rs485";
static const char EVENT_RS485_STATE[]     = "state";
static const char FORMAT_RS485_STATE[]    = "state=%s baud=%u format=%u%c%u protocol=fcp-v1 automatic_direction=1";
static const char ADDRESS_UNAVAILABLE[]   = "unavailable";
static const char FORMAT_SYSTEM_INFO[] =
    "device=%s\r\nfirmware=%s\r\nversion=%s\r\nhardware=%s\r\nprocessor=%s\r\nhostname=%s\r\n"
    "uptime_ms=%" PRIu64 "\r\nfree_heap_bytes=%" PRIu64 "\r\nwifi=%s\r\nwifi_ipv4=%s\r\nwifi_ipv6=%s\r\n"
    "ethernet=%s\r\nethernet_ipv4=%s\r\nethernet_ipv6=%s\r\nmqtt=%s\r\nmqtt_transport=%s\r\n"
    "mqtt_error=%s\r\nmqtt_reconnect_count=%" PRIu32 "\r\nmqtt_queue_depth=%zu\r\nrs485=%s\r\n"
    "rs485_address=%u\r\nrs485_baud_rate=%" PRIu32 "\r\nrs485_errors=%" PRIu32 "\r\nrs485_queue_drops=%" PRIu32
    "\r\nprotocol_errors=%" PRIu32 "\r\nprotocol_response_drops=%" PRIu32 "\r\nterminal=%s\r\nterminal_sessions=%" PRIu32
    "\r\nterminal_failed_logins=%" PRIu32 "\r\nterminal_output_drops=%" PRIu32 "\r\nbuses=unsupported";

static network_manager_t controller_network_manager;
static ethernet_link_t controller_ethernet_link;
static mqtt_service_t controller_mqtt_service;
static mqtt_api_t controller_mqtt_api;
static diagnostic_rate_limiter_t mqtt_event_rate_limiter;
static mqtt_session_state_t previous_mqtt_state = MQTT_SESSION_DISABLED;
static settings_service_t controller_settings_service;
static controller_settings_t controller_settings_snapshot;
static terminal_service_t controller_terminal_service;
static rs485_service_t controller_rs485_service;
static controller_protocol_t controller_protocol;
static controller_auth_t controller_auth;
static controller_flow_t controller_flow;
static flow_debug_t controller_debug;
static flow_host_t controller_flow_host;
static uint64_t next_flow_scan_ms;
static flow_target_point_t debug_target_points[CONTROLLER_IO_POINT_COUNT];
static flow_input_sample_t debug_input_samples[CONTROLLER_IO_INPUT_COUNT];
static flow_target_t debug_target;
static controller_points_t controller_points;
static bool is_flow_ready;
static bool is_debug_ready;
static bool is_flow_host_ready;
static controller_io_t controller_io;
static bool is_io_ready;
static uint64_t next_io_poll_ms;
static char protocol_device_id[PROTOCOL_DEVICE_ID_CAPACITY];
static platform_settings_result_t platform_settings_result = PLATFORM_SETTINGS_DISABLED;
static char mqtt_availability_topic[MQTT_TOPIC_CAPACITY];
static char mqtt_health_topic[MQTT_TOPIC_CAPACITY];

/* Resolves a stable output point identifier to the physical output index. */
static bool get_debug_output_index(const char *point_id, uint8_t *output);

/* Copies an encoded protocol response into the bounded RS485 transmit queue. */
static bool send_protocol_frame(void *context, const uint8_t *data, size_t size)
{
    return rs485_service_send(context, data, size);
}

/* Initializes the transport-neutral protocol dispatcher from current persistent address settings. */
static void initialize_controller_protocol(void)
{
    platform_startup_info_t startup;
    platform_get_startup_info(&startup);
    platform_get_device_id(protocol_device_id, sizeof(protocol_device_id));
    platform_auth_deinitialize();
    const bool is_auth_ready = controller_settings_snapshot.protocol_key.is_set &&
                               platform_auth_initialize(controller_settings_snapshot.protocol_key.value);

    if (is_auth_ready)
    {
        const controller_auth_config_t auth_config = {.get_hmac              = platform_auth_get_hmac,
                                                      .get_random            = platform_auth_get_random,
                                                      .challenge_lifetime_ms = AUTH_CHALLENGE_LIFETIME_MS,
                                                      .session_lifetime_ms   = AUTH_SESSION_LIFETIME_MS,
                                                      .maximum_attempts      = AUTH_MAXIMUM_ATTEMPTS};
        controller_auth_init(&controller_auth, &auth_config);
    }

    controller_protocol_config_t config = {.address          = controller_settings_snapshot.rs485.address,
                                           .device_id        = protocol_device_id,
                                           .hardware_model   = get_controller_board_name(),
                                           .firmware_version = startup.firmware_version,
                                           .point_provider   = controller_io_get_point_provider(&controller_io),
                                           .get_io_block     = controller_io_get_protocol_block,
                                           .set_output       = controller_io_set_protocol_output,
                                           .set_output_block = controller_io_set_protocol_output_block,
                                           .io_context       = &controller_io,
                                           .auth             = is_auth_ready ? &controller_auth : NULL};
    config.flow                         = is_flow_ready ? &controller_flow : NULL;
    config.debug                        = is_debug_ready ? &controller_debug : NULL;
    config.points                       = is_io_ready ? &controller_points : NULL;
    controller_protocol_init(&controller_protocol, &config, send_protocol_frame, &controller_rs485_service);
}

/* Recovers one complete durable flow generation after NVS initialization. */
static void initialize_flow(void)
{
    controller_flow_store_t store;
    is_flow_ready = platform_flow_initialize(&store) && controller_flow_init(&controller_flow, platform_flow_get_digest,
                                                                             platform_flow_is_artifact_valid, NULL, &store);
}

/* Captures the physical digital inputs into the v2 VM's coherent input frame. */
static bool read_flow_inputs(void * /* context */, flow_vm_input_sample_t *samples, size_t capacity, size_t *count,
                             uint64_t *sampled_at_ms)
{
    const controller_io_snapshot_t snapshot = controller_io_get_snapshot(&controller_io);

    if (samples == NULL || count == NULL || sampled_at_ms == NULL || capacity < CONTROLLER_IO_INPUT_COUNT)
    {
        return false;
    }

    for (size_t index = 0; index < CONTROLLER_IO_INPUT_COUNT; index++)
    {
        snprintf(samples[index].point_id, sizeof(samples[index].point_id), "input-%02u", (unsigned)(index + 1U));
        samples[index].value   = (snapshot.inputs & (uint16_t)(1U << index)) != 0U;
        samples[index].quality = snapshot.are_inputs_valid ? 1U : 0U;
    }

    *count         = CONTROLLER_IO_INPUT_COUNT;
    *sampled_at_ms = (uint64_t)snapshot.sampled_at_ms;

    return snapshot.are_inputs_valid;
}

/* Publishes one committed v2 command batch under the durable flow owner. */
static bool publish_flow_commands(void * /* context */, const flow_vm_command_t *commands, size_t count, uint64_t now_ms)
{
    static const char SOURCE_ID[]              = "flow-runtime";
    static const char CORRELATION_ID[]         = "scan";
    static const uint8_t FLOW_COMMAND_PRIORITY = 16;

    for (size_t index = 0; index < count; index++)
    {
        uint8_t output = 0;

        if (!get_debug_output_index(commands[index].point_id, &output))
        {
            return false;
        }

        controller_point_command_t command = {.is_used       = true,
                                              .output        = output,
                                              .command_class = 1,
                                              .priority      = FLOW_COMMAND_PRIORITY,
                                              .value         = commands[index].value,
                                              .issued_at_ms  = (int64_t)now_ms,
                                              .expires_at_ms = 0};
        snprintf(command.source_id, sizeof(command.source_id), "%s", SOURCE_ID);
        snprintf(command.correlation_id, sizeof(command.correlation_id), "%s", CORRELATION_ID);

        if (controller_points_command(&controller_points, &command, command.issued_at_ms) != CONTROLLER_POINT_OK)
        {
            return false;
        }
    }

    return true;
}

/* Synchronizes durable activation and invokes one bounded production PLC scan. */
static void process_flow(uint64_t now_ms)
{
    if (!is_flow_ready || !is_flow_host_ready || now_ms < next_flow_scan_ms)
    {
        return;
    }

    if (flow_host_synchronize(&controller_flow_host, &controller_flow) && controller_flow_host.is_running)
    {
        flow_vm_snapshot_t snapshot;
        flow_host_scan(&controller_flow_host, now_ms, &snapshot);
    }

    next_flow_scan_ms = now_ms + CONTROLLER_TICK_MS;
}

/* Copies the latest coherent physical input bitmap into the portable debug evaluator adapter. */
static bool get_debug_input(void * /* context */, flow_input_frame_t *frame)
{
    const controller_io_snapshot_t snapshot = controller_io_get_snapshot(&controller_io);

    for (size_t index = 0; index < CONTROLLER_IO_INPUT_COUNT; index++)
    {
        debug_input_samples[index].value   = (snapshot.inputs & (uint16_t)(1U << index)) != 0U;
        debug_input_samples[index].quality = snapshot.are_inputs_valid ? FLOW_QUALITY_GOOD : FLOW_QUALITY_UNAVAILABLE;
    }

    *frame = (flow_input_frame_t){.samples       = debug_input_samples,
                                  .sample_count  = CONTROLLER_IO_INPUT_COUNT,
                                  .sampled_at_ms = (uint64_t)snapshot.sampled_at_ms,
                                  .is_coherent   = snapshot.are_inputs_valid};

    return true;
}

/* Gets platform monotonic microseconds through the portable debug timing adapter. */
static uint64_t get_debug_time_us(void * /* context */)
{
    return platform_get_monotonic_us();
}

/* Resolves a configured debug output point to its bounded physical output index. */
static bool get_debug_output_index(const char *point_id, uint8_t *output)
{
    for (size_t index = 0; index < CONTROLLER_IO_OUTPUT_COUNT; index++)
    {
        const flow_target_point_t *point = &debug_target_points[CONTROLLER_IO_INPUT_COUNT + index];

        if (strcmp(point->id, point_id) == 0)
        {
            *output = (uint8_t)index;
            return true;
        }
    }

    return false;
}

/* Submits one short-lived command under the dedicated volatile debug owner. */
static bool command_debug_output(void * /* context */, const char *point_id, bool value, uint8_t priority, uint64_t expires_at_ms,
                                 bool *is_effective)
{
    static const char DEBUG_SOURCE_ID[]      = "flow-debug";
    static const char DEBUG_CORRELATION_ID[] = "live-tick";
    uint8_t output                           = 0;

    if (!is_io_ready || is_effective == NULL || expires_at_ms > INT64_MAX || !get_debug_output_index(point_id, &output))
    {
        return false;
    }

    controller_point_command_t command = {.is_used       = true,
                                          .output        = output,
                                          .command_class = 1,
                                          .priority      = priority,
                                          .value         = value,
                                          .issued_at_ms  = (int64_t)platform_get_monotonic_ms(),
                                          .expires_at_ms = (int64_t)expires_at_ms};
    snprintf(command.source_id, sizeof(command.source_id), "%s", DEBUG_SOURCE_ID);
    snprintf(command.correlation_id, sizeof(command.correlation_id), "%s", DEBUG_CORRELATION_ID);

    if (controller_points_command(&controller_points, &command, command.issued_at_ms) != CONTROLLER_POINT_OK)
    {
        return false;
    }

    *is_effective = controller_points_is_source_effective(&controller_points, output, DEBUG_SOURCE_ID);
    return true;
}

/* Relinquishes only the dedicated volatile debug owner's command for one physical point. */
static void relinquish_debug_output(void * /* context */, const char *point_id)
{
    static const char DEBUG_SOURCE_ID[] = "flow-debug";
    uint8_t output                      = 0;

    if (is_io_ready && get_debug_output_index(point_id, &output))
    {
        controller_points_relinquish(&controller_points, output, DEBUG_SOURCE_ID, (int64_t)platform_get_monotonic_ms());
    }
}

/* Builds the fixed KC868 digital target table used only for volatile shadow evaluation. */
static bool initialize_debug(void)
{
    for (size_t index = 0; index < CONTROLLER_IO_INPUT_COUNT; index++)
    {
        const int input_size =
            snprintf(debug_target_points[index].id, sizeof(debug_target_points[index].id), "input-%02u", (unsigned)(index + 1U));
        const int sample_size = snprintf(debug_input_samples[index].point_id, sizeof(debug_input_samples[index].point_id),
                                         "input-%02u", (unsigned)(index + 1U));

        if (input_size <= 0 || (size_t)input_size >= sizeof(debug_target_points[index].id) || sample_size <= 0 ||
            (size_t)sample_size >= sizeof(debug_input_samples[index].point_id))
        {
            return false;
        }

        debug_target_points[index].direction  = 1;
        debug_target_points[index].value_type = CONTROLLER_PROTOCOL_POINT_DIGITAL;
    }

    for (size_t index = 0; index < CONTROLLER_IO_OUTPUT_COUNT; index++)
    {
        flow_target_point_t *point = &debug_target_points[CONTROLLER_IO_INPUT_COUNT + index];
        const int size             = snprintf(point->id, sizeof(point->id), "output-%02u", (unsigned)(index + 1U));

        if (size <= 0 || (size_t)size >= sizeof(point->id))
        {
            return false;
        }

        point->direction  = 2;
        point->value_type = CONTROLLER_PROTOCOL_POINT_DIGITAL;
    }

    debug_target              = (flow_target_t){.points                 = debug_target_points,
                                                .point_count            = CONTROLLER_IO_POINT_COUNT,
                                                .supported_capabilities = UINT32_C(0x1f),
                                                .maximum_snapshot_bytes = FLOW_DEBUG_SNAPSHOT_CAPACITY};
    const bool is_initialized = flow_debug_init(&controller_debug, &debug_target, get_debug_input, NULL);

    if (is_initialized)
    {
        flow_debug_set_time_source(&controller_debug, get_debug_time_us, NULL);
        flow_debug_set_output_adapter(&controller_debug, command_debug_output, relinquish_debug_output, NULL);
    }

    return is_initialized;
}

/* Initializes field I/O in a safe read-only mode and leaves failed hardware explicitly unavailable. */
static void initialize_io(void)
{
    controller_io_init(&controller_io);
    platform_io_config_t config;
    controller_board_get_io_config(&config);
    is_io_ready = platform_io_initialize(&config);

    if (is_io_ready)
    {
        controller_io_set_writer(&controller_io, platform_io_write_outputs);
        controller_points_init(&controller_points, platform_io_write_outputs);
    }

    is_debug_ready     = initialize_debug();
    is_flow_host_ready = flow_host_init(&controller_flow_host, read_flow_inputs, publish_flow_commands, NULL);
    next_flow_scan_ms  = platform_get_monotonic_ms();
    next_io_poll_ms    = platform_get_monotonic_ms();
}

/* Polls all PCF8574 banks into one cache so protocol reads never block on field I/O. */
static void process_io(uint64_t now_ms)
{
    if (!is_io_ready || now_ms < next_io_poll_ms)
    {
        return;
    }

    uint16_t inputs        = 0;
    uint16_t outputs       = 0;
    bool are_inputs_valid  = false;
    bool are_outputs_valid = false;
    platform_io_read(&inputs, &are_inputs_valid, &outputs, &are_outputs_valid);
    controller_io_update(&controller_io, inputs, are_inputs_valid, outputs, are_outputs_valid, (int64_t)now_ms);

    if (are_outputs_valid)
    {
        controller_points_observe(&controller_points, outputs);
    }

    controller_points_process(&controller_points, (int64_t)now_ms);
    next_io_poll_ms = now_ms + IO_POLL_INTERVAL_MS;
}

/* Gets the persisted RS485 baud or the board build default while settings are unavailable. */
static uint32_t get_rs485_baud_rate(void)
{
    if (controller_settings_snapshot.rs485.baud_rate != 0)
    {
        return controller_settings_snapshot.rs485.baud_rate;
    }

    rs485_config_t config;
    controller_board_get_rs485_config(&config);

    return config.baud_rate;
}

/* Gets the stable one-character diagnostic parity marker for the configured UART format. */
static char get_rs485_parity_marker(rs485_parity_t parity)
{
    static const char markers[] = {'N', 'E', 'O'};

    return parity <= RS485_PARITY_ODD ? markers[parity] : markers[RS485_PARITY_NONE];
}

/* Gets platform entropy through the callback signature required by communications supervisors. */
static uint32_t get_communications_random(void *context);

/* Builds hostname-scoped status topics and applies the retained broker last will. */
static void configure_mqtt_status_topics(mqtt_broker_config_t *config)
{
    const char *hostname = controller_settings_snapshot.hostname.is_set ? controller_settings_snapshot.hostname.value
                                                                        : get_controller_default_hostname();
    const int availability_size =
        snprintf(mqtt_availability_topic, sizeof(mqtt_availability_topic), MQTT_AVAILABILITY_TOPIC_FORMAT, hostname);
    const int health_size = snprintf(mqtt_health_topic, sizeof(mqtt_health_topic), MQTT_HEALTH_TOPIC_FORMAT, hostname);

    if (availability_size <= 0 || (size_t)availability_size >= sizeof(mqtt_availability_topic) || health_size <= 0 ||
        (size_t)health_size >= sizeof(mqtt_health_topic))
    {
        mqtt_availability_topic[0] = '\0';
        mqtt_health_topic[0]       = '\0';

        return;
    }

    config->last_will_topic       = mqtt_availability_topic;
    config->last_will_payload     = MQTT_OFFLINE_PAYLOAD;
    config->last_will_qos         = MQTT_QOS_AT_LEAST_ONCE;
    config->is_last_will_retained = true;
}

/* Writes terminal output through the board-selected non-blocking transport. */
static bool write_terminal(void * /* context */, const char *data, size_t size)
{
    return platform_terminal_write(data, size);
}

/* Formats a redacted device snapshot from portable board, platform, and subsystem contracts. */
static void get_terminal_system_info(void * /* context */, char *output, size_t capacity)
{
    platform_startup_info_t startup;
    platform_get_startup_info(&startup);
    const controller_health_snapshot_t health = get_controller_health_snapshot();
    const network_link_snapshot_t wifi        = network_manager_get_link_snapshot(&controller_network_manager, NETWORK_LINK_WIFI);
    const network_link_snapshot_t ethernet =
        network_manager_get_link_snapshot(&controller_network_manager, NETWORK_LINK_ETHERNET);
    const char *hostname = controller_settings_snapshot.hostname.is_set ? controller_settings_snapshot.hostname.value
                                                                        : get_controller_default_hostname();

    /* Render one field per line so interactive users can scan the snapshot without horizontal wrapping. */
    snprintf(output, capacity, FORMAT_SYSTEM_INFO, get_controller_board_name(), startup.firmware_name, startup.firmware_version,
             get_controller_board_name(), startup.processor, hostname, health.uptime_ms, health.free_heap_bytes,
             health.wifi_state, wifi.ipv4_address[0] != '\0' ? wifi.ipv4_address : ADDRESS_UNAVAILABLE,
             wifi.ipv6_address[0] != '\0' ? wifi.ipv6_address : ADDRESS_UNAVAILABLE, health.ethernet_state,
             ethernet.ipv4_address[0] != '\0' ? ethernet.ipv4_address : ADDRESS_UNAVAILABLE,
             ethernet.ipv6_address[0] != '\0' ? ethernet.ipv6_address : ADDRESS_UNAVAILABLE, health.mqtt_state,
             health.mqtt_transport, health.mqtt_error, health.mqtt_reconnect_count, health.mqtt_queue_depth, health.rs485_state,
             (unsigned)controller_settings_snapshot.rs485.address, get_rs485_baud_rate(), health.rs485_errors,
             health.rs485_queue_drops, health.protocol_errors, health.protocol_response_drops, health.terminal_state,
             health.terminal_authenticated_sessions, health.terminal_failed_logins, health.terminal_output_drops);
}

/* Dispatches the portable reboot request after terminal confirmation. */
static bool reboot_terminal(void * /* context */)
{
    platform_settings_prepare_reboot();

    /* Give the powered card time to observe inactive chip select before the CPU resets its GPIO routing. */
    platform_delay_ms(SETTINGS_REBOOT_SETTLE_DELAY_MS);

    return platform_reboot();
}

/* Initializes user-confirmed foreign settings media without touching sectors outside the reserved range. */
static bool initialize_terminal_storage(void * /* context */)
{
    return platform_settings_initialize_media();
}

/* Formats MQTT session and bounded API status without broker settings or credentials. */
static void get_terminal_mqtt_status(void * /* context */, char *output, size_t capacity)
{
    const mqtt_session_health_t session = mqtt_service_get_health(&controller_mqtt_service);
    const mqtt_api_health_t api         = mqtt_api_get_health(&controller_mqtt_api);
    snprintf(output, capacity, FORMAT_MQTT_TERMINAL_STATUS, mqtt_get_session_state_name(session.state),
             session.is_transport_selected ? session.selected_transport.name : TRANSPORT_NONE,
             mqtt_get_error_category_name(session.last_error_category), (unsigned)session.reconnect_count,
             (unsigned)api.publish_queue_depth, (unsigned)api.receive_queue_depth, (unsigned)api.publish_rejection_count,
             (unsigned)api.receive_rejection_count);
}

/* Applies a durable terminal settings generation to MQTT without restarting unrelated services. */
static void apply_terminal_settings(void * /* context */)
{
    controller_settings_snapshot = settings_service_get_snapshot(&controller_settings_service);
    mqtt_service_stop(&controller_mqtt_service);
    mqtt_broker_config_t mqtt_config;
    platform_mqtt_get_config(&mqtt_config, &controller_settings_snapshot);
    configure_mqtt_status_topics(&mqtt_config);
    mqtt_service_init(&controller_mqtt_service, &mqtt_config, platform_mqtt_get_transport_route, platform_mqtt_connect,
                      platform_mqtt_disconnect, platform_mqtt_replay_subscriptions, get_communications_random,
                      &controller_network_manager);
    previous_mqtt_state = controller_mqtt_service.state;
    mqtt_api_set_online(&controller_mqtt_api, false);
    rs485_config_t rs485_config;
    controller_board_get_rs485_config(&rs485_config);
    rs485_config.baud_rate = get_rs485_baud_rate();

    if (platform_rs485_reconfigure(&rs485_config))
    {
        rs485_service_init(&controller_rs485_service, &rs485_config, platform_rs485_write);
        initialize_controller_protocol();
    }
}

/* Routes diagnostics through the terminal's explicit authenticated Diagnostics mode. */
static void emit_terminal_diagnostic(void *context, const char *record)
{
    terminal_service_emit_diagnostic(context, record);
}

/* Initializes the default USB terminal without waiting for a connected peer. */
static void initialize_terminal(void)
{
    if (!platform_terminal_initialize())
    {
        return;
    }

    const terminal_config_t config = {
        .settings           = &controller_settings_service,
        .write              = write_terminal,
        .get_system_info    = get_terminal_system_info,
        .reboot             = reboot_terminal,
        .initialize_storage = platform_settings_result == PLATFORM_SETTINGS_MEDIA_INVALID ? initialize_terminal_storage : NULL,
        .settings_changed   = apply_terminal_settings,
        .get_mqtt_status    = get_terminal_mqtt_status,
        .settings_unavailable_reason = platform_settings_get_result_name(platform_settings_result),
        .context                     = NULL,
        .idle_timeout_ms             = TERMINAL_IDLE_TIMEOUT_MS,
        .login_backoff_ms            = TERMINAL_LOGIN_BACKOFF_MS};
    terminal_service_init(&controller_terminal_service, &config);
    diagnostics_set_sink(emit_terminal_diagnostic, &controller_terminal_service);
    terminal_service_connect(&controller_terminal_service, platform_get_monotonic_ms());
}

/* Initializes board-selected settings and reports only redacted storage metadata. */
static void initialize_settings(void)
{
    settings_storage_config_t storage_config;
    settings_defaults_t defaults;
    settings_store_t store;
    controller_board_get_settings_storage_config(&storage_config);
    controller_board_get_settings_defaults(&defaults);
    platform_settings_result             = platform_settings_initialize(&storage_config, &store);
    const settings_storage_state_t state = settings_service_initialize(
        &controller_settings_service, platform_settings_result == PLATFORM_SETTINGS_READY ? &store : NULL, &defaults);
    controller_settings_snapshot = state == SETTINGS_STORAGE_READY ? settings_service_get_snapshot(&controller_settings_service)
                                                                   : (controller_settings_t){0};
    diagnostics_emit(state == SETTINGS_STORAGE_READY ? DIAGNOSTIC_INFO : DIAGNOSTIC_WARNING, COMPONENT_SETTINGS,
                     EVENT_SETTINGS_STATE, FORMAT_SETTINGS_STATE, settings_get_storage_state_name(state),
                     controller_settings_service.schema_version, controller_settings_service.generation);
}

/* Dispatches a supervisor start action to its independent link adapter. */
static void start_network_link(network_link_id_t link_id, void * /* context */)
{
    /* Dispatch only Ethernet because Wi-Fi is intentionally dormant. */
    if (link_id == NETWORK_LINK_ETHERNET)
    {
        ethernet_link_start(&controller_ethernet_link);
    }
}

/* Dispatches a supervisor stop action to its independent link adapter. */
static void stop_network_link(network_link_id_t link_id, void * /* context */)
{
    /* Stop only the independently owned Ethernet interface. */
    if (link_id == NETWORK_LINK_ETHERNET)
    {
        ethernet_link_stop(&controller_ethernet_link);
    }
}

/* Gets platform entropy through the callback signature required by communications supervisors. */
static uint32_t get_communications_random(void * /* context */)
{
    return platform_get_random_u32();
}

/* Initializes networking after task startup so boot never waits for association. */
static void initialize_networking(void)
{
    ethernet_link_config_t ethernet_config;
    controller_board_get_ethernet_config(&ethernet_config);
    ethernet_config.hostname     = controller_settings_snapshot.hostname.is_set ? controller_settings_snapshot.hostname.value
                                                                                : get_controller_default_hostname();
    const bool is_ethernet_ready = ethernet_link_init(&controller_ethernet_link, &controller_network_manager, &ethernet_config);
    const network_link_config_t network_configs[NETWORK_LINK_COUNT] = {
        [NETWORK_LINK_WIFI] =
            {
                .enabled = false,
            },
        [NETWORK_LINK_ETHERNET] =
            {
                .enabled            = is_ethernet_ready && ethernet_config.enabled,
                .priority           = ETHERNET_ROUTE_PRIORITY,
                .initial_backoff_ms = ETHERNET_INITIAL_BACKOFF_MS,
                .maximum_backoff_ms = ETHERNET_MAXIMUM_BACKOFF_MS,
                .jitter_percent     = ETHERNET_BACKOFF_JITTER_PERCENT,
                .stable_online_ms   = ETHERNET_STABLE_ONLINE_MS,
            },
    };
    network_manager_init(&controller_network_manager, network_configs, start_network_link, stop_network_link,
                         get_communications_random, NULL, platform_get_monotonic_ms());
}

/* Initializes MQTT against the selected transport adapter independently of network supervision. */
static void initialize_mqtt(void)
{
    mqtt_broker_config_t mqtt_config;
    platform_mqtt_get_config(&mqtt_config, &controller_settings_snapshot);
    configure_mqtt_status_topics(&mqtt_config);
    const bool is_mqtt_platform_ready = platform_mqtt_initialize();
    mqtt_api_init(&controller_mqtt_api, platform_mqtt_publish, platform_mqtt_subscribe, NULL);
    platform_mqtt_set_api(&controller_mqtt_api);
    mqtt_service_init(&controller_mqtt_service, &mqtt_config, platform_mqtt_get_transport_route,
                      is_mqtt_platform_ready ? platform_mqtt_connect : NULL, platform_mqtt_disconnect,
                      platform_mqtt_replay_subscriptions, get_communications_random, &controller_network_manager);
    previous_mqtt_state = controller_mqtt_service.state;

    if (mqtt_config.enabled && (!is_mqtt_platform_ready || controller_mqtt_service.state == MQTT_SESSION_DISABLED))
    {
        diagnostics_emit(DIAGNOSTIC_ERROR, COMPONENT_MQTT, EVENT_MQTT_CONFIG, MESSAGE_MQTT_CONFIG);
    }
}

/* Publishes the retained versioned online marker after each successful connection. */
static void publish_mqtt_availability(void)
{
    const mqtt_publish_request_t request = {.topic          = mqtt_availability_topic,
                                            .payload        = MQTT_ONLINE_PAYLOAD,
                                            .payload_size   = sizeof(MQTT_ONLINE_PAYLOAD) - 1,
                                            .qos            = MQTT_QOS_AT_LEAST_ONCE,
                                            .is_retained    = true,
                                            .correlation_id = MQTT_AVAILABILITY_CORRELATION,
                                            .offline_policy = MQTT_OFFLINE_REPLACE_NEWEST};
    mqtt_api_publish(&controller_mqtt_api, &request);
}

/* Publishes a coalesced versioned health snapshot whose offline backlog remains one message. */
static void publish_mqtt_health(void)
{
    char payload[MQTT_HEALTH_PAYLOAD_CAPACITY];
    const controller_health_snapshot_t health = get_controller_health_snapshot();
    const mqtt_api_health_t mqtt_api_health   = mqtt_api_get_health(&controller_mqtt_api);
    const int size = snprintf(payload, sizeof(payload), FORMAT_MQTT_HEALTH_PAYLOAD, health.uptime_ms, health.free_heap_bytes,
                              health.mqtt_reconnect_count, (unsigned)mqtt_api_health.publish_queue_depth,
                              (unsigned)mqtt_api_health.receive_queue_depth);

    if (size <= 0 || (size_t)size >= sizeof(payload))
    {
        return;
    }

    const mqtt_publish_request_t request = {.topic          = mqtt_health_topic,
                                            .payload        = payload,
                                            .payload_size   = (size_t)size,
                                            .qos            = MQTT_QOS_AT_LEAST_ONCE,
                                            .is_retained    = true,
                                            .correlation_id = MQTT_HEALTH_CORRELATION,
                                            .offline_policy = MQTT_OFFLINE_REPLACE_NEWEST};
    mqtt_api_publish(&controller_mqtt_api, &request);
}

/* Publishes through the runtime-owned bounded bidirectional MQTT API. */
mqtt_delivery_status_t controller_runtime_mqtt_publish(const mqtt_publish_request_t *request)
{
    return mqtt_api_publish(&controller_mqtt_api, request);
}

/* Registers one runtime MQTT subscription for automatic reconnect replay. */
bool controller_runtime_mqtt_subscribe(const mqtt_subscription_t *subscription)
{
    return mqtt_api_subscribe(&controller_mqtt_api, subscription);
}

/* Gets the runtime MQTT API queue and overload snapshot. */
mqtt_api_health_t get_controller_runtime_mqtt_api_health(void)
{
    return mqtt_api_get_health(&controller_mqtt_api);
}

/* Gets the runtime-owned RS485 health snapshot without exposing frame contents. */
rs485_health_t get_controller_runtime_rs485_health(void)
{
    return rs485_service_get_health(&controller_rs485_service);
}

/* Copies a complete frame into the runtime-owned bounded RS485 transmit queue. */
bool controller_runtime_rs485_send(const uint8_t *data, size_t size)
{
    return rs485_service_send(&controller_rs485_service, data, size);
}

/* Gets and removes the oldest complete runtime-owned RS485 receive frame. */
bool controller_runtime_rs485_get_received(rs485_frame_t *frame)
{
    return rs485_service_get_received(&controller_rs485_service, frame);
}

/* Gets protocol validation and response counters without exposing message content. */
controller_protocol_health_t get_controller_runtime_protocol_health(void)
{
    return controller_protocol_get_health(&controller_protocol);
}

/* Gets the runtime-owned network manager for read-only consumer discovery. */
const network_manager_t *get_controller_runtime_network_manager(void)
{
    return &controller_network_manager;
}

/* Gets the runtime-owned MQTT health snapshot for diagnostics and consumers. */
mqtt_session_health_t get_controller_runtime_mqtt_health(void)
{
    return mqtt_service_get_health(&controller_mqtt_service);
}

/* Gets the runtime-owned terminal health snapshot without credential data. */
terminal_health_t get_controller_runtime_terminal_health(void)
{
    return terminal_service_get_health(&controller_terminal_service);
}

/* Drains bounded platform events and advances the portable MQTT supervisor. */
static void process_mqtt(uint64_t now_ms)
{
    mqtt_queued_event_t queued_event;

    for (size_t processed = 0; processed < MAXIMUM_MQTT_EVENTS_PER_TICK; processed++)
    {
        if (!platform_mqtt_get_event(&queued_event))
        {
            break;
        }

        const mqtt_transport_event_t event = {
            .type           = queued_event.type,
            .sequence       = queued_event.sequence,
            .error_category = queued_event.error_category,
            .error_detail   = queued_event.error_detail,
        };
        mqtt_service_enqueue_event(&controller_mqtt_service, &event);
    }

    mqtt_service_process(&controller_mqtt_service, now_ms);
    const mqtt_session_health_t health = mqtt_service_get_health(&controller_mqtt_service);
    mqtt_api_set_online(&controller_mqtt_api, health.state == MQTT_SESSION_ONLINE);
    mqtt_inbound_message_t inbound;

    for (size_t processed = 0; processed < MQTT_RECEIVE_QUEUE_CAPACITY && platform_mqtt_get_inbound(&inbound); processed++)
    {
        mqtt_api_enqueue_inbound(&controller_mqtt_api, inbound.topic, strlen(inbound.topic), inbound.payload,
                                 inbound.payload_size, inbound.qos, inbound.is_duplicate);
    }

    mqtt_api_process(&controller_mqtt_api);

    if (health.state != previous_mqtt_state)
    {
        if (health.state == MQTT_SESSION_ONLINE)
        {
            publish_mqtt_availability();
        }

        diagnostics_emit_limited(&mqtt_event_rate_limiter, MQTT_EVENT_RATE_WINDOW_MS, MAXIMUM_MQTT_EVENTS_PER_WINDOW,
                                 health.state == MQTT_SESSION_ONLINE ? DIAGNOSTIC_INFO : DIAGNOSTIC_WARNING, COMPONENT_MQTT,
                                 EVENT_MQTT_STATE, FORMAT_MQTT_STATE, mqtt_get_session_state_name(health.state),
                                 health.is_transport_selected ? health.selected_transport.name : TRANSPORT_NONE,
                                 mqtt_get_error_category_name(health.last_error_category), health.reconnect_count,
                                 (unsigned)health.queued_event_count);
        previous_mqtt_state = health.state;
    }
}

/* Initializes the automatic-direction board UART without waiting for an attached peer. */
static void initialize_rs485(void)
{
    rs485_config_t config;
    controller_board_get_rs485_config(&config);
    config.baud_rate             = get_rs485_baud_rate();
    const bool is_platform_ready = !config.enabled || platform_rs485_initialize(&config);
    rs485_service_init(&controller_rs485_service, &config, is_platform_ready && config.enabled ? platform_rs485_write : NULL);
    initialize_controller_protocol();
    const rs485_health_t health = rs485_service_get_health(&controller_rs485_service);
    diagnostics_emit(is_platform_ready ? DIAGNOSTIC_INFO : DIAGNOSTIC_ERROR, COMPONENT_RS485, EVENT_RS485_STATE,
                     FORMAT_RS485_STATE, rs485_get_state_name(health.state), config.baud_rate, (unsigned)config.data_bits,
                     get_rs485_parity_marker(config.parity), config.stop_bits == RS485_STOP_BITS_2 ? 2U : 1U);
}

/* Drains bounded UART events and advances timeout-delimited raw framing. */
static void process_rs485(uint64_t now_ms)
{
    platform_rs485_event_t event;

    for (size_t processed = 0; processed < MAXIMUM_RS485_EVENTS_PER_TICK && platform_rs485_get_event(&event); processed++)
    {
        switch (event.type)
        {
            case PLATFORM_RS485_EVENT_DATA:
                rs485_service_receive_bytes(&controller_rs485_service, event.data, event.size, now_ms);
                break;
            case PLATFORM_RS485_EVENT_FRAMING_ERROR:
                rs485_service_report_error(&controller_rs485_service, RS485_TRANSPORT_ERROR_FRAMING);
                break;
            case PLATFORM_RS485_EVENT_PARITY_ERROR:
                rs485_service_report_error(&controller_rs485_service, RS485_TRANSPORT_ERROR_PARITY);
                break;
            case PLATFORM_RS485_EVENT_OVERFLOW:
                rs485_service_report_error(&controller_rs485_service, RS485_TRANSPORT_ERROR_OVERFLOW);
                break;
            case PLATFORM_RS485_EVENT_QUEUE_DROP:
                rs485_service_report_queue_drops(&controller_rs485_service, (uint32_t)event.size);
                break;
        }
    }

    rs485_service_process(&controller_rs485_service, now_ms);

    /* Complete transport frames enter the validated protocol codec; raw commissioning echo is no longer active. */
    rs485_frame_t frame;

    while (rs485_service_get_received(&controller_rs485_service, &frame))
    {
        controller_protocol_receive(&controller_protocol, frame.data, frame.size, now_ms);
    }

    controller_protocol_process(&controller_protocol, now_ms);
}

/* Services communications state machines and emits heartbeat status indefinitely. */
static void controller_task(void * /* context */)
{
    char status[STATUS_BUFFER_SIZE];
    uint64_t next_status_ms = platform_get_monotonic_ms();
    initialize_settings();
    initialize_terminal();
    initialize_networking();
    initialize_flow();
    initialize_mqtt();
    initialize_io();
    initialize_rs485();

    for (;;)
    {
        const uint64_t now_ms = platform_get_monotonic_ms();

        /* Ethernet callbacks are drained first so supervision sees current link state. */
        ethernet_link_process(&controller_ethernet_link);

        /* Frequent bounded processing keeps retries responsive without blocking the task. */
        network_manager_process(&controller_network_manager, now_ms);

        /* MQTT consumes only current neutral link snapshots and owned platform events. */
        process_mqtt(now_ms);

        /* Field samples are cached before protocol dispatch for coherent non-blocking reads. */
        process_io(now_ms);
        process_flow(now_ms);

        if (is_debug_ready)
        {
            /* Continuous shadow evaluation shares the monotonic supervisor loop and never blocks on protocol transfer. */
            flow_debug_process(&controller_debug, now_ms);
        }

        /* RS485 has its own bounded transport path and cannot delay network or MQTT processing. */
        process_rs485(now_ms);
        uint8_t terminal_input[TERMINAL_READ_CAPACITY];
        const size_t terminal_input_size = platform_terminal_read(terminal_input, sizeof(terminal_input));
        terminal_service_receive(&controller_terminal_service, terminal_input, terminal_input_size, now_ms);
        terminal_service_process(&controller_terminal_service, now_ms);

        if (now_ms >= next_status_ms)
        {
            const controller_health_snapshot_t snapshot = get_controller_health_snapshot();
            controller_health_format(status, sizeof(status), &snapshot);
            diagnostics_emit(DIAGNOSTIC_INFO, COMPONENT_RUNTIME, EVENT_HEARTBEAT, FORMAT_STATUS, status);
            publish_mqtt_health();
            next_status_ms = now_ms + STATUS_INTERVAL_MS;
        }

        platform_delay_ms(CONTROLLER_TICK_MS);
    }
}

/* Starts the non-blocking controller runtime task and reports creation success. */
bool controller_runtime_start(void)
{
    controller_health_init();

    return platform_start_task(CONTROLLER_TASK_NAME, controller_task, NULL, CONTROLLER_TASK_STACK_SIZE, CONTROLLER_TASK_PRIORITY);
}
