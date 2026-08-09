#include "mqtt/service.h"

#include <string.h>

/* Retry arithmetic uses a percentage scale and bounded exponential growth. */
enum
{
    PERCENT_SCALE                  = 100U,
    EXPONENTIAL_BACKOFF_MULTIPLIER = 2U,
};

/* Stable state names form the diagnostic and health contract. */
static const char STATE_DISABLED[]              = "disabled";
static const char STATE_WAITING_FOR_TRANSPORT[] = "waiting_for_transport";
static const char STATE_CONNECTING[]            = "connecting";
static const char STATE_ONLINE[]                = "online";
static const char STATE_BACKOFF[]               = "backoff";
static const char STATE_STOPPING[]              = "stopping";
static const char STATE_UNKNOWN[]               = "unknown";

/* Stable error names expose categories without transport or credential details. */
static const char ERROR_NONE[]           = "none";
static const char ERROR_ROUTE[]          = "route";
static const char ERROR_DNS[]            = "dns";
static const char ERROR_TLS[]            = "tls";
static const char ERROR_AUTHENTICATION[] = "authentication";
static const char ERROR_BROKER[]         = "broker";
static const char ERROR_TRANSPORT[]      = "transport";

/* Stable internal errors describe failures without transport-owned strings. */
static const char ERROR_TRANSPORT_START[] = "transport_start_failed";
static const char ERROR_ROUTE_LOST[]      = "selected_transport_lost";

/* Copies optional text into bounded owned storage. */
static void copy_text(char *destination, size_t size, const char *source)
{
    if (size == 0)
    {
        return;
    }

    if (source == NULL)
    {
        source = "";
    }
    strncpy(destination, source, size - 1);
    destination[size - 1] = '\0';
}

/* Tests whether a string reference exists and is not empty. */
static bool is_nonempty(const char *value)
{
    return value != NULL && value[0] != '\0';
}

/* Tests whether typed broker settings are complete, bounded, and internally consistent. */
bool is_mqtt_broker_config_valid(const mqtt_broker_config_t *config)
{
    if (config == NULL || !is_nonempty(config->uri) || !is_nonempty(config->client_id) || config->keepalive_seconds == 0 ||
        config->maximum_outbound_queue_depth == 0 || config->maximum_inbound_payload_bytes == 0 ||
        config->initial_backoff_ms == 0 || config->maximum_backoff_ms < config->initial_backoff_ms ||
        config->jitter_percent > PERCENT_SCALE)
    {
        return false;
    }

    if (config->tls_policy > MQTT_TLS_PINNED_CA || config->session_policy > MQTT_SESSION_PERSISTENT || config->last_will_qos > 2)
    {
        return false;
    }
    /* A pinned policy cannot verify a broker without a platform credential reference. */
    const bool is_last_will_valid = !is_nonempty(config->last_will_topic) || is_nonempty(config->last_will_payload);

    return is_last_will_valid && (config->tls_policy != MQTT_TLS_PINNED_CA || is_nonempty(config->ca_reference));
}

/* Initializes one broker session without waiting for a transport or broker. */
void mqtt_service_init(mqtt_service_t *service, const mqtt_broker_config_t *config,
                       mqtt_transport_get_route_t get_transport_route, mqtt_transport_connect_t connect_transport,
                       mqtt_transport_disconnect_t disconnect_transport, mqtt_subscription_replay_t replay_subscriptions,
                       mqtt_random_t random, void *callback_context)
{
    memset(service, 0, sizeof(*service));
    service->get_transport_route  = get_transport_route;
    service->connect_transport    = connect_transport;
    service->disconnect_transport = disconnect_transport;
    service->replay_subscriptions = replay_subscriptions;
    service->random               = random;
    service->callback_context     = callback_context;

    if (config != NULL)
    {
        service->config = *config;
    }
    service->state = config != NULL && config->enabled && get_transport_route != NULL && is_mqtt_broker_config_valid(config)
                         ? MQTT_SESSION_WAITING_FOR_TRANSPORT
                         : MQTT_SESSION_DISABLED;
}

