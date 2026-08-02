#pragma once

#include <stdbool.h>
#include <stddef.h>
#include <stdint.h>

/* Bounded storage limits prevent network callbacks from growing memory use. */
#define NETWORK_INTERFACE_NAME_MAX 16
#define NETWORK_ADDRESS_MAX 48
#define NETWORK_TRANSITION_REASON_MAX 48
#define NETWORK_EVENT_QUEUE_CAPACITY 16

/* Stable link identifiers let consumers select routes without driver headers. */
typedef enum {
    NETWORK_LINK_WIFI,
    NETWORK_LINK_ETHERNET,
    NETWORK_LINK_COUNT,
} network_link_id_t;

/* Neutral supervisor states shared by every network adapter implementation. */
typedef enum {
    NETWORK_LINK_DISABLED,
    NETWORK_LINK_STARTING,
    NETWORK_LINK_CONNECTING,
    NETWORK_LINK_ONLINE,
    NETWORK_LINK_DEGRADED,
    NETWORK_LINK_BACKOFF,
    NETWORK_LINK_STOPPED,
} network_link_state_t;

/* Adapter event types describe link changes without platform-specific values. */
typedef enum {
    NETWORK_EVENT_STARTED,
    NETWORK_EVENT_CONNECTING,
    NETWORK_EVENT_ONLINE,
    NETWORK_EVENT_DEGRADED,
    NETWORK_EVENT_CONNECTION_LOST,
    NETWORK_EVENT_FAILED,
    NETWORK_EVENT_STOPPED,
} network_event_type_t;

/* Consumer policies distinguish automatic selection from explicit binding. */
typedef enum {
    NETWORK_ROUTE_AUTOMATIC,
    NETWORK_ROUTE_WIFI,
    NETWORK_ROUTE_ETHERNET,
} network_route_policy_t;

typedef struct {
    bool enabled;
    uint8_t priority;
    uint32_t initial_backoff_ms;
    uint32_t maximum_backoff_ms;
    uint8_t jitter_percent;
    uint32_t stable_online_ms;
} network_link_config_t;

typedef struct {
    network_link_id_t link_id;
    network_event_type_t type;
    uint32_t sequence;
    const char *interface_name;
    const char *ipv4_address;
    const char *ipv6_address;
    bool dns_ready;
    const char *reason;
} network_event_t;

typedef struct {
    network_link_id_t link_id;
    network_event_type_t type;
    uint32_t sequence;
    char interface_name[NETWORK_INTERFACE_NAME_MAX];
    char ipv4_address[NETWORK_ADDRESS_MAX];
    char ipv6_address[NETWORK_ADDRESS_MAX];
    bool dns_ready;
    char reason[NETWORK_TRANSITION_REASON_MAX];
} network_queued_event_t;

typedef struct {
    network_link_id_t link_id;
    network_link_state_t state;
    char interface_name[NETWORK_INTERFACE_NAME_MAX];
    char ipv4_address[NETWORK_ADDRESS_MAX];
    char ipv6_address[NETWORK_ADDRESS_MAX];
    bool dns_ready;
    uint32_t retry_count;
    uint32_t last_event_sequence;
    uint64_t transitioned_at_ms;
    uint64_t retry_at_ms;
    char last_transition_reason[NETWORK_TRANSITION_REASON_MAX];
} network_link_snapshot_t;

/* Adapter start and stop callback used by a supervisor without blocking it. */
typedef void (*network_link_action_t)(network_link_id_t link_id, void *context);

/* Entropy callback used to calculate portable deterministic retry jitter. */
typedef uint32_t (*network_random_t)(void *context);

typedef struct {
    network_link_config_t config[NETWORK_LINK_COUNT];
    network_link_snapshot_t links[NETWORK_LINK_COUNT];
    network_queued_event_t events[NETWORK_EVENT_QUEUE_CAPACITY];
    size_t event_head;
    size_t event_count;
    uint32_t dropped_events;
    network_link_action_t start_link;
    network_link_action_t stop_link;
    network_random_t random;
    void *callback_context;
} network_manager_t;

/* Initializes independent link supervisors and starts each enabled adapter. */
void network_manager_init(network_manager_t *manager,
                          const network_link_config_t configs[NETWORK_LINK_COUNT],
                          network_link_action_t start_link,
                          network_link_action_t stop_link,
                          network_random_t random,
                          void *callback_context,
                          uint64_t now_ms);
/* Copies a short-lived adapter event into the bounded owned event queue. */
bool network_manager_enqueue_event(network_manager_t *manager,
                                   const network_event_t *event);

/* Processes queued events and advances retry and stability timers. */
void network_manager_process(network_manager_t *manager, uint64_t now_ms);

/* Enables or disables one link without changing the state of another link. */
void network_manager_set_enabled(network_manager_t *manager,
                                 network_link_id_t link_id, bool enabled,
                                 uint64_t now_ms);

/* Stops and immediately restarts one enabled link under supervisor ownership. */
void network_manager_reconnect(network_manager_t *manager,
                               network_link_id_t link_id, uint64_t now_ms);

/* Stops all enabled adapters and discards queued events during shutdown. */
void network_manager_shutdown(network_manager_t *manager, uint64_t now_ms);

/* Gets an owned snapshot for a valid link, or an empty snapshot otherwise. */
network_link_snapshot_t network_manager_get_link_snapshot(
    const network_manager_t *manager, network_link_id_t link_id);

/* Gets an eligible link for a route policy and reports whether one exists. */
bool network_manager_get_selected_link(const network_manager_t *manager,
                                       network_route_policy_t policy,
                                       bool require_dns,
                                       network_link_id_t *selected_link);

/* Gets the stable diagnostic name associated with a link identifier. */
const char *network_get_link_id_name(network_link_id_t link_id);

/* Gets the stable diagnostic name associated with a link state. */
const char *network_get_link_state_name(network_link_state_t state);
