#include "platform_mqtt.h"

#include <net/if.h>
#include <stdatomic.h>
#include <stdio.h>
#include <string.h>

#include "esp_crt_bundle.h"
#include "esp_netif.h"
#include "esp_tls_errors.h"
#include "freertos/FreeRTOS.h"
#include "freertos/queue.h"
#include "freertos/task.h"
#include "mqtt_client.h"
#include "network_manager.h"
#include "sdkconfig.h"

/* URI and queue limits keep platform configuration bounded before transport startup. */
enum
{
    MQTT_BROKER_URI_MAX          = 192,
    MQTT_OUTBOUND_QUEUE_DEPTH    = 16,
    MQTT_MAXIMUM_INBOUND_PAYLOAD = 4096,
    MQTT_EVENT_QUEUE_DEPTH       = MQTT_EVENT_QUEUE_CAPACITY,
    MQTT_COMMAND_QUEUE_DEPTH     = 4,
    MQTT_TRANSPORT_TASK_STACK    = 4096,
    MQTT_TRANSPORT_TASK_PRIORITY = 5,
};

/* Transport commands isolate potentially waiting lifecycle calls from controller supervision. */
typedef enum
{
    MQTT_COMMAND_CONNECT,
    MQTT_COMMAND_DISCONNECT,
} mqtt_command_type_t;

typedef struct
{
    mqtt_command_type_t type;
    mqtt_broker_config_t config;
    network_link_id_t selected_link;
} mqtt_command_t;

/* Credential references identify platform-owned secrets without placing them in shared health. */
static const char MQTT_USERNAME_REFERENCE[]        = "sdkconfig:mqtt_username";
static const char MQTT_PASSWORD_REFERENCE[]        = "sdkconfig:mqtt_password";
static const char MQTT_URI_FORMAT[]                = "%s://%s:%d";
static const char MQTT_SCHEME_PLAIN[]              = "mqtt";
static const char MQTT_SCHEME_TLS[]                = "mqtts";
static const char MQTT_EVENT_CONNECTED_CODE[]      = "connected";
static const char MQTT_EVENT_DISCONNECTED_CODE[]   = "disconnected";
static const char MQTT_ERROR_AUTHENTICATION_CODE[] = "authentication_failed";
static const char MQTT_ERROR_BROKER_CODE[]         = "broker_refused";
static const char MQTT_ERROR_DNS_CODE[]            = "dns_failed";
static const char MQTT_ERROR_TLS_CODE[]            = "tls_failed";
static const char MQTT_ERROR_TRANSPORT_CODE[]      = "transport_failed";
static const char NETWORK_INTERFACE_WIFI_KEY[]     = "WIFI_STA_DEF";
static const char NETWORK_INTERFACE_ETHERNET_KEY[] = "ETH_DEF";

static char mqtt_broker_uri[MQTT_BROKER_URI_MAX];
static QueueHandle_t mqtt_event_queue;
static QueueHandle_t mqtt_command_queue;
static esp_mqtt_client_handle_t mqtt_client;
static atomic_uint_least32_t mqtt_event_sequence;
static atomic_bool is_failure_reported;
static const settings_nullable_string_t *mqtt_username;
static const settings_nullable_string_t *mqtt_password;

/* Kconfig omits disabled Boolean symbols, so expose TLS as an ordinary typed value. */
#ifdef CONFIG_CONTROLLER_MQTT_TLS_ENABLED
static const bool is_mqtt_tls_enabled = true;
#else
static const bool is_mqtt_tls_enabled = false;
#endif

/* Gets the neutral route selected by the platform configuration choice. */
static network_route_policy_t get_mqtt_link_policy(void)
{
#ifdef CONFIG_CONTROLLER_MQTT_LINK_WIFI
    return NETWORK_ROUTE_WIFI;
#elif defined(CONFIG_CONTROLLER_MQTT_LINK_ETHERNET)
    return NETWORK_ROUTE_ETHERNET;
#else
    return NETWORK_ROUTE_AUTOMATIC;
#endif
}

/* Tests whether Kconfig requests broker-side persistent session state. */
static bool is_mqtt_session_persistent(void)
{
#ifdef CONFIG_CONTROLLER_MQTT_PERSISTENT_SESSION
    return true;
#else
    return false;
#endif
}

/* Tests whether Kconfig requests a retained broker last will. */
static bool is_mqtt_last_will_retained(void)
{
#ifdef CONFIG_CONTROLLER_MQTT_LAST_WILL_RETAIN
    return true;
#else
    return false;
#endif
}

