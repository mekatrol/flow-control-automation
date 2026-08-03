#pragma once

#include <stdbool.h>

#include "ethernet_link.h"

/* Initializes SPI, W5500, esp-netif, and callbacks without waiting for a cable. */
bool platform_ethernet_initialize(const ethernet_link_config_t *config);

/* Starts the asynchronous Ethernet driver state machine. */
bool platform_ethernet_start(void);

/* Stops the asynchronous Ethernet driver state machine. */
void platform_ethernet_stop(void);

/* Gets one owned event without blocking, or reports an empty queue. */
bool platform_ethernet_get_event(ethernet_platform_event_t *event);
