#include "ethernet/link.h"

#include "diagnostics/service.h"
#include "platform/ethernet.h"

/* Event processing and diagnostic emission remain bounded during cable noise. */
enum
{
    MAXIMUM_EVENTS_PER_TICK   = 8,
    EVENT_RATE_WINDOW_MS      = 10000,
    MAXIMUM_EVENTS_PER_WINDOW = 4,
};

/* Stable Ethernet diagnostics expose state and addresses without driver types. */
static const char COMPONENT_ETHERNET[]   = "ethernet";
static const char INTERFACE_ETHERNET[]   = "w5500";
static const char EVENT_INIT_FAILED[]    = "platform_init_failed";
static const char EVENT_START_FAILED[]   = "driver_start_failed";
static const char MESSAGE_INIT_FAILED[]  = "W5500 initialization failed";
static const char MESSAGE_START_FAILED[] = "W5500 driver start failed";
static const char FORMAT_STATE[]         = "state_change=%s";
static const char FORMAT_ADDRESS[]       = "ipv4=%s ipv6=%s dns_ready=%u";
static const char REASON_STARTED[]       = "driver_started";
static const char REASON_LINK_UP[]       = "link_up_waiting_for_address";
static const char REASON_ADDRESS_READY[] = "address_ready";
static const char REASON_ADDRESS_LOST[]  = "address_lost";
static const char REASON_LINK_DOWN[]     = "link_down";
static const char REASON_DRIVER_FAILED[] = "driver_failed";
static const char REASON_STOPPED[]       = "stopped";

/* Gets the stable diagnostic reason associated with an Ethernet event. */
static const char *get_event_reason(ethernet_platform_event_type_t type)
{
    static const char *const reasons[] = {
        REASON_STARTED,   REASON_LINK_UP,       REASON_ADDRESS_READY, REASON_ADDRESS_LOST,
        REASON_LINK_DOWN, REASON_DRIVER_FAILED, REASON_STOPPED,
    };
    return type <= ETHERNET_PLATFORM_EVENT_STOPPED ? reasons[type] : REASON_DRIVER_FAILED;
}

/* Initializes the W5500 adapter without waiting for link or DHCP. */
bool ethernet_link_init(ethernet_link_t *ethernet_link, network_manager_t *network_manager, const ethernet_link_config_t *config)
{
    *ethernet_link = (ethernet_link_t){.network_manager = network_manager};
    if (!is_ethernet_link_config_valid(config) || !config->enabled)
    {
        return false;
    }
    ethernet_link->platform_initialized = platform_ethernet_initialize(config);

    if (!ethernet_link->platform_initialized)
    {
        diagnostics_emit(DIAGNOSTIC_ERROR, COMPONENT_ETHERNET, EVENT_INIT_FAILED, MESSAGE_INIT_FAILED);
    }
    return ethernet_link->platform_initialized;
}

/* Enqueues a driver failure for supervised retry outside platform callbacks. */
static void enqueue_driver_failure(ethernet_link_t *ethernet_link)
{
    const network_event_t event = {
        .link_id        = NETWORK_LINK_ETHERNET,
        .type           = NETWORK_EVENT_FAILED,
        .sequence       = ++ethernet_link->next_sequence,
        .interface_name = INTERFACE_ETHERNET,
        .reason         = REASON_DRIVER_FAILED,
    };
    network_manager_enqueue_event(ethernet_link->network_manager, &event);
}

/* Requests one non-blocking W5500 driver start attempt. */
void ethernet_link_start(ethernet_link_t *ethernet_link)
{
    if (!ethernet_link->platform_initialized || !platform_ethernet_start())
    {
        enqueue_driver_failure(ethernet_link);
        diagnostics_emit(DIAGNOSTIC_ERROR, COMPONENT_ETHERNET, EVENT_START_FAILED, MESSAGE_START_FAILED);
    }
}

/* Stops Ethernet without affecting another network link. */
void ethernet_link_stop(ethernet_link_t *ethernet_link)
{
    if (ethernet_link->platform_initialized)
    {
        platform_ethernet_stop();
    }
}

/* Drains bounded platform events into the neutral network manager. */
void ethernet_link_process(ethernet_link_t *ethernet_link)
{
    ethernet_platform_event_t platform_event;

    for (size_t processed = 0; processed < MAXIMUM_EVENTS_PER_TICK; processed++)
    {
        if (!platform_ethernet_get_event(&platform_event))
        {
            return;
        }
        const network_event_t event = {
            .link_id        = NETWORK_LINK_ETHERNET,
            .type           = ethernet_link_get_network_event_type(platform_event.type),
            .sequence       = ++ethernet_link->next_sequence,
            .interface_name = INTERFACE_ETHERNET,
            .ipv4_address   = platform_event.ipv4_address,
            .ipv6_address   = platform_event.ipv6_address,
            .dns_ready      = platform_event.dns_ready,
            .reason         = get_event_reason(platform_event.type),
        };

        if (platform_event.type == ETHERNET_PLATFORM_EVENT_ADDRESS_READY)
        {
            diagnostics_emit_limited(&ethernet_link->event_rate_limiter, EVENT_RATE_WINDOW_MS, MAXIMUM_EVENTS_PER_WINDOW,
                                     DIAGNOSTIC_INFO, COMPONENT_ETHERNET, REASON_ADDRESS_READY, FORMAT_ADDRESS,
                                     platform_event.ipv4_address, platform_event.ipv6_address,
                                     platform_event.dns_ready ? 1U : 0U);
        }
        else
        {
            const diagnostic_severity_t severity =
                event.type == NETWORK_EVENT_FAILED || event.type == NETWORK_EVENT_CONNECTION_LOST ? DIAGNOSTIC_WARNING
                                                                                                  : DIAGNOSTIC_INFO;
            diagnostics_emit_limited(&ethernet_link->event_rate_limiter, EVENT_RATE_WINDOW_MS, MAXIMUM_EVENTS_PER_WINDOW,
                                     severity, COMPONENT_ETHERNET, get_event_reason(platform_event.type), FORMAT_STATE,
                                     get_event_reason(platform_event.type));
        }
        network_manager_enqueue_event(ethernet_link->network_manager, &event);
    }
}
