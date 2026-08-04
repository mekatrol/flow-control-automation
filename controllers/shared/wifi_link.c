#include "wifi_link.h"

#include "diagnostics.h"
#include "platform_wifi.h"

/* Processing is bounded so a noisy adapter cannot monopolize the runtime task. */
enum
{
    MAXIMUM_EVENTS_PER_TICK   = 8,
    EVENT_RATE_WINDOW_MS      = 10000,
    MAXIMUM_EVENTS_PER_WINDOW = 4,
    DHCP_TIMEOUT_MS           = 30000,
};

/* Stable diagnostic and transition values describe Wi-Fi without credentials. */
static const char COMPONENT_WIFI[]                    = "wifi";
static const char EVENT_CONFIG_INVALID[]              = "configuration_invalid";
static const char EVENT_PLATFORM_INIT_FAILED[]        = "platform_init_failed";
static const char EVENT_CONNECTION_ATTEMPT_FAILED[]   = "connection_attempt_failed";
static const char MESSAGE_CONFIG_INVALID[]            = "station disabled because configuration exceeds supported limits";
static const char MESSAGE_PLATFORM_INIT_FAILED[]      = "station disabled because platform initialization failed";
static const char MESSAGE_CONNECTION_ATTEMPT_FAILED[] = "platform rejected the non-blocking connection request";
static const char FORMAT_EVENT_STATE[]                = "state_change=%s";
static const char FORMAT_ADDRESS_READY[]              = "ipv4=%s ipv6=%s dns_ready=%u";
static const char FORMAT_FAILURE_STATE[]              = "state_change=%s reason_code=%u rssi_dbm=%d";
static const char INTERFACE_WIFI[]                    = "wifi_sta";
static const char REASON_DRIVER_STARTED[]             = "driver_started";
static const char REASON_ASSOCIATING[]                = "associating";
static const char REASON_ASSOCIATED[]                 = "associated_waiting_for_address";
static const char REASON_ADDRESS_READY[]              = "address_ready";
static const char REASON_ADDRESS_LOST[]               = "address_lost";
static const char REASON_AUTHENTICATION_FAILED[]      = "authentication_failed";
static const char REASON_ASSOCIATION_FAILED[]         = "association_failed";
static const char REASON_DRIVER_FAILED[]              = "driver_failed";
static const char REASON_STOPPED[]                    = "stopped";
static const char REASON_DHCP_TIMEOUT[]               = "dhcp_timeout";

/* Gets the stable reason associated with a platform Wi-Fi event. */
static const char *get_event_reason(wifi_platform_event_type_t type)
{
    static const char *const reasons[] = {
        REASON_DRIVER_STARTED,        REASON_ASSOCIATING,        REASON_ASSOCIATED,    REASON_ADDRESS_READY, REASON_ADDRESS_LOST,
        REASON_AUTHENTICATION_FAILED, REASON_ASSOCIATION_FAILED, REASON_DRIVER_FAILED, REASON_STOPPED,
    };
    return type <= WIFI_PLATFORM_EVENT_STOPPED ? reasons[type] : REASON_DRIVER_FAILED;
}

/* Initializes the platform station without beginning network association. */
bool wifi_link_init(wifi_link_t *wifi_link, network_manager_t *network_manager, const wifi_link_config_t *config)
{
    wifi_link->network_manager        = network_manager;
    wifi_link->next_sequence          = 0;
    wifi_link->platform_initialized   = false;
    wifi_link->event_rate_limiter     = (diagnostic_rate_limiter_t){0};
    wifi_link->is_waiting_for_address = false;
    wifi_link->address_deadline_ms    = 0;
    if (!is_wifi_link_config_valid(config))
    {
        diagnostics_emit(DIAGNOSTIC_ERROR, COMPONENT_WIFI, EVENT_CONFIG_INVALID, MESSAGE_CONFIG_INVALID);
        return false;
    }
    if (!is_wifi_link_config_enabled(config))
    {
        return true;
    }
    /* Platform initialization is bounded and deliberately does not associate yet. */
    wifi_link->platform_initialized = platform_wifi_initialize(config);
    if (!wifi_link->platform_initialized)
    {
        diagnostics_emit(DIAGNOSTIC_ERROR, COMPONENT_WIFI, EVENT_PLATFORM_INIT_FAILED, MESSAGE_PLATFORM_INIT_FAILED);
    }
    return wifi_link->platform_initialized;
}

/* Enqueues a locally generated failure when a platform request cannot start. */
static void enqueue_start_failure(wifi_link_t *wifi_link)
{
    const network_event_t event = {
        .link_id        = NETWORK_LINK_WIFI,
        .type           = NETWORK_EVENT_FAILED,
        .sequence       = ++wifi_link->next_sequence,
        .interface_name = INTERFACE_WIFI,
        .reason         = REASON_DRIVER_FAILED,
    };
    (void)network_manager_enqueue_event(wifi_link->network_manager, &event);
    diagnostics_emit(DIAGNOSTIC_ERROR, COMPONENT_WIFI, EVENT_CONNECTION_ATTEMPT_FAILED, MESSAGE_CONNECTION_ATTEMPT_FAILED);
}

/* Requests one bounded connection attempt from the platform adapter. */
void wifi_link_start(wifi_link_t *wifi_link)
{
    if (!wifi_link->platform_initialized || !platform_wifi_start())
    {
        enqueue_start_failure(wifi_link);
    }
}