/* Gets the ESP-IDF interface object associated with one neutral link. */
static esp_netif_t *get_network_interface(network_link_id_t link_id)
{
    const char *key = link_id == NETWORK_LINK_WIFI ? NETWORK_INTERFACE_WIFI_KEY : NETWORK_INTERFACE_ETHERNET_KEY;
    return esp_netif_get_handle_from_ifkey(key);
}

/* Copies one stable event into the bounded callback queue without blocking ESP-MQTT. */
static void enqueue_transport_event(mqtt_transport_event_type_t type, mqtt_error_category_t category, const char *detail)
{
    if (mqtt_event_queue == NULL)
    {
        return;
    }
    const mqtt_queued_event_t event = {
        .type           = type,
        .sequence       = atomic_fetch_add(&mqtt_event_sequence, 1) + 1,
        .error_category = category,
    };
    mqtt_queued_event_t owned_event = event;
    (void)snprintf(owned_event.error_detail, sizeof(owned_event.error_detail), "%s", detail);
    (void)xQueueSend(mqtt_event_queue, &owned_event, 0);
}

/* Gets a redacted portable category from ESP-MQTT connection error details. */
static mqtt_error_category_t get_error_category(const esp_mqtt_error_codes_t *error)
{
    if (error == NULL)
    {
        return MQTT_ERROR_TRANSPORT;
    }
    if (error->error_type == MQTT_ERROR_TYPE_CONNECTION_REFUSED)
    {
        return error->connect_return_code == MQTT_CONNECTION_REFUSE_BAD_USERNAME ||
                       error->connect_return_code == MQTT_CONNECTION_REFUSE_NOT_AUTHORIZED
                   ? MQTT_ERROR_AUTHENTICATION
                   : MQTT_ERROR_BROKER;
    }
    if (error->esp_tls_last_esp_err == ESP_ERR_ESP_TLS_CANNOT_RESOLVE_HOSTNAME)
    {
        return MQTT_ERROR_DNS;
    }
    if (error->esp_tls_stack_err != 0 || error->esp_tls_cert_verify_flags != 0)
    {
        return MQTT_ERROR_TLS;
    }
    return MQTT_ERROR_TRANSPORT;
}

/* Gets a stable non-sensitive code for an MQTT error category. */
static const char *get_error_code(mqtt_error_category_t category)
{
    switch (category)
    {
        case MQTT_ERROR_AUTHENTICATION:
            return MQTT_ERROR_AUTHENTICATION_CODE;
        case MQTT_ERROR_BROKER:
            return MQTT_ERROR_BROKER_CODE;
        case MQTT_ERROR_DNS:
            return MQTT_ERROR_DNS_CODE;
        case MQTT_ERROR_TLS:
            return MQTT_ERROR_TLS_CODE;
        default:
            return MQTT_ERROR_TRANSPORT_CODE;
    }
}

/* Converts ESP-MQTT callbacks into bounded owned service events. */
static void handle_mqtt_event(void * /* context */, esp_event_base_t /* event_base */, int32_t event_id, void *event_data)
{
    const esp_mqtt_event_t *event = event_data;
    if (event_id == MQTT_EVENT_CONNECTED)
    {
        atomic_store(&is_failure_reported, false);
        enqueue_transport_event(MQTT_TRANSPORT_CONNECTED, MQTT_ERROR_NONE, MQTT_EVENT_CONNECTED_CODE);
    }
    else if (event_id == MQTT_EVENT_ERROR)
    {
        const mqtt_error_category_t category = get_error_category(event->error_handle);
        atomic_store(&is_failure_reported, true);
        enqueue_transport_event(MQTT_TRANSPORT_FAILED, category, get_error_code(category));
    }
    else if (event_id == MQTT_EVENT_DISCONNECTED && !atomic_load(&is_failure_reported))
    {
        enqueue_transport_event(MQTT_TRANSPORT_DISCONNECTED, MQTT_ERROR_TRANSPORT, MQTT_EVENT_DISCONNECTED_CODE);
    }
}

/* Stops and releases the current ESP-MQTT client from the transport task. */
static void stop_mqtt_client(void)
{
    if (mqtt_client == NULL)
    {
        return;
    }
    /* Suppress the expected callback because the supervisor already owns this transition. */
    atomic_store(&is_failure_reported, true);
    (void)esp_mqtt_client_stop(mqtt_client);
    (void)esp_mqtt_client_destroy(mqtt_client);
    mqtt_client = NULL;
}

