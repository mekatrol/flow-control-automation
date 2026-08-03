#pragma once

#include <stdbool.h>
#include <stdint.h>

#include "diagnostics_core.h"
#include "network_manager.h"

/* Typed board configuration keeps W5500 wiring out of shared service logic. */
typedef struct
{
    bool enabled;
    int clock_gpio;
    int mosi_gpio;
    int miso_gpio;
    int chip_select_gpio;
    int interrupt_gpio;
    int reset_gpio;
    uint32_t spi_clock_hz;
    const char *hostname;
} ethernet_link_config_t;

/* Platform event categories preserve Ethernet detail before neutral mapping. */
typedef enum
{
    ETHERNET_PLATFORM_EVENT_DRIVER_STARTED,
    ETHERNET_PLATFORM_EVENT_LINK_UP,
    ETHERNET_PLATFORM_EVENT_ADDRESS_READY,
    ETHERNET_PLATFORM_EVENT_ADDRESS_LOST,
    ETHERNET_PLATFORM_EVENT_LINK_DOWN,
    ETHERNET_PLATFORM_EVENT_DRIVER_FAILED,
    ETHERNET_PLATFORM_EVENT_STOPPED,
} ethernet_platform_event_type_t;

/* Owned platform event data remains safe after an ESP-IDF callback returns. */
typedef struct
{
    ethernet_platform_event_type_t type;
    char ipv4_address[NETWORK_ADDRESS_MAX];
    char ipv6_address[NETWORK_ADDRESS_MAX];
    bool dns_ready;
} ethernet_platform_event_t;

/* Shared Ethernet state binds platform events to the neutral manager. */
typedef struct
{
    network_manager_t *network_manager;
    uint32_t next_sequence;
    bool platform_initialized;
    diagnostic_rate_limiter_t event_rate_limiter;
} ethernet_link_t;

/* Tests whether the board-provided Ethernet configuration is usable. */
bool is_ethernet_link_config_valid(const ethernet_link_config_t *config);

/* Gets the neutral event type corresponding to a platform Ethernet event. */
network_event_type_t ethernet_link_get_network_event_type(ethernet_platform_event_type_t platform_type);

/* Initializes the W5500 adapter without waiting for link or DHCP. */
bool ethernet_link_init(ethernet_link_t *ethernet_link, network_manager_t *network_manager, const ethernet_link_config_t *config);

/* Requests one non-blocking W5500 driver start attempt. */
void ethernet_link_start(ethernet_link_t *ethernet_link);

/* Stops Ethernet without affecting another network link. */
void ethernet_link_stop(ethernet_link_t *ethernet_link);

/* Drains bounded platform events into the neutral network manager. */
void ethernet_link_process(ethernet_link_t *ethernet_link);
