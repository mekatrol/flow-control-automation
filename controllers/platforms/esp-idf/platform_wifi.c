#include "platform/wifi.h"

#include <stdio.h>
#include <string.h>

#include "board/config.h"
#include "esp_event.h"
#include "esp_netif.h"
#include "esp_netif_ip_addr.h"
#include "esp_random.h"
#include "esp_system.h"
#include "esp_wifi.h"
#include "freertos/FreeRTOS.h"
#include "freertos/queue.h"
#include "nvs_flash.h"
#include "sdkconfig.h"

/* The callback queue is fixed so driver event storms cannot allocate indefinitely. */
enum
{
    WIFI_EVENT_QUEUE_DEPTH = 16
};

static QueueHandle_t wifi_event_queue;
static esp_netif_t *wifi_network_interface;
static bool is_driver_started;

/* Tests whether an ESP-IDF disconnect reason represents authentication failure. */
static bool is_authentication_failure(uint16_t reason)
{
    switch (reason)
    {
        case WIFI_REASON_AUTH_EXPIRE:
        case WIFI_REASON_AUTH_LEAVE:
        case WIFI_REASON_4WAY_HANDSHAKE_TIMEOUT:
        case WIFI_REASON_GROUP_KEY_UPDATE_TIMEOUT:
        case WIFI_REASON_802_1X_AUTH_FAILED:
        case WIFI_REASON_AUTH_FAIL:
        case WIFI_REASON_HANDSHAKE_TIMEOUT:
            return true;
        default:
            return false;
    }
}

/* Copies one event into the queue without blocking the ESP-IDF event task. */
static void enqueue_platform_event(const wifi_platform_event_t *event)
{
    if (wifi_event_queue != NULL)
    {
        (void)xQueueSend(wifi_event_queue, event, 0);
    }
}

/* Gets whether DHCP installed a usable primary DNS server for this interface. */
static bool is_dns_ready(void)
{
    esp_netif_dns_info_t dns = {0};
    if (esp_netif_get_dns_info(wifi_network_interface, ESP_NETIF_DNS_MAIN, &dns) != ESP_OK)
    {
        return false;
    }
    return dns.ip.type == ESP_IPADDR_TYPE_V4 && dns.ip.u_addr.ip4.addr != 0;
}

/* Converts ESP-IDF Wi-Fi callbacks into owned, bounded platform events. */
static void handle_wifi_event(void * /* context */, esp_event_base_t event_base, int32_t event_id, void *event_data)
{
    if (event_base != WIFI_EVENT)
    {
        return;
    }
    wifi_platform_event_t event = {0};
    switch (event_id)
    {
        case WIFI_EVENT_STA_START:
            is_driver_started = true;
            event.type        = WIFI_PLATFORM_EVENT_DRIVER_STARTED;
            break;
        case WIFI_EVENT_STA_CONNECTED:
            event.type = WIFI_PLATFORM_EVENT_ASSOCIATED;
            break;
        case WIFI_EVENT_STA_DISCONNECTED: {
            const wifi_event_sta_disconnected_t *disconnected = event_data;
            event.reason_code                                 = disconnected->reason;
            event.rssi_dbm                                    = disconnected->rssi;
            event.type = is_authentication_failure(disconnected->reason) ? WIFI_PLATFORM_EVENT_AUTHENTICATION_FAILED
                                                                         : WIFI_PLATFORM_EVENT_ASSOCIATION_FAILED;
            break;
        }
        case WIFI_EVENT_STA_STOP:
            is_driver_started = false;
            event.type        = WIFI_PLATFORM_EVENT_STOPPED;
            break;
        default:
            return;
    }
    enqueue_platform_event(&event);
}

/* Converts ESP-IDF address callbacks into owned, bounded platform events. */
static void handle_ip_event(void * /* context */, esp_event_base_t event_base, int32_t event_id, void *event_data)
{
    if (event_base != IP_EVENT)
    {
        return;
    }
    wifi_platform_event_t event = {0};
    if (event_id == IP_EVENT_STA_GOT_IP)
    {
        const ip_event_got_ip_t *got_ip = event_data;
        if (got_ip->esp_netif != wifi_network_interface)
        {
            return;
        }
        event.type      = WIFI_PLATFORM_EVENT_ADDRESS_READY;
        event.dns_ready = is_dns_ready();
        (void)snprintf(event.ipv4_address, sizeof(event.ipv4_address), IPSTR, IP2STR(&got_ip->ip_info.ip));
    }
    else if (event_id == IP_EVENT_GOT_IP6)
    {
        const ip_event_got_ip6_t *got_ip6 = event_data;
        if (got_ip6->esp_netif != wifi_network_interface)
        {
            return;
        }
        event.type      = WIFI_PLATFORM_EVENT_ADDRESS_READY;
        event.dns_ready = is_dns_ready();
        (void)snprintf(event.ipv6_address, sizeof(event.ipv6_address), IPV6STR, IPV62STR(got_ip6->ip6_info.ip));
    }
    else if (event_id == IP_EVENT_STA_LOST_IP)
    {
        event.type = WIFI_PLATFORM_EVENT_ADDRESS_LOST;
    }
    else
    {
        return;
    }
    enqueue_platform_event(&event);
}