/* Copies a short-lived transport event into bounded owned storage. */
bool mqtt_service_enqueue_event(mqtt_service_t *service, const mqtt_transport_event_t *event)
{
    if (service == NULL || event == NULL || event->type > MQTT_TRANSPORT_STOPPED)
    {
        return false;
    }

    if (service->event_count == MQTT_EVENT_QUEUE_CAPACITY)
    {
        service->dropped_events++;

        return false;
    }
    const size_t tail                = (service->event_head + service->event_count) % MQTT_EVENT_QUEUE_CAPACITY;
    mqtt_queued_event_t *destination = &service->events[tail];
    destination->type                = event->type;
    destination->sequence            = event->sequence;
    destination->error_category      = event->error_category;
    copy_text(destination->error_detail, sizeof(destination->error_detail), event->error_detail);
    service->event_count++;

    return true;
}

/* Gets a bounded retry delay using reconnect count and optional symmetric jitter. */
static uint32_t get_backoff_delay(mqtt_service_t *service)
{
    uint64_t delay = service->config.initial_backoff_ms;
    uint32_t shift = service->consecutive_failure_count;

    if (shift > 0)
    {
        shift--;
    }

    while (shift-- > 0 && delay < service->config.maximum_backoff_ms)
    {
        delay *= EXPONENTIAL_BACKOFF_MULTIPLIER;

        if (delay > service->config.maximum_backoff_ms)
        {
            delay = service->config.maximum_backoff_ms;
        }
    }
    const uint64_t range = delay * service->config.jitter_percent / PERCENT_SCALE;

    if (range != 0 && service->random != NULL)
    {
        const uint64_t width = range * EXPONENTIAL_BACKOFF_MULTIPLIER + 1U;
        const int64_t offset = (int64_t)(service->random(service->callback_context) % width) - (int64_t)range;
        delay                = (uint64_t)((int64_t)delay + offset);
    }

    return (uint32_t)(delay > service->config.maximum_backoff_ms ? service->config.maximum_backoff_ms : delay);
}

/* Enters supervised backoff and clears the route used by the failed connection. */
static void enter_backoff(mqtt_service_t *service, uint64_t now_ms, mqtt_error_category_t category, const char *detail)
{
    if (service->reconnect_count != UINT32_MAX)
    {
        service->reconnect_count++;
    }

    if (service->consecutive_failure_count != UINT32_MAX)
    {
        service->consecutive_failure_count++;
    }
    service->state                 = MQTT_SESSION_BACKOFF;
    service->retry_at_ms           = now_ms + get_backoff_delay(service);
    service->last_error_category   = category;
    service->is_transport_selected = false;
    copy_text(service->last_error_detail, sizeof(service->last_error_detail), detail);

    if (service->disconnect_transport != NULL)
    {
        service->disconnect_transport(service->callback_context);
    }
}

/* Applies one fresh transport event outside the platform callback. */
static void apply_event(mqtt_service_t *service, const mqtt_queued_event_t *event, uint64_t now_ms)
{
    if (event->sequence <= service->last_event_sequence || service->state == MQTT_SESSION_DISABLED ||
        service->state == MQTT_SESSION_STOPPING)
    {
        return;
    }
    service->last_event_sequence = event->sequence;

    switch (event->type)
    {
        case MQTT_TRANSPORT_CONNECTED:

            if (service->state == MQTT_SESSION_CONNECTING)
            {
                service->state                     = MQTT_SESSION_ONLINE;
                service->last_error_category       = MQTT_ERROR_NONE;
                service->last_error_detail[0]      = '\0';
                service->consecutive_failure_count = 0;
                service->subscription_replay_count++;

                if (service->replay_subscriptions != NULL)
                {
                    service->replay_subscriptions(service->callback_context);
                }
            }
            break;
        case MQTT_TRANSPORT_DISCONNECTED:
        case MQTT_TRANSPORT_FAILED:
            enter_backoff(service, now_ms,
                          event->error_category == MQTT_ERROR_NONE ? MQTT_ERROR_TRANSPORT : event->error_category,
                          event->error_detail);
            break;
        case MQTT_TRANSPORT_STOPPED:

            if (service->state != MQTT_SESSION_BACKOFF)
            {
                service->state = MQTT_SESSION_WAITING_FOR_TRANSPORT;
            }
            break;
    }
}

/* Tests whether the selected route remains eligible for broker traffic. */
static bool is_selected_transport_eligible(const mqtt_service_t *service)
{
    if (!service->is_transport_selected)
    {
        return false;
    }
    mqtt_transport_route_t current_route;

    return service->get_transport_route(&current_route, service->callback_context) &&
           current_route.identifier == service->selected_transport.identifier &&
           current_route.generation == service->selected_transport.generation;
}

