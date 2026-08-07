#pragma once

#include <stdbool.h>

#include "controller/protocol.h"
#include "mqtt/api.h"
#include "mqtt/service.h"
#include "network/manager.h"
#include "rs485/service.h"
#include "terminal/service.h"

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

/* Gets the runtime-owned RS485 health snapshot without exposing frame contents. */
rs485_health_t get_controller_runtime_rs485_health(void);

/* Copies a complete frame into the runtime-owned bounded RS485 transmit queue. */
bool controller_runtime_rs485_send(const uint8_t *data, size_t size);

/* Gets and removes the oldest complete runtime-owned RS485 receive frame. */
bool controller_runtime_rs485_get_received(rs485_frame_t *frame);

/* Gets protocol validation and response counters without exposing message content. */
controller_protocol_health_t get_controller_runtime_protocol_health(void);