/* Stops the platform station without affecting other network links. */
void wifi_link_stop(wifi_link_t *wifi_link)
{
    if (wifi_link->platform_initialized)
    {
        platform_wifi_stop();
    }
}

/* Drains bounded platform events into the neutral network manager queue. */
void wifi_link_process(wifi_link_t *wifi_link, uint64_t now_ms)
{
    wifi_platform_event_t platform_event;
    for (size_t processed = 0; processed < MAXIMUM_EVENTS_PER_TICK; processed++)
    {
        if (!platform_wifi_get_event(&platform_event))
        {
            break;
        }
        if (platform_event.type == WIFI_PLATFORM_EVENT_DRIVER_STARTED && !platform_wifi_connect())
        {
            /* Association runs here, never in the ESP-IDF callback task. */
            enqueue_start_failure(wifi_link);
            continue;
        }
        const network_event_t event = {
            .link_id        = NETWORK_LINK_WIFI,
            .type           = wifi_link_get_network_event_type(platform_event.type),
            .sequence       = ++wifi_link->next_sequence,
            .interface_name = INTERFACE_WIFI,
            .ipv4_address   = platform_event.ipv4_address,
            .ipv6_address   = platform_event.ipv6_address,
            .dns_ready      = platform_event.dns_ready,
            .reason         = get_event_reason(platform_event.type),
        };
        if (platform_event.type == WIFI_PLATFORM_EVENT_ASSOCIATED)
        {
            /* DHCP receives a bounded window after association before supervision retries. */
            wifi_link->is_waiting_for_address = true;
            wifi_link->address_deadline_ms    = now_ms + DHCP_TIMEOUT_MS;
        }
        else if (platform_event.type == WIFI_PLATFORM_EVENT_ADDRESS_READY ||
                 platform_event.type == WIFI_PLATFORM_EVENT_ADDRESS_LOST || event.type == NETWORK_EVENT_FAILED)
        {
            wifi_link->is_waiting_for_address = false;
        }
        const diagnostic_severity_t severity = event.type == NETWORK_EVENT_FAILED || event.type == NETWORK_EVENT_CONNECTION_LOST
                                                   ? DIAGNOSTIC_WARNING
                                                   : DIAGNOSTIC_INFO;
        /* Address diagnostics expose interface allocation without logging credentials. */
        if (platform_event.type == WIFI_PLATFORM_EVENT_ADDRESS_READY)
        {
            diagnostics_emit_limited(&wifi_link->event_rate_limiter, EVENT_RATE_WINDOW_MS, MAXIMUM_EVENTS_PER_WINDOW, severity,
                                     COMPONENT_WIFI, get_event_reason(platform_event.type), FORMAT_ADDRESS_READY,
                                     platform_event.ipv4_address, platform_event.ipv6_address,
                                     platform_event.dns_ready ? 1U : 0U);
        }
        else if (event.type == NETWORK_EVENT_FAILED)
        {
            diagnostics_emit_limited(&wifi_link->event_rate_limiter, EVENT_RATE_WINDOW_MS, MAXIMUM_EVENTS_PER_WINDOW, severity,
                                     COMPONENT_WIFI, get_event_reason(platform_event.type), FORMAT_FAILURE_STATE,
                                     get_event_reason(platform_event.type), (unsigned)platform_event.reason_code,
                                     (int)platform_event.rssi_dbm);
        }
        else
        {
            /* Each failure category remains distinct while limiting a noisy link. */
            diagnostics_emit_limited(&wifi_link->event_rate_limiter, EVENT_RATE_WINDOW_MS, MAXIMUM_EVENTS_PER_WINDOW, severity,
                                     COMPONENT_WIFI, get_event_reason(platform_event.type), FORMAT_EVENT_STATE,
                                     get_event_reason(platform_event.type));
        }
        /* The neutral queue owns copied strings, so this stack event is safe. */
        (void)network_manager_enqueue_event(wifi_link->network_manager, &event);
    }
    if (wifi_link->is_waiting_for_address && now_ms >= wifi_link->address_deadline_ms)
    {
        const network_event_t timeout = {
            .link_id        = NETWORK_LINK_WIFI,
            .type           = NETWORK_EVENT_FAILED,
            .sequence       = ++wifi_link->next_sequence,
            .interface_name = INTERFACE_WIFI,
            .reason         = REASON_DHCP_TIMEOUT,
        };
        wifi_link->is_waiting_for_address = false;
        (void)network_manager_enqueue_event(wifi_link->network_manager, &timeout);
        diagnostics_emit(DIAGNOSTIC_WARNING, COMPONENT_WIFI, REASON_DHCP_TIMEOUT, FORMAT_EVENT_STATE, REASON_DHCP_TIMEOUT);
    }
}

/* Enables or disables Wi-Fi for later maintenance and configuration commands. */
void wifi_link_set_enabled(wifi_link_t *wifi_link, bool enabled, uint64_t now_ms)
{
    network_manager_set_enabled(wifi_link->network_manager, NETWORK_LINK_WIFI, enabled && wifi_link->platform_initialized,
                                now_ms);
}

/* Requests an immediate supervised reconnect for later maintenance commands. */
void wifi_link_reconnect(wifi_link_t *wifi_link, uint64_t now_ms)
{
    if (wifi_link->platform_initialized)
    {
        network_manager_reconnect(wifi_link->network_manager, NETWORK_LINK_WIFI, now_ms);
    }
}
