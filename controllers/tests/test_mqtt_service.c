#include <assert.h>
#include <stdio.h>
#include <string.h>

#include "mqtt_service.h"

/* Named timing, route, and limit values make supervisor expectations explicit. */
enum
{
    TEST_KEEPALIVE_SECONDS     = 30,
    TEST_OUTBOUND_QUEUE_DEPTH  = 8,
    TEST_INBOUND_PAYLOAD_BYTES = 512,
    TEST_INITIAL_BACKOFF_MS    = 1000,
    TEST_MAXIMUM_BACKOFF_MS    = 8000,
    TEST_JITTER_PERCENT        = 20,
    FIRST_EVENT_SEQUENCE       = 1,
    SECOND_EVENT_SEQUENCE      = 2,
    SERIAL_ROUTE_IDENTIFIER    = 41,
    CAN_ROUTE_IDENTIFIER       = 42,
    FIRST_ROUTE_GENERATION     = 10,
    SECOND_ROUTE_GENERATION    = 11,
    FIRST_PROCESS_TIME_MS      = 100,
    FAILURE_TIME_MS            = 200,
    FIRST_RETRY_TIME_MS        = 1200,
    SECOND_FAILURE_TIME_MS     = 1300,
    SECOND_RETRY_TIME_MS       = 3300,
};

static const char TEST_URI[]             = "mqtts://broker.example.test";
static const char TEST_CLIENT_ID[]       = "controller-test-001";
static const char TEST_USERNAME_REF[]    = "config:mqtt_username";
static const char TEST_PASSWORD_REF[]    = "config:mqtt_password";
static const char TEST_CA_REF[]          = "bundle:platform";
static const char TEST_ROUTE_SERIAL[]    = "rs485";
static const char TEST_ROUTE_CAN[]       = "can";
static const char TEST_BROKER_ERROR[]    = "broker_unavailable";
static const char TEST_SUCCESS_MESSAGE[] = "mqtt_service tests passed";

typedef struct
{
    size_t connect_count;
    size_t disconnect_count;
    size_t replay_count;
    uint32_t connected_route;
    bool connect_result;
    bool is_route_available;
    mqtt_transport_route_t route;
} fixture_t;

/* Gets complete broker settings for isolated supervisor tests. */
static mqtt_broker_config_t get_valid_config(void)
{
    return (mqtt_broker_config_t){
        .enabled                       = true,
        .uri                           = TEST_URI,
        .client_id                     = TEST_CLIENT_ID,
        .username_reference            = TEST_USERNAME_REF,
        .password_reference            = TEST_PASSWORD_REF,
        .tls_policy                    = MQTT_TLS_PLATFORM_TRUST,
        .ca_reference                  = TEST_CA_REF,
        .keepalive_seconds             = TEST_KEEPALIVE_SECONDS,
        .session_policy                = MQTT_SESSION_CLEAN,
        .maximum_outbound_queue_depth  = TEST_OUTBOUND_QUEUE_DEPTH,
        .maximum_inbound_payload_bytes = TEST_INBOUND_PAYLOAD_BYTES,
        .initial_backoff_ms            = TEST_INITIAL_BACKOFF_MS,
        .maximum_backoff_ms            = TEST_MAXIMUM_BACKOFF_MS,
        .jitter_percent                = TEST_JITTER_PERCENT,
    };
}

/* Sets one opaque transport route without introducing network concepts. */
static void set_route(fixture_t *fixture, uint32_t identifier, uint64_t generation, const char *name)
{
    fixture->is_route_available = true;
    fixture->route.identifier   = identifier;
    fixture->route.generation   = generation;
    (void)snprintf(fixture->route.name, sizeof(fixture->route.name), "%s", name);
}

/* Gets the fixture's current route or reports transport unavailability. */
static bool get_transport_route(mqtt_transport_route_t *route, void *context)
{
    fixture_t *fixture = context;
    if (!fixture->is_route_available)
    {
        return false;
    }
    *route = fixture->route;
    return true;
}