/* Starts one connection attempt when the transport provider has an eligible route. */
static void try_connect(mqtt_service_t *service, uint64_t now_ms)
{
    mqtt_transport_route_t route;

    if (!service->get_transport_route(&route, service->callback_context))
    {
        service->state                 = MQTT_SESSION_WAITING_FOR_TRANSPORT;
        service->is_transport_selected = false;

        return;
    }
    service->selected_transport    = route;
    service->is_transport_selected = true;
    service->state                 = MQTT_SESSION_CONNECTING;

    if (service->connect_transport == NULL || !service->connect_transport(&service->config, &route, service->callback_context))
    {
        enter_backoff(service, now_ms, MQTT_ERROR_TRANSPORT, ERROR_TRANSPORT_START);
    }
}

/* Advances route selection, transport events, and reconnect timers without blocking. */
void mqtt_service_process(mqtt_service_t *service, uint64_t now_ms)
{
    if (service == NULL || service->state == MQTT_SESSION_DISABLED || service->state == MQTT_SESSION_STOPPING)
    {
        return;
    }

    while (service->event_count > 0)
    {
        const mqtt_queued_event_t event = service->events[service->event_head];
        service->event_head             = (service->event_head + 1) % MQTT_EVENT_QUEUE_CAPACITY;
        service->event_count--;
        apply_event(service, &event, now_ms);
    }

    if ((service->state == MQTT_SESSION_CONNECTING || service->state == MQTT_SESSION_ONLINE) &&
        !is_selected_transport_eligible(service))
    {
        enter_backoff(service, now_ms, MQTT_ERROR_ROUTE, ERROR_ROUTE_LOST);
    }

    if (service->state == MQTT_SESSION_WAITING_FOR_TRANSPORT ||
        (service->state == MQTT_SESSION_BACKOFF && now_ms >= service->retry_at_ms))
    {
        try_connect(service, now_ms);
    }
}

/* Stops the session and prevents new transport connection attempts. */
void mqtt_service_stop(mqtt_service_t *service)
{
    if (service == NULL || service->state == MQTT_SESSION_DISABLED || service->state == MQTT_SESSION_STOPPING)
    {
        return;
    }
    service->state                 = MQTT_SESSION_STOPPING;
    service->is_transport_selected = false;
    service->event_count           = 0;

    if (service->disconnect_transport != NULL)
    {
        service->disconnect_transport(service->callback_context);
    }
}

/* Gets an immutable health snapshot with redacted error information. */
mqtt_session_health_t mqtt_service_get_health(const mqtt_service_t *service)
{
    mqtt_session_health_t health = {0};

    if (service == NULL)
    {
        return health;
    }
    health.state                     = service->state;
    health.last_error_category       = service->last_error_category;
    health.selected_transport        = service->selected_transport;
    health.is_transport_selected     = service->is_transport_selected;
    health.reconnect_count           = service->reconnect_count;
    health.queued_event_count        = service->event_count;
    health.dropped_event_count       = service->dropped_events;
    health.subscription_replay_count = service->subscription_replay_count;
    health.retry_at_ms               = service->retry_at_ms;
    copy_text(health.last_error_detail, sizeof(health.last_error_detail), service->last_error_detail);

    return health;
}

/* Gets the stable diagnostic name associated with an MQTT session state. */
const char *mqtt_get_session_state_name(mqtt_session_state_t state)
{
    static const char *const names[] = {
        STATE_DISABLED, STATE_WAITING_FOR_TRANSPORT, STATE_CONNECTING, STATE_ONLINE, STATE_BACKOFF, STATE_STOPPING};

    return state <= MQTT_SESSION_STOPPING ? names[state] : STATE_UNKNOWN;
}

/* Gets the stable diagnostic name associated with an MQTT error category. */
const char *mqtt_get_error_category_name(mqtt_error_category_t category)
{
    static const char *const names[] = {ERROR_NONE,           ERROR_ROUTE,  ERROR_DNS,      ERROR_TLS,
                                        ERROR_AUTHENTICATION, ERROR_BROKER, ERROR_TRANSPORT};

    return category <= MQTT_ERROR_TRANSPORT ? names[category] : STATE_UNKNOWN;
}
