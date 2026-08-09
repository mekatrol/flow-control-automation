#pragma once

#include <stdbool.h>
#include <stddef.h>
#include <stdint.h>

/* Bounded MQTT metadata prevents callbacks and diagnostics from growing memory use. */
#define MQTT_EVENT_QUEUE_CAPACITY 8
#define MQTT_ERROR_DETAIL_MAX 48
#define MQTT_TRANSPORT_NAME_MAX 16

/* TLS policies make broker verification requirements explicit. */
typedef enum
{
    MQTT_TLS_DISABLED,
    MQTT_TLS_PLATFORM_TRUST,
    MQTT_TLS_PINNED_CA,
} mqtt_tls_policy_t;

/* Session policies describe whether broker subscriptions survive reconnects. */
typedef enum
{
    MQTT_SESSION_CLEAN,
    MQTT_SESSION_PERSISTENT,
} mqtt_session_policy_t;

/* Supervised states expose broker progress without transport-specific types. */
typedef enum
{
    MQTT_SESSION_DISABLED,
    MQTT_SESSION_WAITING_FOR_TRANSPORT,
    MQTT_SESSION_CONNECTING,
    MQTT_SESSION_ONLINE,
    MQTT_SESSION_BACKOFF,
    MQTT_SESSION_STOPPING,
} mqtt_session_state_t;

/* Stable error categories preserve actionable health without leaking credentials. */
typedef enum
{
    MQTT_ERROR_NONE,
    MQTT_ERROR_ROUTE,
    MQTT_ERROR_DNS,
    MQTT_ERROR_TLS,
    MQTT_ERROR_AUTHENTICATION,
    MQTT_ERROR_BROKER,
    MQTT_ERROR_TRANSPORT,
} mqtt_error_category_t;

/* Transport events are reduced to stable service-level outcomes. */
typedef enum
{
    MQTT_TRANSPORT_CONNECTED,
    MQTT_TRANSPORT_DISCONNECTED,
    MQTT_TRANSPORT_FAILED,
    MQTT_TRANSPORT_STOPPED,
} mqtt_transport_event_type_t;

/* Typed broker configuration contains references to secrets, never secret values. */
typedef struct
{
    bool enabled;
    const char *uri;
    const char *client_id;
    const char *username_reference;
    const char *password_reference;
    mqtt_tls_policy_t tls_policy;
    const char *ca_reference;
    uint16_t keepalive_seconds;
    mqtt_session_policy_t session_policy;
    const char *last_will_topic;
    const char *last_will_payload;
    uint8_t last_will_qos;
    bool is_last_will_retained;
    size_t maximum_outbound_queue_depth;
    size_t maximum_inbound_payload_bytes;
    uint32_t initial_backoff_ms;
    uint32_t maximum_backoff_ms;
    uint8_t jitter_percent;
} mqtt_broker_config_t;

/* Owned callback event data remains valid after a transport callback returns. */
typedef struct
{
    mqtt_transport_event_type_t type;
    uint32_t sequence;
    mqtt_error_category_t error_category;

    /* Detail must be a stable non-sensitive code rather than transport prose or credentials. */
    const char *error_detail;
} mqtt_transport_event_t;

typedef struct
{
    mqtt_transport_event_type_t type;
    uint32_t sequence;
    mqtt_error_category_t error_category;
    char error_detail[MQTT_ERROR_DETAIL_MAX];
} mqtt_queued_event_t;

/* Opaque routes let IP, serial, CAN, and future adapters report transport availability. */
typedef struct
{
    uint32_t identifier;
    uint64_t generation;
    char name[MQTT_TRANSPORT_NAME_MAX];
} mqtt_transport_route_t;

/* Read-only health summarizes state without exposing broker credentials. */
typedef struct
{
    mqtt_session_state_t state;
    mqtt_error_category_t last_error_category;
    mqtt_transport_route_t selected_transport;
    bool is_transport_selected;
    uint32_t reconnect_count;
    size_t queued_event_count;
    uint32_t dropped_event_count;
    uint32_t subscription_replay_count;
    uint64_t retry_at_ms;
    char last_error_detail[MQTT_ERROR_DETAIL_MAX];
} mqtt_session_health_t;

/* Gets the currently eligible route without exposing transport-specific state. */
typedef bool (*mqtt_transport_get_route_t)(mqtt_transport_route_t *route, void *context);

/* Starts one asynchronous connection on the selected opaque transport route. */
typedef bool (*mqtt_transport_connect_t)(const mqtt_broker_config_t *config, const mqtt_transport_route_t *route, void *context);

/* Requests an asynchronous transport stop without blocking the supervisor. */
typedef void (*mqtt_transport_disconnect_t)(void *context);

/* Recreates the session's registered subscriptions after broker connection. */
typedef void (*mqtt_subscription_replay_t)(void *context);

/* Entropy callback makes retry jitter portable and deterministic in tests. */
typedef uint32_t (*mqtt_random_t)(void *context);

typedef struct
{
    mqtt_broker_config_t config;
    mqtt_queued_event_t events[MQTT_EVENT_QUEUE_CAPACITY];
    size_t event_head;
    size_t event_count;
    uint32_t dropped_events;
    uint32_t last_event_sequence;
    uint32_t reconnect_count;
    uint32_t consecutive_failure_count;
    uint32_t subscription_replay_count;
    uint64_t retry_at_ms;
    mqtt_session_state_t state;
    mqtt_error_category_t last_error_category;
    mqtt_transport_route_t selected_transport;
    bool is_transport_selected;
    char last_error_detail[MQTT_ERROR_DETAIL_MAX];
    mqtt_transport_get_route_t get_transport_route;
    mqtt_transport_connect_t connect_transport;
    mqtt_transport_disconnect_t disconnect_transport;
    mqtt_subscription_replay_t replay_subscriptions;
    mqtt_random_t random;
    void *callback_context;
} mqtt_service_t;

/* Tests whether typed broker settings are complete, bounded, and internally consistent. */
bool is_mqtt_broker_config_valid(const mqtt_broker_config_t *config);

/* Initializes one broker session without waiting for a transport or broker. */
void mqtt_service_init(mqtt_service_t *service, const mqtt_broker_config_t *config,
                       mqtt_transport_get_route_t get_transport_route, mqtt_transport_connect_t connect_transport,
                       mqtt_transport_disconnect_t disconnect_transport, mqtt_subscription_replay_t replay_subscriptions,
                       mqtt_random_t random, void *callback_context);

/* Copies a short-lived transport event into bounded owned storage. */
bool mqtt_service_enqueue_event(mqtt_service_t *service, const mqtt_transport_event_t *event);

/* Advances route selection, transport events, and reconnect timers without blocking. */
void mqtt_service_process(mqtt_service_t *service, uint64_t now_ms);

/* Stops the session and prevents new transport connection attempts. */
void mqtt_service_stop(mqtt_service_t *service);

/* Gets an immutable health snapshot with redacted error information. */
mqtt_session_health_t mqtt_service_get_health(const mqtt_service_t *service);

/* Gets the stable diagnostic name associated with an MQTT session state. */
const char *mqtt_get_session_state_name(mqtt_session_state_t state);

/* Gets the stable diagnostic name associated with an MQTT error category. */
const char *mqtt_get_error_category_name(mqtt_error_category_t category);
