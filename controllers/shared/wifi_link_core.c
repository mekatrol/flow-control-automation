#include "wifi_link.h"

#include <string.h>

/* Tests whether typed Wi-Fi settings are safe and supported. */
bool is_wifi_link_config_valid(const wifi_link_config_t *config)
{
    if (config == NULL || config->ssid == NULL || config->password == NULL || config->hostname == NULL)
    {
        return false;
    }
    return strlen(config->ssid) <= WIFI_SSID_MAX_LENGTH && strlen(config->password) <= WIFI_PASSWORD_MAX_LENGTH &&
           strlen(config->hostname) <= WIFI_HOSTNAME_MAX_LENGTH && config->hostname[0] != '\0';
}

/* Tests whether an empty SSID intentionally disables the station. */
bool is_wifi_link_config_enabled(const wifi_link_config_t *config)
{
    return is_wifi_link_config_valid(config) && config->ssid[0] != '\0';
}

/* Gets the neutral event type corresponding to a platform Wi-Fi event. */
network_event_type_t wifi_link_get_network_event_type(wifi_platform_event_type_t platform_type)
{
    switch (platform_type)
    {
        case WIFI_PLATFORM_EVENT_DRIVER_STARTED:
            return NETWORK_EVENT_STARTED;
        case WIFI_PLATFORM_EVENT_ASSOCIATING:
        case WIFI_PLATFORM_EVENT_ASSOCIATED:
            return NETWORK_EVENT_CONNECTING;
        case WIFI_PLATFORM_EVENT_ADDRESS_READY:
            return NETWORK_EVENT_ONLINE;
        case WIFI_PLATFORM_EVENT_ADDRESS_LOST:
            return NETWORK_EVENT_CONNECTION_LOST;
        case WIFI_PLATFORM_EVENT_AUTHENTICATION_FAILED:
        case WIFI_PLATFORM_EVENT_ASSOCIATION_FAILED:
        case WIFI_PLATFORM_EVENT_DRIVER_FAILED:
            return NETWORK_EVENT_FAILED;
        case WIFI_PLATFORM_EVENT_STOPPED:
            return NETWORK_EVENT_STOPPED;
        default:
            return NETWORK_EVENT_FAILED;
    }
}
