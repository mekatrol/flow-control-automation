#include "controller_runtime.h"

#include <stdio.h>

#include "board.h"
#include "controller_health.h"
#include "diagnostics.h"
#include "ethernet_link.h"
#include "network_manager.h"
#include "platform.h"
#include "platform_mqtt.h"
#include "platform_settings.h"
#include "platform_terminal.h"
#include "terminal_service.h"

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
};

/* Runtime diagnostic identifiers define the stable heartbeat event schema. */
static const char CONTROLLER_TASK_NAME[]  = "controller_runtime";
static const char COMPONENT_RUNTIME[]     = "runtime";
static const char EVENT_HEARTBEAT[]       = "heartbeat";
static const char FORMAT_STATUS[]         = "%s";
static const char COMPONENT_MQTT[]        = "mqtt";
static const char EVENT_MQTT_CONFIG[]     = "configuration_invalid";
static const char EVENT_MQTT_STATE[]      = "state_change";
static const char MESSAGE_MQTT_CONFIG[]   = "MQTT host requires a valid client ID and reconnect limits";
static const char FORMAT_MQTT_STATE[]     = "state=%s transport=%s error=%s reconnect_count=%u queue_depth=%u";
static const char TRANSPORT_NONE[]        = "none";
static const char COMPONENT_SETTINGS[]    = "settings";
static const char EVENT_SETTINGS_STATE[]  = "state";
static const char FORMAT_SETTINGS_STATE[] = "state=%s schema=%u generation=%u";

static network_manager_t controller_network_manager;
static ethernet_link_t controller_ethernet_link;
static mqtt_service_t controller_mqtt_service;
static diagnostic_rate_limiter_t mqtt_event_rate_limiter;
static mqtt_session_state_t previous_mqtt_state = MQTT_SESSION_DISABLED;
static settings_service_t controller_settings_service;
static controller_settings_t controller_settings_snapshot;
static terminal_service_t controller_terminal_service;
static platform_settings_result_t platform_settings_result = PLATFORM_SETTINGS_DISABLED;

/* Writes terminal output through the board-selected non-blocking transport. */
static bool write_terminal(void * /* context */, const char *data, size_t size)
{
    return platform_terminal_write(data, size);
}

/* Formats a redacted device snapshot from portable board, platform, and subsystem contracts. */
static void get_terminal_system_info(void * /* context */, char *output, size_t capacity)
{
    platform_startup_info_t startup;
    char health[STATUS_BUFFER_SIZE];
    platform_get_startup_info(&startup);
    const controller_health_snapshot_t snapshot = get_controller_health_snapshot();
    (void)controller_health_format(health, sizeof(health), &snapshot);
    (void)snprintf(output, capacity, "device=%s firmware=%s version=%s hardware=%s processor=%s %s buses=unsupported",
                   get_controller_board_name(), startup.firmware_name, startup.firmware_version, get_controller_board_name(),
                   startup.processor, health);
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
    const bool is_mqtt_platform_ready = platform_mqtt_initialize();
    mqtt_service_init(&controller_mqtt_service, &mqtt_config, platform_mqtt_get_transport_route,
                      is_mqtt_platform_ready ? platform_mqtt_connect : NULL, platform_mqtt_disconnect,
                      platform_mqtt_replay_subscriptions, get_communications_random, &controller_network_manager);
    previous_mqtt_state = controller_mqtt_service.state;
    if (mqtt_config.enabled && (!is_mqtt_platform_ready || controller_mqtt_service.state == MQTT_SESSION_DISABLED))
    {
        diagnostics_emit(DIAGNOSTIC_ERROR, COMPONENT_MQTT, EVENT_MQTT_CONFIG, MESSAGE_MQTT_CONFIG);
    }
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
        (void)mqtt_service_enqueue_event(&controller_mqtt_service, &event);
    }
    mqtt_service_process(&controller_mqtt_service, now_ms);
    const mqtt_session_health_t health = mqtt_service_get_health(&controller_mqtt_service);
    if (health.state != previous_mqtt_state)
    {
        diagnostics_emit_limited(&mqtt_event_rate_limiter, MQTT_EVENT_RATE_WINDOW_MS, MAXIMUM_MQTT_EVENTS_PER_WINDOW,
                                 health.state == MQTT_SESSION_ONLINE ? DIAGNOSTIC_INFO : DIAGNOSTIC_WARNING, COMPONENT_MQTT,
                                 EVENT_MQTT_STATE, FORMAT_MQTT_STATE, mqtt_get_session_state_name(health.state),
                                 health.is_transport_selected ? health.selected_transport.name : TRANSPORT_NONE,
                                 mqtt_get_error_category_name(health.last_error_category), health.reconnect_count,
                                 (unsigned)health.queued_event_count);
        previous_mqtt_state = health.state;
    }
}

/* Services communications state machines and emits heartbeat status indefinitely. */
static void controller_task(void * /* context */)
{
    char status[STATUS_BUFFER_SIZE];
    uint64_t next_status_ms = platform_get_monotonic_ms();
    initialize_settings();
    initialize_terminal();
    initialize_networking();
    initialize_mqtt();
    for (;;)
    {
        const uint64_t now_ms = platform_get_monotonic_ms();
        /* Ethernet callbacks are drained first so supervision sees current link state. */
        ethernet_link_process(&controller_ethernet_link);
        /* Frequent bounded processing keeps retries responsive without blocking the task. */
        network_manager_process(&controller_network_manager, now_ms);
        /* MQTT consumes only current neutral link snapshots and owned platform events. */
        process_mqtt(now_ms);
        uint8_t terminal_input[TERMINAL_READ_CAPACITY];
        const size_t terminal_input_size = platform_terminal_read(terminal_input, sizeof(terminal_input));
        terminal_service_receive(&controller_terminal_service, terminal_input, terminal_input_size, now_ms);
        terminal_service_process(&controller_terminal_service, now_ms);
        if (now_ms >= next_status_ms)
        {
            const controller_health_snapshot_t snapshot = get_controller_health_snapshot();
            controller_health_format(status, sizeof(status), &snapshot);
            diagnostics_emit(DIAGNOSTIC_INFO, COMPONENT_RUNTIME, EVENT_HEARTBEAT, FORMAT_STATUS, status);
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