/* Creates one ESP-MQTT client bound to the commanded interface. */
static bool start_mqtt_client(const mqtt_broker_config_t *config, network_link_id_t selected_link)
{
    struct ifreq interface         = {0};
    esp_netif_t *network_interface = get_network_interface(selected_link);
    if (network_interface == NULL || esp_netif_get_netif_impl_name(network_interface, interface.ifr_name) != ESP_OK)
    {
        return false;
    }
    stop_mqtt_client();
    atomic_store(&is_failure_reported, false);
    const esp_mqtt_client_config_t mqtt_config = {
        .broker.address.uri                    = config->uri,
        .broker.verification.crt_bundle_attach = config->tls_policy == MQTT_TLS_PLATFORM_TRUST ? esp_crt_bundle_attach : NULL,
        .credentials.username                  = platform_mqtt_get_username(),
        .credentials.client_id                 = config->client_id,
        .credentials.authentication.password   = platform_mqtt_get_password(),
        .session.disable_clean_session         = config->session_policy == MQTT_SESSION_PERSISTENT,
        .session.keepalive                     = config->keepalive_seconds,
        .session.last_will.topic               = config->last_will_topic,
        .session.last_will.msg                 = config->last_will_payload,
        .session.last_will.qos                 = config->last_will_qos,
        .session.last_will.retain              = config->is_last_will_retained,
        .network.disable_auto_reconnect        = true,
        .network.if_name                       = &interface,
    };
    mqtt_client = esp_mqtt_client_init(&mqtt_config);
    if (mqtt_client == NULL || esp_mqtt_client_register_event(mqtt_client, MQTT_EVENT_ANY, handle_mqtt_event, NULL) != ESP_OK ||
        esp_mqtt_client_start(mqtt_client) != ESP_OK)
    {
        stop_mqtt_client();
        return false;
    }
    return true;
}

/* Owns all potentially waiting ESP-MQTT lifecycle operations outside controller supervision. */
static void mqtt_transport_task(void * /* context */)
{
    mqtt_command_t command;
    for (;;)
    {
        if (xQueueReceive(mqtt_command_queue, &command, portMAX_DELAY) != pdTRUE)
        {
            continue;
        }
        if (command.type == MQTT_COMMAND_DISCONNECT)
        {
            stop_mqtt_client();
        }
        else if (!start_mqtt_client(&command.config, command.selected_link))
        {
            enqueue_transport_event(MQTT_TRANSPORT_FAILED, MQTT_ERROR_TRANSPORT, MQTT_ERROR_TRANSPORT_CODE);
        }
    }
}

/* Initializes bounded MQTT callback and lifecycle queues without contacting a broker. */
bool platform_mqtt_initialize(void)
{
    if (mqtt_event_queue != NULL && mqtt_command_queue != NULL)
    {
        return true;
    }
    mqtt_event_queue   = xQueueCreate(MQTT_EVENT_QUEUE_DEPTH, sizeof(mqtt_queued_event_t));
    mqtt_command_queue = xQueueCreate(MQTT_COMMAND_QUEUE_DEPTH, sizeof(mqtt_command_t));
    return mqtt_event_queue != NULL && mqtt_command_queue != NULL &&
           xTaskCreate(mqtt_transport_task, "mqtt_transport", MQTT_TRANSPORT_TASK_STACK, NULL, MQTT_TRANSPORT_TASK_PRIORITY,
                       NULL) == pdPASS;
}

/* Gets an eligible DNS-capable IP route without exposing network state to MQTT core. */
bool platform_mqtt_get_transport_route(mqtt_transport_route_t *route, void *context)
{
    const network_manager_t *network_manager = context;
    network_link_id_t selected_link;
    if (route == NULL || network_manager == NULL ||
        !network_manager_get_selected_link(network_manager, get_mqtt_link_policy(), true, &selected_link))
    {
        return false;
    }
    const network_link_snapshot_t snapshot = network_manager_get_link_snapshot(network_manager, selected_link);
    *route                                 = (mqtt_transport_route_t){
                                        .identifier = (uint32_t)selected_link,
                                        .generation = snapshot.transitioned_at_ms,
    };
    (void)snprintf(route->name, sizeof(route->name), "%s", network_get_link_id_name(selected_link));
    return true;
}

/* Queues a transport stop without waiting for ESP-MQTT teardown. */
void platform_mqtt_disconnect(void * /* context */)
{
    const mqtt_command_t command = {.type = MQTT_COMMAND_DISCONNECT};
    if (mqtt_command_queue != NULL)
    {
        (void)xQueueSend(mqtt_command_queue, &command, 0);
    }
}