/* Records asynchronous transport starts and returns the configured outcome. */
static bool connect_transport(const mqtt_broker_config_t *config, const mqtt_transport_route_t *route, void *context)
{
    assert(config != NULL);
    fixture_t *fixture = context;
    fixture->connect_count++;
    fixture->connected_route = route->identifier;
    return fixture->connect_result;
}

/* Records transport stop requests for retry and shutdown assertions. */
static void disconnect_transport(void *context)
{
    fixture_t *fixture = context;
    fixture->disconnect_count++;
}

/* Records subscription restoration after each successful connection. */
static void replay_subscriptions(void *context)
{
    fixture_t *fixture = context;
    fixture->replay_count++;
}

/* Gets deterministic midpoint jitter so expected first retry timing remains exact. */
static uint32_t get_midpoint_random(void *context)
{
    assert(context != NULL);
    return TEST_INITIAL_BACKOFF_MS * TEST_JITTER_PERCENT / 100;
}

/* Initializes a service fixture with callbacks that never block. */
static mqtt_service_t get_service(fixture_t *fixture, const mqtt_broker_config_t *config)
{
    mqtt_service_t service;
    mqtt_service_init(&service, config, get_transport_route, connect_transport, disconnect_transport, replay_subscriptions,
                      get_midpoint_random, fixture);
    return service;
}

/* Enqueues one transport result using stable test sequencing. */
static void enqueue_event(mqtt_service_t *service, mqtt_transport_event_type_t type, uint32_t sequence,
                          mqtt_error_category_t category)
{
    const mqtt_transport_event_t event = {
        .type           = type,
        .sequence       = sequence,
        .error_category = category,
        .error_detail   = category == MQTT_ERROR_NONE ? NULL : TEST_BROKER_ERROR,
    };
    assert(mqtt_service_enqueue_event(service, &event));
}

/* Verifies required fields, bounds, policies, and pinned trust references. */
static void test_configuration_validation(void)
{
    mqtt_broker_config_t config = get_valid_config();
    assert(is_mqtt_broker_config_valid(&config));
    config.client_id = "";
    assert(!is_mqtt_broker_config_valid(&config));
    config                = get_valid_config();
    config.jitter_percent = 101;
    assert(!is_mqtt_broker_config_valid(&config));
    config              = get_valid_config();
    config.tls_policy   = MQTT_TLS_PINNED_CA;
    config.ca_reference = NULL;
    assert(!is_mqtt_broker_config_valid(&config));
    config                 = get_valid_config();
    config.last_will_topic = "controller/status";
    assert(!is_mqtt_broker_config_valid(&config));
}

/* Verifies a session waits for any transport provider before connecting. */
static void test_waits_for_transport(void)
{
    fixture_t fixture                 = {.connect_result = true};
    const mqtt_broker_config_t config = get_valid_config();
    mqtt_service_t service            = get_service(&fixture, &config);
    mqtt_service_process(&service, FIRST_PROCESS_TIME_MS);
    assert(service.state == MQTT_SESSION_WAITING_FOR_TRANSPORT);
    assert(fixture.connect_count == 0);
    set_route(&fixture, SERIAL_ROUTE_IDENTIFIER, FIRST_ROUTE_GENERATION, TEST_ROUTE_SERIAL);
    mqtt_service_process(&service, FAILURE_TIME_MS);
    assert(service.state == MQTT_SESSION_CONNECTING);
    assert(fixture.connected_route == SERIAL_ROUTE_IDENTIFIER);
}

/* Verifies duplicate connection events replay subscriptions only once. */
static void test_connection_and_subscription_replay(void)
{
    fixture_t fixture = {.connect_result = true};
    set_route(&fixture, CAN_ROUTE_IDENTIFIER, FIRST_ROUTE_GENERATION, TEST_ROUTE_CAN);
    const mqtt_broker_config_t config = get_valid_config();
    mqtt_service_t service            = get_service(&fixture, &config);
    mqtt_service_process(&service, FIRST_PROCESS_TIME_MS);
    enqueue_event(&service, MQTT_TRANSPORT_CONNECTED, FIRST_EVENT_SEQUENCE, MQTT_ERROR_NONE);
    enqueue_event(&service, MQTT_TRANSPORT_CONNECTED, FIRST_EVENT_SEQUENCE, MQTT_ERROR_NONE);
    mqtt_service_process(&service, FAILURE_TIME_MS);
    assert(service.state == MQTT_SESSION_ONLINE);
    assert(fixture.replay_count == 1);
}

