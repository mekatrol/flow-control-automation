#pragma once

#include <stdbool.h>
#include <stddef.h>
#include <stdint.h>

#include "diagnostics/core.h"
#include "network/manager.h"

/* Wi-Fi configuration limits match the IEEE SSID and ESP-IDF station limits. */
#define WIFI_SSID_MAX_LENGTH 32
#define WIFI_PASSWORD_MAX_LENGTH 63
#define WIFI_HOSTNAME_MAX_LENGTH 32

/* Portable power-save choices keep policy out of the ESP-IDF adapter. */
typedef enum
{
    WIFI_POWER_SAVE_DISABLED,
    WIFI_POWER_SAVE_MINIMUM_MODEM,
} wifi_power_save_t;

/* Typed Wi-Fi station settings consumed by shared validation and the adapter. */
typedef struct
{
    const char *ssid;
    const char *password;
    const char *hostname;
    wifi_power_save_t power_save;
} wifi_link_config_t;

/* Platform event categories preserve Wi-Fi failure detail before neutral mapping. */
typedef enum
{
    WIFI_PLATFORM_EVENT_DRIVER_STARTED,
    WIFI_PLATFORM_EVENT_ASSOCIATING,
    WIFI_PLATFORM_EVENT_ASSOCIATED,
    WIFI_PLATFORM_EVENT_ADDRESS_READY,
    WIFI_PLATFORM_EVENT_ADDRESS_LOST,
    WIFI_PLATFORM_EVENT_AUTHENTICATION_FAILED,
    WIFI_PLATFORM_EVENT_ASSOCIATION_FAILED,
    WIFI_PLATFORM_EVENT_DRIVER_FAILED,
    WIFI_PLATFORM_EVENT_STOPPED,
} wifi_platform_event_type_t;

/* Owned platform event data is safe after an ESP-IDF callback returns. */
typedef struct
{
    wifi_platform_event_type_t type;
    uint16_t reason_code;
    int8_t rssi_dbm;
    char ipv4_address[NETWORK_ADDRESS_MAX];
    char ipv6_address[NETWORK_ADDRESS_MAX];
    bool dns_ready;
} wifi_platform_event_t;

/* Shared Wi-Fi state binds platform events to the neutral network manager. */
typedef struct
{
    network_manager_t *network_manager;
    uint32_t next_sequence;
    bool platform_initialized;
    diagnostic_rate_limiter_t event_rate_limiter;
    bool is_waiting_for_address;
    uint64_t address_deadline_ms;
} wifi_link_t;

/* Tests whether typed Wi-Fi settings are safe and supported. */
bool is_wifi_link_config_valid(const wifi_link_config_t *config);

/* Tests whether an empty SSID intentionally disables the station. */
bool is_wifi_link_config_enabled(const wifi_link_config_t *config);

/* Gets the neutral event type corresponding to a platform Wi-Fi event. */
network_event_type_t wifi_link_get_network_event_type(wifi_platform_event_type_t platform_type);

/* Initializes the platform station without beginning network association. */
bool wifi_link_init(wifi_link_t *wifi_link, network_manager_t *network_manager, const wifi_link_config_t *config);

/* Requests one bounded connection attempt from the platform adapter. */
void wifi_link_start(wifi_link_t *wifi_link);

/* Stops the platform station without affecting other network links. */
void wifi_link_stop(wifi_link_t *wifi_link);

/* Drains bounded platform events into the neutral network manager queue. */
void wifi_link_process(wifi_link_t *wifi_link, uint64_t now_ms);

/* Enables or disables Wi-Fi for later maintenance and configuration commands. */
void wifi_link_set_enabled(wifi_link_t *wifi_link, bool enabled, uint64_t now_ms);

/* Requests an immediate supervised reconnect for later maintenance commands. */
void wifi_link_reconnect(wifi_link_t *wifi_link, uint64_t now_ms);
