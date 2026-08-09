#include "mqtt/api.h"

#include <string.h>

/* ESP-MQTT uses a negative identifier to report synchronous enqueue failure. */
enum
{
    MQTT_TRANSPORT_FAILURE_ID = -1,
};

/* Gets a bounded string size and reports an unterminated value at capacity. */
static size_t get_bounded_length(const char *value, size_t capacity)
{
    size_t size = 0;

    if (value == NULL)
    {
        return capacity;
    }

    while (size < capacity && value[size] != '\0')
    {
        size++;
    }
    return size;
}

/* Copies validated text into an owned, terminated buffer. */
static void copy_text(char *destination, size_t capacity, const char *source)
{
    const size_t size = get_bounded_length(source, capacity - 1);
    memcpy(destination, source, size);
    destination[size] = '\0';
}

/* Tests an MQTT topic name or subscription filter according to bounded syntax rules. */
bool is_mqtt_topic_valid(const char *topic, bool is_filter)
{
    const size_t size = get_bounded_length(topic, MQTT_TOPIC_CAPACITY);

    if (size == 0 || size >= MQTT_TOPIC_CAPACITY)
    {
        return false;
    }

    for (size_t index = 0; index < size; index++)
    {
        const char character = topic[index];

        if (character == '\0' || (!is_filter && (character == '+' || character == '#')))
        {
            return false;
        }

        if (character == '#' && (!is_filter || index + 1 != size || (index != 0 && topic[index - 1] != '/')))
        {
            return false;
        }

        if (character == '+' &&
            (!is_filter || (index != 0 && topic[index - 1] != '/') || (index + 1 != size && topic[index + 1] != '/')))
        {
            return false;
        }
    }
    return true;
}

/* Records a bounded delivery outcome, dropping the oldest when producers stop draining results. */
static void record_delivery(mqtt_api_t *api, const char *correlation_id, mqtt_delivery_status_t status,
                            int32_t transport_message_id)
{
    if (api->delivery_count == MQTT_DELIVERY_QUEUE_CAPACITY)
    {
        api->delivery_head = (api->delivery_head + 1) % MQTT_DELIVERY_QUEUE_CAPACITY;
        api->delivery_count--;
    }
    const size_t tail              = (api->delivery_head + api->delivery_count) % MQTT_DELIVERY_QUEUE_CAPACITY;
    mqtt_delivery_result_t *result = &api->deliveries[tail];
    copy_text(result->correlation_id, sizeof(result->correlation_id), correlation_id != NULL ? correlation_id : "");
    result->status               = status;
    result->transport_message_id = transport_message_id;
    api->delivery_count++;
}

/* Initializes empty bounded registries and queues around an abstract transport. */
void mqtt_api_init(mqtt_api_t *api, mqtt_publish_transport_t publish_transport, mqtt_subscribe_transport_t subscribe_transport,
                   void *transport_context)
{
    memset(api, 0, sizeof(*api));
    api->publish_transport   = publish_transport;
    api->subscribe_transport = subscribe_transport;
    api->transport_context   = transport_context;
}

/* Updates broker availability and flushes accepted offline work when processing resumes. */
void mqtt_api_set_online(mqtt_api_t *api, bool is_online)
{
    if (api != NULL)
    {
        api->is_online = is_online;
    }
}

/* Copies a valid request into one owned queue slot. */
static void enqueue_publish(mqtt_api_t *api, const mqtt_publish_request_t *request)
{
    const size_t tail             = (api->publish_head + api->publish_count) % MQTT_PUBLISH_QUEUE_CAPACITY;
    mqtt_owned_publish_t *publish = &api->publishes[tail];
    copy_text(publish->topic, sizeof(publish->topic), request->topic);
    memcpy(publish->payload, request->payload, request->payload_size);
    publish->payload_size   = request->payload_size;
    publish->qos            = request->qos;
    publish->is_retained    = request->is_retained;
    publish->offline_policy = request->offline_policy;
    copy_text(publish->correlation_id, sizeof(publish->correlation_id),
              request->correlation_id != NULL ? request->correlation_id : "");
    api->publish_count++;
    api->health.publish_queue_depth = api->publish_count;
}

/* Replaces queued telemetry with the same topic so stale samples never accumulate. */
static bool is_publish_coalesced(mqtt_api_t *api, const mqtt_publish_request_t *request)
{
    for (size_t offset = 0; offset < api->publish_count; offset++)
    {
        const size_t index            = (api->publish_head + offset) % MQTT_PUBLISH_QUEUE_CAPACITY;
        mqtt_owned_publish_t *publish = &api->publishes[index];

        if (publish->offline_policy == MQTT_OFFLINE_REPLACE_NEWEST && strcmp(publish->topic, request->topic) == 0)
        {
            memcpy(publish->payload, request->payload, request->payload_size);
            publish->payload_size = request->payload_size;
            publish->qos          = request->qos;
            publish->is_retained  = request->is_retained;
            copy_text(publish->correlation_id, sizeof(publish->correlation_id),
                      request->correlation_id != NULL ? request->correlation_id : "");
            api->health.coalesced_count++;
            return true;
        }
    }
    return false;
}

