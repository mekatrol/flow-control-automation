#include <assert.h>
#include <stdio.h>
#include <string.h>

#include "mqtt/api.h"

enum
{
    TEST_MESSAGE_ID = 17,
};

static const char TEST_TOPIC[]       = "controllers/unit/status";
static const char TEST_FILTER[]      = "controllers/+/command";
static const char TEST_COMMAND[]     = "controllers/unit/command";
static const char TEST_OWNER[]       = "flow-runtime";
static const char TEST_CORRELATION[] = "request-42";
static const char TEST_PAYLOAD[]     = "{\"state\":\"online\"}";

typedef struct
{
    size_t publish_count;
    size_t subscribe_count;
    size_t receive_count;
    char received_payload[MQTT_PAYLOAD_CAPACITY];
} fixture_t;

/* Records a non-blocking platform publish and returns its broker message identifier. */
static int32_t publish_transport(const char *topic, const void *payload, size_t payload_size, mqtt_qos_t qos, bool is_retained,
                                 void *context)
{
    fixture_t *fixture = context;
    assert(strcmp(topic, TEST_TOPIC) == 0);
    assert(payload_size == strlen(TEST_PAYLOAD));
    assert(memcmp(payload, TEST_PAYLOAD, payload_size) == 0);
    assert(qos == MQTT_QOS_AT_LEAST_ONCE);
    assert(is_retained);
    fixture->publish_count++;

    return TEST_MESSAGE_ID;
}

/* Records subscription replay without retaining the filter pointer. */
static bool subscribe_transport(const char *topic_filter, mqtt_qos_t qos, void *context)
{
    fixture_t *fixture = context;
    assert(strcmp(topic_filter, TEST_FILTER) == 0);
    assert(qos == MQTT_QOS_AT_LEAST_ONCE);
    fixture->subscribe_count++;

    return true;
}

/* Copies an inbound command to prove callback data remains owned and bounded. */
static void receive_message(const mqtt_inbound_message_t *message, void *context)
{
    fixture_t *fixture = context;
    assert(strcmp(message->topic, TEST_COMMAND) == 0);
    memcpy(fixture->received_payload, message->payload, message->payload_size);
    fixture->received_payload[message->payload_size] = '\0';
    fixture->receive_count++;
}

/* Gets the common retained status publication used by queue tests. */
static mqtt_publish_request_t get_request(mqtt_offline_policy_t policy)
{
    return (mqtt_publish_request_t){.topic          = TEST_TOPIC,
                                    .payload        = TEST_PAYLOAD,
                                    .payload_size   = strlen(TEST_PAYLOAD),
                                    .qos            = MQTT_QOS_AT_LEAST_ONCE,
                                    .is_retained    = true,
                                    .correlation_id = TEST_CORRELATION,
                                    .offline_policy = policy};
}

/* Verifies validation, explicit offline policy, coalescing, and online flush. */
static void test_bounded_publish(void)
{
    fixture_t fixture = {0};
    mqtt_api_t api;
    mqtt_api_init(&api, publish_transport, subscribe_transport, &fixture);
    mqtt_publish_request_t request = get_request(MQTT_OFFLINE_DISCARD);
    assert(mqtt_api_publish(&api, &request) == MQTT_DELIVERY_REJECTED_OFFLINE);
    request.offline_policy = MQTT_OFFLINE_REPLACE_NEWEST;
    assert(mqtt_api_publish(&api, &request) == MQTT_DELIVERY_ACCEPTED);
    assert(mqtt_api_publish(&api, &request) == MQTT_DELIVERY_ACCEPTED);
    assert(mqtt_api_get_health(&api).publish_queue_depth == 1);
    assert(mqtt_api_get_health(&api).coalesced_count == 1);
    mqtt_api_set_online(&api, true);
    mqtt_api_process(&api);
    assert(fixture.publish_count == 1);
    assert(mqtt_api_get_health(&api).publish_queue_depth == 0);
}

/* Verifies wildcard matching, owned receive data, and reconnect subscription replay. */
static void test_subscription_and_inbound_ownership(void)
{
    fixture_t fixture = {0};
    mqtt_api_t api;
    mqtt_api_init(&api, publish_transport, subscribe_transport, &fixture);
    const mqtt_subscription_t subscription = {.topic_filter = TEST_FILTER,
                                              .qos          = MQTT_QOS_AT_LEAST_ONCE,
                                              .owner_id     = TEST_OWNER,
                                              .callback     = receive_message,
                                              .context      = &fixture};
    assert(mqtt_api_subscribe(&api, &subscription));
    mqtt_api_set_online(&api, true);
    mqtt_api_replay_subscriptions(&api);
    assert(fixture.subscribe_count == 1);
    char callback_payload[] = "activate";
    assert(mqtt_api_enqueue_inbound(&api, TEST_COMMAND, strlen(TEST_COMMAND), callback_payload, strlen(callback_payload),
                                    MQTT_QOS_AT_LEAST_ONCE, true));
    memset(callback_payload, 'x', strlen(callback_payload));
    mqtt_api_process(&api);
    assert(fixture.receive_count == 1);
    assert(strcmp(fixture.received_payload, "activate") == 0);
}

/* Verifies invalid filters and deterministic receive queue saturation. */
static void test_rejection_limits(void)
{
    mqtt_api_t api;
    mqtt_api_init(&api, NULL, NULL, NULL);
    assert(!is_mqtt_topic_valid("bad/#/filter", true));

    for (size_t index = 0; index < MQTT_RECEIVE_QUEUE_CAPACITY; index++)
    {
        assert(mqtt_api_enqueue_inbound(&api, TEST_COMMAND, strlen(TEST_COMMAND), TEST_PAYLOAD, strlen(TEST_PAYLOAD),
                                        MQTT_QOS_AT_MOST_ONCE, false));
    }
    assert(!mqtt_api_enqueue_inbound(&api, TEST_COMMAND, strlen(TEST_COMMAND), TEST_PAYLOAD, strlen(TEST_PAYLOAD),
                                     MQTT_QOS_AT_MOST_ONCE, false));
    assert(mqtt_api_get_health(&api).receive_rejection_count == 1);
}

/* Runs all portable bidirectional MQTT API checks. */
int main(void)
{
    test_bounded_publish();
    test_subscription_and_inbound_ownership();
    test_rejection_limits();
    puts("MQTT API tests passed");

    return 0;
}