/* Verifies failures back off exponentially and retry through the same provider. */
static void test_bounded_backoff(void)
{
    fixture_t fixture = {.connect_result = true};
    set_route(&fixture, SERIAL_ROUTE_IDENTIFIER, FIRST_ROUTE_GENERATION, TEST_ROUTE_SERIAL);
    mqtt_broker_config_t config = get_valid_config();
    config.jitter_percent       = 0;
    mqtt_service_t service      = get_service(&fixture, &config);
    mqtt_service_process(&service, FIRST_PROCESS_TIME_MS);
    enqueue_event(&service, MQTT_TRANSPORT_FAILED, FIRST_EVENT_SEQUENCE, MQTT_ERROR_BROKER);
    mqtt_service_process(&service, FAILURE_TIME_MS);
    assert(service.retry_at_ms == FIRST_RETRY_TIME_MS);
    mqtt_service_process(&service, FIRST_RETRY_TIME_MS);
    enqueue_event(&service, MQTT_TRANSPORT_FAILED, SECOND_EVENT_SEQUENCE, MQTT_ERROR_BROKER);
    mqtt_service_process(&service, SECOND_FAILURE_TIME_MS);
    assert(service.retry_at_ms == SECOND_RETRY_TIME_MS);
    assert(fixture.disconnect_count == 2);
}

/* Verifies route loss and generation changes reconnect through any replacement transport. */
static void test_route_change_reconnect(void)
{
    fixture_t fixture = {.connect_result = true};
    set_route(&fixture, SERIAL_ROUTE_IDENTIFIER, FIRST_ROUTE_GENERATION, TEST_ROUTE_SERIAL);
    const mqtt_broker_config_t config = get_valid_config();
    mqtt_service_t service            = get_service(&fixture, &config);
    mqtt_service_process(&service, FIRST_PROCESS_TIME_MS);
    enqueue_event(&service, MQTT_TRANSPORT_CONNECTED, FIRST_EVENT_SEQUENCE, MQTT_ERROR_NONE);
    mqtt_service_process(&service, FAILURE_TIME_MS);
    set_route(&fixture, CAN_ROUTE_IDENTIFIER, SECOND_ROUTE_GENERATION, TEST_ROUTE_CAN);
    mqtt_service_process(&service, FAILURE_TIME_MS);
    assert(service.state == MQTT_SESSION_BACKOFF);
    assert(service.last_error_category == MQTT_ERROR_ROUTE);
    mqtt_service_process(&service, FIRST_RETRY_TIME_MS);
    assert(fixture.connected_route == CAN_ROUTE_IDENTIFIER);
}

/* Verifies callback overload is rejected and observable without allocation. */
static void test_event_queue_limit(void)
{
    fixture_t fixture                 = {.connect_result = true};
    const mqtt_broker_config_t config = get_valid_config();
    mqtt_service_t service            = get_service(&fixture, &config);
    for (uint32_t sequence = FIRST_EVENT_SEQUENCE; sequence <= MQTT_EVENT_QUEUE_CAPACITY; sequence++)
    {
        enqueue_event(&service, MQTT_TRANSPORT_CONNECTED, sequence, MQTT_ERROR_NONE);
    }
    const mqtt_transport_event_t overflow = {
        .type = MQTT_TRANSPORT_CONNECTED, .sequence = MQTT_EVENT_QUEUE_CAPACITY + 1, .error_category = MQTT_ERROR_NONE};
    assert(!mqtt_service_enqueue_event(&service, &overflow));
    assert(mqtt_service_get_health(&service).dropped_event_count == 1);
}

/* Runs all MQTT supervisor checks and returns success when assertions hold. */
int main(void)
{
    test_configuration_validation();
    test_waits_for_transport();
    test_connection_and_subscription_replay();
    test_bounded_backoff();
    test_route_change_reconnect();
    test_event_queue_limit();
    (void)puts(TEST_SUCCESS_MESSAGE);
    return 0;
}