/* Validates and accepts, coalesces, or rejects one publication deterministically. */
mqtt_delivery_status_t mqtt_api_publish(mqtt_api_t *api, const mqtt_publish_request_t *request)
{
    const size_t correlation_size =
        request != NULL
            ? get_bounded_length(request->correlation_id != NULL ? request->correlation_id : "", MQTT_CORRELATION_CAPACITY)
            : MQTT_CORRELATION_CAPACITY;

    if (api == NULL || request == NULL || !is_mqtt_topic_valid(request->topic, false) || request->payload == NULL ||
        request->payload_size >= MQTT_PAYLOAD_CAPACITY || request->qos > MQTT_QOS_EXACTLY_ONCE ||
        request->offline_policy > MQTT_OFFLINE_QUEUE || correlation_size >= MQTT_CORRELATION_CAPACITY)
    {
        if (api != NULL)
        {
            api->health.publish_rejection_count++;
            record_delivery(api, request != NULL ? request->correlation_id : NULL, MQTT_DELIVERY_REJECTED_INVALID,
                            MQTT_TRANSPORT_FAILURE_ID);
        }
        return MQTT_DELIVERY_REJECTED_INVALID;
    }

    if (!api->is_online && request->offline_policy == MQTT_OFFLINE_DISCARD)
    {
        api->health.publish_rejection_count++;
        record_delivery(api, request->correlation_id, MQTT_DELIVERY_REJECTED_OFFLINE, MQTT_TRANSPORT_FAILURE_ID);
        return MQTT_DELIVERY_REJECTED_OFFLINE;
    }

    if (request->offline_policy == MQTT_OFFLINE_REPLACE_NEWEST && is_publish_coalesced(api, request))
    {
        record_delivery(api, request->correlation_id, MQTT_DELIVERY_ACCEPTED, 0);
        return MQTT_DELIVERY_ACCEPTED;
    }

    if (api->publish_count == MQTT_PUBLISH_QUEUE_CAPACITY)
    {
        api->health.publish_rejection_count++;
        record_delivery(api, request->correlation_id, MQTT_DELIVERY_REJECTED_QUEUE_FULL, MQTT_TRANSPORT_FAILURE_ID);
        return MQTT_DELIVERY_REJECTED_QUEUE_FULL;
    }
    enqueue_publish(api, request);
    record_delivery(api, request->correlation_id, MQTT_DELIVERY_ACCEPTED, 0);
    return MQTT_DELIVERY_ACCEPTED;
}

/* Gets the oldest owned delivery outcome without blocking. */
bool mqtt_api_get_delivery_result(mqtt_api_t *api, mqtt_delivery_result_t *result)
{
    if (api == NULL || result == NULL || api->delivery_count == 0)
    {
        return false;
    }
    *result            = api->deliveries[api->delivery_head];
    api->delivery_head = (api->delivery_head + 1) % MQTT_DELIVERY_QUEUE_CAPACITY;
    api->delivery_count--;
    return true;
}

/* Registers a unique owner/filter destination in bounded owned storage. */
bool mqtt_api_subscribe(mqtt_api_t *api, const mqtt_subscription_t *subscription)
{
    if (api == NULL || subscription == NULL || !is_mqtt_topic_valid(subscription->topic_filter, true) ||
        subscription->qos > MQTT_QOS_EXACTLY_ONCE || subscription->callback == NULL ||
        get_bounded_length(subscription->owner_id, MQTT_OWNER_CAPACITY) >= MQTT_OWNER_CAPACITY)
    {
        return false;
    }

    for (size_t index = 0; index < MQTT_SUBSCRIPTION_CAPACITY; index++)
    {
        mqtt_owned_subscription_t *owned = &api->subscriptions[index];

        if (owned->is_used && strcmp(owned->owner_id, subscription->owner_id) == 0 &&
            strcmp(owned->topic_filter, subscription->topic_filter) == 0)
        {
            return false;
        }

        if (!owned->is_used)
        {
            copy_text(owned->topic_filter, sizeof(owned->topic_filter), subscription->topic_filter);
            copy_text(owned->owner_id, sizeof(owned->owner_id), subscription->owner_id);
            owned->qos      = subscription->qos;
            owned->callback = subscription->callback;
            owned->context  = subscription->context;
            owned->is_used  = true;
            api->health.subscription_count++;

            if (api->is_online && api->subscribe_transport != NULL)
            {
                (void)api->subscribe_transport(owned->topic_filter, owned->qos, api->transport_context);
            }
            return true;
        }
    }
    return false;
}