/* Queues one asynchronous ESP-MQTT session bound to the selected interface. */
bool platform_mqtt_connect(const mqtt_broker_config_t *config, const mqtt_transport_route_t *route, void * /* context */)
{
    if (mqtt_command_queue == NULL || config == NULL || route == NULL || route->identifier >= NETWORK_LINK_COUNT)
    {
        return false;
    }
    const mqtt_command_t command = {
        .type          = MQTT_COMMAND_CONNECT,
        .config        = *config,
        .selected_link = (network_link_id_t)route->identifier,
    };
    return xQueueSend(mqtt_command_queue, &command, 0) == pdTRUE;
}

/* Gets one owned transport event without blocking, or reports an empty queue. */
bool platform_mqtt_get_event(mqtt_queued_event_t *event)
{
    return mqtt_event_queue != NULL && xQueueReceive(mqtt_event_queue, event, 0) == pdTRUE;
}

/* Replays subscriptions registered by the future bidirectional MQTT API. */
void platform_mqtt_replay_subscriptions(void * /* context */)
{
    /* Phase 6 owns the bounded subscription registry; no subscriptions exist yet. */
}

/* Gets typed MQTT settings using credentials from the persistent snapshot. */
void platform_mqtt_get_config(mqtt_broker_config_t *config, const controller_settings_t *settings)
{
    mqtt_username      = settings != NULL ? &settings->mqtt_username : NULL;
    mqtt_password      = settings != NULL ? &settings->mqtt_password : NULL;
    const char *scheme = is_mqtt_tls_enabled ? MQTT_SCHEME_TLS : MQTT_SCHEME_PLAIN;
    const int result   = snprintf(mqtt_broker_uri, sizeof(mqtt_broker_uri), MQTT_URI_FORMAT, scheme, CONFIG_CONTROLLER_MQTT_HOST,
                                  CONFIG_CONTROLLER_MQTT_PORT);
    /* An invalid or truncated URI disables the service through normal configuration validation. */
    if (result < 0 || (size_t)result >= sizeof(mqtt_broker_uri))
    {
        mqtt_broker_uri[0] = '\0';
    }
    *config = (mqtt_broker_config_t){
        .enabled                       = CONFIG_CONTROLLER_MQTT_HOST[0] != '\0',
        .uri                           = mqtt_broker_uri,
        .client_id                     = CONFIG_CONTROLLER_MQTT_CLIENT_ID,
        .username_reference            = mqtt_username != NULL && mqtt_username->is_set ? MQTT_USERNAME_REFERENCE : NULL,
        .password_reference            = mqtt_password != NULL && mqtt_password->is_set ? MQTT_PASSWORD_REFERENCE : NULL,
        .tls_policy                    = is_mqtt_tls_enabled ? MQTT_TLS_PLATFORM_TRUST : MQTT_TLS_DISABLED,
        .ca_reference                  = NULL,
        .keepalive_seconds             = CONFIG_CONTROLLER_MQTT_KEEPALIVE_SECONDS,
        .session_policy                = is_mqtt_session_persistent() ? MQTT_SESSION_PERSISTENT : MQTT_SESSION_CLEAN,
        .last_will_topic               = CONFIG_CONTROLLER_MQTT_LAST_WILL_TOPIC,
        .last_will_payload             = CONFIG_CONTROLLER_MQTT_LAST_WILL_PAYLOAD,
        .last_will_qos                 = CONFIG_CONTROLLER_MQTT_LAST_WILL_QOS,
        .is_last_will_retained         = is_mqtt_last_will_retained(),
        .maximum_outbound_queue_depth  = MQTT_OUTBOUND_QUEUE_DEPTH,
        .maximum_inbound_payload_bytes = MQTT_MAXIMUM_INBOUND_PAYLOAD,
        .initial_backoff_ms            = CONFIG_CONTROLLER_MQTT_INITIAL_BACKOFF_MS,
        .maximum_backoff_ms            = CONFIG_CONTROLLER_MQTT_MAXIMUM_BACKOFF_MS,
        .jitter_percent                = CONFIG_CONTROLLER_MQTT_BACKOFF_JITTER_PERCENT,
    };
}

/* Gets the configured broker username for use only by the MQTT transport adapter. */
const char *platform_mqtt_get_username(void)
{
    return mqtt_username != NULL && mqtt_username->is_set ? mqtt_username->value : NULL;
}

/* Gets the configured broker password for use only by the MQTT transport adapter. */
const char *platform_mqtt_get_password(void)
{
    return mqtt_password != NULL && mqtt_password->is_set ? mqtt_password->value : NULL;
}