/* Initializes NVS and repairs the two documented recoverable initialization errors. */
static bool initialize_persistence(void)
{
    esp_err_t result = nvs_flash_init();
    if (result == ESP_ERR_NVS_NO_FREE_PAGES || result == ESP_ERR_NVS_NEW_VERSION_FOUND)
    {
        /* Erasing is required because neither error can be repaired in place. */
        if (nvs_flash_erase() != ESP_OK)
        {
            return false;
        }
        result = nvs_flash_init();
    }
    return result == ESP_OK;
}

/* Gets typed Wi-Fi settings from the persistent settings snapshot. */
void platform_wifi_get_config(wifi_link_config_t *config, const controller_settings_t *settings)
{
    *config = (wifi_link_config_t){
        .ssid     = settings != NULL && settings->wifi_ssid.is_set ? settings->wifi_ssid.value : "",
        .password = settings != NULL && settings->wifi_password.is_set ? settings->wifi_password.value : "",
        .hostname = settings != NULL && settings->hostname.is_set ? settings->hostname.value : get_controller_default_hostname(),
        .power_save = CONFIG_CONTROLLER_WIFI_POWER_SAVE ? WIFI_POWER_SAVE_MINIMUM_MODEM : WIFI_POWER_SAVE_DISABLED,
    };
}

/* Initializes persistence, interfaces, events, and station driver without connecting. */
bool platform_wifi_initialize(const wifi_link_config_t *config)
{
    if (!initialize_persistence())
    {
        return false;
    }
    esp_err_t result = esp_netif_init();
    if (result != ESP_OK && result != ESP_ERR_INVALID_STATE)
    {
        return false;
    }
    result = esp_event_loop_create_default();
    if (result != ESP_OK && result != ESP_ERR_INVALID_STATE)
    {
        return false;
    }
    wifi_event_queue = xQueueCreate(WIFI_EVENT_QUEUE_DEPTH, sizeof(wifi_platform_event_t));
    if (wifi_event_queue == NULL)
    {
        return false;
    }
    wifi_network_interface = esp_netif_create_default_wifi_sta();
    if (wifi_network_interface == NULL)
    {
        return false;
    }
    if (esp_netif_set_hostname(wifi_network_interface, config->hostname) != ESP_OK)
    {
        return false;
    }

    wifi_init_config_t initialization = WIFI_INIT_CONFIG_DEFAULT();
    if (esp_wifi_init(&initialization) != ESP_OK)
    {
        return false;
    }
    if (esp_event_handler_register(WIFI_EVENT, ESP_EVENT_ANY_ID, handle_wifi_event, NULL) != ESP_OK)
    {
        return false;
    }
    if (esp_event_handler_register(IP_EVENT, ESP_EVENT_ANY_ID, handle_ip_event, NULL) != ESP_OK)
    {
        return false;
    }

    wifi_config_t station = {0};
    /* Length validation permits a full 32-byte SSID, which need not be terminated. */
    (void)memcpy(station.sta.ssid, config->ssid, strlen(config->ssid));
    (void)memcpy(station.sta.password, config->password, strlen(config->password));
    /* Match ESP-IDF station guidance: protected credentials require WPA2 or better. */
    station.sta.threshold.authmode = config->password[0] != '\0' ? WIFI_AUTH_WPA2_PSK : WIFI_AUTH_OPEN;
    if (esp_wifi_set_storage(WIFI_STORAGE_RAM) != ESP_OK || esp_wifi_set_mode(WIFI_MODE_STA) != ESP_OK ||
        esp_wifi_set_config(WIFI_IF_STA, &station) != ESP_OK)
    {
        return false;
    }
    const wifi_ps_type_t power_save = config->power_save == WIFI_POWER_SAVE_MINIMUM_MODEM ? WIFI_PS_MIN_MODEM : WIFI_PS_NONE;
    return esp_wifi_set_ps(power_save) == ESP_OK;
}

/* Requests one asynchronous station association attempt. */
bool platform_wifi_start(void)
{
    if (is_driver_started)
    {
        return esp_wifi_connect() == ESP_OK;
    }
    return esp_wifi_start() == ESP_OK;
}

/* Requests association after the station driver has reported that it started. */
bool platform_wifi_connect(void)
{
    if (!is_driver_started)
    {
        return false;
    }
    wifi_platform_event_t event = {.type = WIFI_PLATFORM_EVENT_ASSOCIATING};
    enqueue_platform_event(&event);
    return esp_wifi_connect() == ESP_OK;
}

/* Stops the station driver and clears its current association. */
void platform_wifi_stop(void)
{
    if (is_driver_started)
    {
        (void)esp_wifi_stop();
    }
}

/* Gets one owned event without blocking, or reports that the queue is empty. */
bool platform_wifi_get_event(wifi_platform_event_t *event)
{
    return wifi_event_queue != NULL && xQueueReceive(wifi_event_queue, event, 0) == pdTRUE;
}

/* Gets ESP32 hardware entropy for supervisor retry jitter. */
uint32_t platform_wifi_get_random_u32(void)
{
    return esp_random();
}
