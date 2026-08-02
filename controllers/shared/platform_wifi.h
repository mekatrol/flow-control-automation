#pragma once

#include <stdbool.h>
#include <stdint.h>

#include "wifi_link.h"

/* Gets typed Wi-Fi settings from the platform configuration source. */
void platform_wifi_get_config(wifi_link_config_t *config);

/* Initializes persistence, interfaces, events, and station driver without connecting. */
bool platform_wifi_initialize(const wifi_link_config_t *config);

/* Requests one asynchronous station association attempt. */
bool platform_wifi_start(void);

/* Requests association after the station driver has reported that it started. */
bool platform_wifi_connect(void);

/* Stops the station driver and clears its current association. */
void platform_wifi_stop(void);

/* Gets one owned event without blocking, or reports that the queue is empty. */
bool platform_wifi_get_event(wifi_platform_event_t *event);

/* Gets platform entropy for supervisor retry jitter. */
uint32_t platform_wifi_get_random_u32(void);
