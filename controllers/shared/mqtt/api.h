#pragma once

#include <stdbool.h>
#include <stddef.h>
#include <stdint.h>

/* Fixed limits make publish, receive, and subscription ownership deterministic. */
enum
{
    MQTT_TOPIC_CAPACITY          = 129,
    MQTT_PAYLOAD_CAPACITY        = 513,
    MQTT_CORRELATION_CAPACITY    = 65,
    MQTT_OWNER_CAPACITY          = 33,
    MQTT_PUBLISH_QUEUE_CAPACITY  = 16,
    MQTT_RECEIVE_QUEUE_CAPACITY  = 8,
    MQTT_DELIVERY_QUEUE_CAPACITY = 16,
    MQTT_SUBSCRIPTION_CAPACITY   = 8,
};

/* Supported quality levels match the MQTT protocol delivery contracts. */
typedef enum
{
    MQTT_QOS_AT_MOST_ONCE,
    MQTT_QOS_AT_LEAST_ONCE,
    MQTT_QOS_EXACTLY_ONCE,
} mqtt_qos_t;

/* Offline policies state whether a message may consume bounded storage. */
typedef enum
{
    MQTT_OFFLINE_DISCARD,
    MQTT_OFFLINE_REPLACE_NEWEST,
    MQTT_OFFLINE_QUEUE,
} mqtt_offline_policy_t;

/* Delivery results let producers correlate accepted and rejected publications. */
typedef enum
{
    MQTT_DELIVERY_ACCEPTED,
    MQTT_DELIVERY_SENT,
    MQTT_DELIVERY_REJECTED_INVALID,
    MQTT_DELIVERY_REJECTED_OFFLINE,
    MQTT_DELIVERY_REJECTED_QUEUE_FULL,
    MQTT_DELIVERY_TRANSPORT_FAILED,
} mqtt_delivery_status_t;

typedef struct
{
    const char *topic;
    const void *payload;
    size_t payload_size;
    mqtt_qos_t qos;
    bool is_retained;
    const char *correlation_id;
    mqtt_offline_policy_t offline_policy;
} mqtt_publish_request_t;

typedef struct
{
    char correlation_id[MQTT_CORRELATION_CAPACITY];
    mqtt_delivery_status_t status;
    int32_t transport_message_id;
} mqtt_delivery_result_t;

typedef struct
{
    char topic[MQTT_TOPIC_CAPACITY];
    uint8_t payload[MQTT_PAYLOAD_CAPACITY];
    size_t payload_size;
    mqtt_qos_t qos;
    bool is_duplicate;
} mqtt_inbound_message_t;

/* Subscriber callbacks run only from service processing, never from the driver callback. */
typedef void (*mqtt_message_callback_t)(const mqtt_inbound_message_t *message, void *context);

typedef struct
{
    const char *topic_filter;
    mqtt_qos_t qos;
    const char *owner_id;
    mqtt_message_callback_t callback;
    void *context;
} mqtt_subscription_t;

/* Platform callbacks copy data synchronously and must return without indefinite waiting. */
typedef int32_t (*mqtt_publish_transport_t)(const char *topic, const void *payload, size_t payload_size, mqtt_qos_t qos,
                                            bool is_retained, void *context);
typedef bool (*mqtt_subscribe_transport_t)(const char *topic_filter, mqtt_qos_t qos, void *context);

typedef struct
{
    char topic[MQTT_TOPIC_CAPACITY];
    uint8_t payload[MQTT_PAYLOAD_CAPACITY];
    size_t payload_size;
    mqtt_qos_t qos;
    bool is_retained;
    mqtt_offline_policy_t offline_policy;
    char correlation_id[MQTT_CORRELATION_CAPACITY];
} mqtt_owned_publish_t;

typedef struct
{
    char topic_filter[MQTT_TOPIC_CAPACITY];
    char owner_id[MQTT_OWNER_CAPACITY];
    mqtt_qos_t qos;
    mqtt_message_callback_t callback;
    void *context;
    bool is_used;
} mqtt_owned_subscription_t;

/* Health exposes overload behavior without payloads, topics, or credentials. */
typedef struct
{
    size_t publish_queue_depth;
    size_t receive_queue_depth;
    size_t subscription_count;
    uint32_t published_count;
    uint32_t received_count;
    uint32_t coalesced_count;
    uint32_t publish_rejection_count;
    uint32_t receive_rejection_count;
    uint32_t subscriber_drop_count;
} mqtt_api_health_t;

typedef struct
{
    mqtt_owned_publish_t publishes[MQTT_PUBLISH_QUEUE_CAPACITY];
    mqtt_inbound_message_t receives[MQTT_RECEIVE_QUEUE_CAPACITY];
    mqtt_delivery_result_t deliveries[MQTT_DELIVERY_QUEUE_CAPACITY];
    mqtt_owned_subscription_t subscriptions[MQTT_SUBSCRIPTION_CAPACITY];
    size_t publish_head;
    size_t publish_count;
    size_t receive_head;
    size_t receive_count;
    size_t delivery_head;
    size_t delivery_count;
    bool is_online;
    mqtt_publish_transport_t publish_transport;
    mqtt_subscribe_transport_t subscribe_transport;
    void *transport_context;
    mqtt_api_health_t health;
} mqtt_api_t;

/* Initializes empty bounded registries and queues around an abstract transport. */
void mqtt_api_init(mqtt_api_t *api, mqtt_publish_transport_t publish_transport, mqtt_subscribe_transport_t subscribe_transport,
                   void *transport_context);

/* Updates broker availability and flushes accepted offline work when processing resumes. */
void mqtt_api_set_online(mqtt_api_t *api, bool is_online);

/* Validates and accepts, coalesces, or rejects one publication deterministically. */
mqtt_delivery_status_t mqtt_api_publish(mqtt_api_t *api, const mqtt_publish_request_t *request);

/* Gets the oldest owned delivery outcome without blocking. */
bool mqtt_api_get_delivery_result(mqtt_api_t *api, mqtt_delivery_result_t *result);

/* Registers a unique owner/filter destination in bounded owned storage. */
bool mqtt_api_subscribe(mqtt_api_t *api, const mqtt_subscription_t *subscription);

/* Replays every registered subscription after a broker reconnect. */
void mqtt_api_replay_subscriptions(mqtt_api_t *api);

/* Copies one complete driver message before its callback-owned storage expires. */
bool mqtt_api_enqueue_inbound(mqtt_api_t *api, const char *topic, size_t topic_size, const void *payload, size_t payload_size,
                              mqtt_qos_t qos, bool is_duplicate);

/* Sends and dispatches bounded queued work outside MQTT driver callbacks. */
void mqtt_api_process(mqtt_api_t *api);

/* Tests an MQTT topic name or subscription filter according to bounded syntax rules. */
bool is_mqtt_topic_valid(const char *topic, bool is_filter);

/* Gets a redacted immutable API queue and overload snapshot. */
mqtt_api_health_t mqtt_api_get_health(const mqtt_api_t *api);
