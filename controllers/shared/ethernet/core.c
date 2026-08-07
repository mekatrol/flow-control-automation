#include "ethernet/link.h"

/* Tests whether the board-provided Ethernet configuration is usable. */
bool is_ethernet_link_config_valid(const ethernet_link_config_t *config)
{
    return config != NULL && config->hostname != NULL && config->hostname[0] != '\0' && config->clock_gpio >= 0 &&
           config->mosi_gpio >= 0 && config->miso_gpio >= 0 && config->chip_select_gpio >= 0 && config->interrupt_gpio >= 0 &&
           config->reset_gpio >= 0 && config->spi_clock_hz > 0;
}

/* Gets the neutral event type corresponding to a platform Ethernet event. */
network_event_type_t ethernet_link_get_network_event_type(ethernet_platform_event_type_t platform_type)
{
    switch (platform_type)
    {
        case ETHERNET_PLATFORM_EVENT_DRIVER_STARTED:
            return NETWORK_EVENT_STARTED;
        case ETHERNET_PLATFORM_EVENT_LINK_UP:
            return NETWORK_EVENT_CONNECTING;
        case ETHERNET_PLATFORM_EVENT_ADDRESS_READY:
            return NETWORK_EVENT_ONLINE;
        case ETHERNET_PLATFORM_EVENT_ADDRESS_LOST:
        case ETHERNET_PLATFORM_EVENT_LINK_DOWN:
            return NETWORK_EVENT_CONNECTION_LOST;
        case ETHERNET_PLATFORM_EVENT_DRIVER_FAILED:
            return NETWORK_EVENT_FAILED;
        case ETHERNET_PLATFORM_EVENT_STOPPED:
            return NETWORK_EVENT_STOPPED;
        default:
            return NETWORK_EVENT_FAILED;
    }
}