/* Replays every registered subscription after a broker reconnect. */
void mqtt_api_replay_subscriptions(mqtt_api_t *api)
{
    if (api == NULL || !api->is_online || api->subscribe_transport == NULL)
    {
        return;
    }

    for (size_t index = 0; index < MQTT_SUBSCRIPTION_CAPACITY; index++)
    {
        const mqtt_owned_subscription_t *subscription = &api->subscriptions[index];

        if (subscription->is_used)
        {
            (void)api->subscribe_transport(subscription->topic_filter, subscription->qos, api->transport_context);
        }
    }
}

/* Copies one complete driver message before its callback-owned storage expires. */
bool mqtt_api_enqueue_inbound(mqtt_api_t *api, const char *topic, size_t topic_size, const void *payload, size_t payload_size,
                              mqtt_qos_t qos, bool is_duplicate)
{
    if (api == NULL || topic == NULL || topic_size == 0 || topic_size >= MQTT_TOPIC_CAPACITY || payload == NULL ||
        payload_size >= MQTT_PAYLOAD_CAPACITY || qos > MQTT_QOS_EXACTLY_ONCE || api->receive_count == MQTT_RECEIVE_QUEUE_CAPACITY)
    {
        if (api != NULL)
        {
            api->health.receive_rejection_count++;
        }
        return false;
    }
    const size_t tail               = (api->receive_head + api->receive_count) % MQTT_RECEIVE_QUEUE_CAPACITY;
    mqtt_inbound_message_t *message = &api->receives[tail];
    memcpy(message->topic, topic, topic_size);
    message->topic[topic_size] = '\0';

    if (!is_mqtt_topic_valid(message->topic, false))
    {
        api->health.receive_rejection_count++;
        return false;
    }
    memcpy(message->payload, payload, payload_size);
    message->payload[payload_size] = '\0';
    message->payload_size          = payload_size;
    message->qos                   = qos;
    message->is_duplicate          = is_duplicate;
    api->receive_count++;
    api->health.receive_queue_depth = api->receive_count;
    return true;
}

/* Tests one topic against a validated filter with single and multi-level wildcards. */
static bool is_topic_match(const char *filter, const char *topic)
{
    while (*filter != '\0' && *topic != '\0')
    {
        if (*filter == '#')
        {
            return true;
        }

        if (*filter == '+')
        {
            while (*topic != '\0' && *topic != '/')
            {
                topic++;
            }
            filter++;
        }
        else if (*filter++ != *topic++)
        {
            return false;
        }
    }
    return (*filter == '\0' && *topic == '\0') || (*filter == '#' && filter[1] == '\0');
}

/* Sends and dispatches bounded queued work outside MQTT driver callbacks. */
void mqtt_api_process(mqtt_api_t *api)
{
    if (api == NULL)
    {
        return;
    }

    while (api->is_online && api->publish_count > 0)
    {
        mqtt_owned_publish_t *publish = &api->publishes[api->publish_head];
        const int32_t message_id      = api->publish_transport != NULL
                                            ? api->publish_transport(publish->topic, publish->payload, publish->payload_size,
                                                                     publish->qos, publish->is_retained, api->transport_context)
                                            : MQTT_TRANSPORT_FAILURE_ID;
        record_delivery(api, publish->correlation_id, message_id < 0 ? MQTT_DELIVERY_TRANSPORT_FAILED : MQTT_DELIVERY_SENT,
                        message_id);

        if (message_id < 0)
        {
            break;
        }
        api->publish_head = (api->publish_head + 1) % MQTT_PUBLISH_QUEUE_CAPACITY;
        api->publish_count--;
        api->health.publish_queue_depth = api->publish_count;
        api->health.published_count++;
    }

    while (api->receive_count > 0)
    {
        const mqtt_inbound_message_t message = api->receives[api->receive_head];
        api->receive_head                    = (api->receive_head + 1) % MQTT_RECEIVE_QUEUE_CAPACITY;
        api->receive_count--;
        api->health.receive_queue_depth = api->receive_count;
        api->health.received_count++;
        bool is_delivered = false;

        for (size_t index = 0; index < MQTT_SUBSCRIPTION_CAPACITY; index++)
        {
            const mqtt_owned_subscription_t *subscription = &api->subscriptions[index];

            if (subscription->is_used && is_topic_match(subscription->topic_filter, message.topic))
            {
                subscription->callback(&message, subscription->context);
                is_delivered = true;
            }
        }

        if (!is_delivered)
        {
            api->health.subscriber_drop_count++;
        }
    }
}

/* Gets a redacted immutable API queue and overload snapshot. */
mqtt_api_health_t mqtt_api_get_health(const mqtt_api_t *api)
{
    return api != NULL ? api->health : (mqtt_api_health_t){0};
}
