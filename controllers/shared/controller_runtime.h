#pragma once

#include <stdbool.h>

#include "mqtt_api.h"
#include "mqtt_service.h"
#include "network_manager.h"
#include "terminal_service.h"

/* Starts the non-blocking controller runtime task and reports creation success. */
bool controller_runtime_start(void);

/* Gets the runtime-owned network manager for read-only consumer discovery. */
const network_manager_t *get_controller_runtime_network_manager(void);

/* Gets the runtime-owned MQTT health snapshot for diagnostics and consumers. */
mqtt_session_health_t get_controller_runtime_mqtt_health(void);

/* Publishes through the runtime-owned bounded bidirectional MQTT API. */
mqtt_delivery_status_t controller_runtime_mqtt_publish(const mqtt_publish_request_t *request);

/* Registers one runtime MQTT subscription for automatic reconnect replay. */
bool controller_runtime_mqtt_subscribe(const mqtt_subscription_t *subscription);

/* Gets the runtime MQTT API queue and overload snapshot. */
mqtt_api_health_t get_controller_runtime_mqtt_api_health(void);

/* Gets the runtime-owned terminal health snapshot without credential data. */
terminal_health_t get_controller_runtime_terminal_health(void);
