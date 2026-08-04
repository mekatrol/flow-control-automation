#pragma once

#include "mqtt_service.h"
#include "settings_service.h"

/* Gets typed MQTT settings using credentials from the persistent snapshot. */
void platform_mqtt_get_config(mqtt_broker_config_t *config, const controller_settings_t *settings);

/* Initializes bounded platform MQTT callback storage without contacting a broker. */
bool platform_mqtt_initialize(void);

/* Gets an eligible IP route using the configured Wi-Fi/Ethernet policy. */
bool platform_mqtt_get_transport_route(mqtt_transport_route_t *route, void *context);

/* Starts one asynchronous ESP-MQTT session bound to the selected interface. */
bool platform_mqtt_connect(const mqtt_broker_config_t *config, const mqtt_transport_route_t *route, void *context);

/* Stops and releases the current ESP-MQTT session. */
void platform_mqtt_disconnect(void *context);

/* Gets one owned transport event without blocking, or reports an empty queue. */
bool platform_mqtt_get_event(mqtt_queued_event_t *event);

/* Replays subscriptions registered by the future bidirectional MQTT API. */
void platform_mqtt_replay_subscriptions(void *context);

/* Gets the configured broker username for use only by the MQTT transport adapter. */
const char *platform_mqtt_get_username(void);

/* Gets the configured broker password for use only by the MQTT transport adapter. */
const char *platform_mqtt_get_password(void);
