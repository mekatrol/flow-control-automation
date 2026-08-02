#include "network_manager.h"

#include <string.h>

static void copy_text(char *destination, size_t size, const char *source)
{
    if (size == 0) return;
    if (source == NULL) source = "";
    (void)strncpy(destination, source, size - 1);
    destination[size - 1] = '\0';
}

static bool valid_link(network_link_id_t link_id)
{
    return link_id >= NETWORK_LINK_WIFI && link_id < NETWORK_LINK_COUNT;
}

static void transition(network_link_snapshot_t *link,
                       network_link_state_t state, uint64_t now_ms,
                       const char *reason)
{
    link->state = state;
    link->transitioned_at_ms = now_ms;
    copy_text(link->last_transition_reason,
              sizeof(link->last_transition_reason), reason);
}

static uint32_t backoff_delay(network_manager_t *manager,
                              network_link_id_t link_id)
{
    const network_link_config_t *config = &manager->config[link_id];
    uint64_t delay = config->initial_backoff_ms;
    uint32_t shift = manager->links[link_id].retry_count;
    if (shift > 0) shift--;
    while (shift-- > 0 && delay < config->maximum_backoff_ms) {
        delay *= 2;
        if (delay > config->maximum_backoff_ms) delay = config->maximum_backoff_ms;
    }
    const uint64_t range = delay * config->jitter_percent / 100U;
    if (range != 0 && manager->random != NULL) {
        const uint64_t width = range * 2U + 1U;
        const int64_t offset = (int64_t)(manager->random(manager->callback_context) % width) -
                               (int64_t)range;
        delay = (uint64_t)((int64_t)delay + offset);
    }
    if (delay > config->maximum_backoff_ms) delay = config->maximum_backoff_ms;
    return (uint32_t)delay;
}

static void enter_backoff(network_manager_t *manager, network_link_id_t link_id,
                          uint64_t now_ms, const char *reason)
{
    network_link_snapshot_t *link = &manager->links[link_id];
    if (link->retry_count != UINT32_MAX) link->retry_count++;
    transition(link, NETWORK_LINK_BACKOFF, now_ms, reason);
    link->retry_at_ms = now_ms + backoff_delay(manager, link_id);
    link->dns_ready = false;
    link->ipv4_address[0] = '\0';
    link->ipv6_address[0] = '\0';
}

static void begin_start(network_manager_t *manager, network_link_id_t link_id,
                        uint64_t now_ms, const char *reason)
{
    transition(&manager->links[link_id], NETWORK_LINK_STARTING, now_ms, reason);
    manager->links[link_id].retry_at_ms = 0;
    if (manager->start_link != NULL)
        manager->start_link(link_id, manager->callback_context);
}

void network_manager_init(network_manager_t *manager,
                          const network_link_config_t configs[NETWORK_LINK_COUNT],
                          network_link_action_t start_link,
                          network_link_action_t stop_link,
                          network_random_t random,
                          void *callback_context,
                          uint64_t now_ms)
{
    memset(manager, 0, sizeof(*manager));
    manager->start_link = start_link;
    manager->stop_link = stop_link;
    manager->random = random;
    manager->callback_context = callback_context;
    for (network_link_id_t id = NETWORK_LINK_WIFI; id < NETWORK_LINK_COUNT; id++) {
        manager->config[id] = configs[id];
        if (manager->config[id].maximum_backoff_ms <
            manager->config[id].initial_backoff_ms)
            manager->config[id].maximum_backoff_ms =
                manager->config[id].initial_backoff_ms;
        if (manager->config[id].jitter_percent > 100)
            manager->config[id].jitter_percent = 100;
        manager->links[id].link_id = id;
        transition(&manager->links[id],
                   configs[id].enabled ? NETWORK_LINK_STARTING
                                       : NETWORK_LINK_DISABLED,
                   now_ms, configs[id].enabled ? "enabled" : "not_configured");
        if (configs[id].enabled && start_link != NULL)
            start_link(id, callback_context);
    }
}

bool network_manager_enqueue_event(network_manager_t *manager,
                                   const network_event_t *event)
{
    if (manager == NULL || event == NULL || !valid_link(event->link_id)) return false;
    if (manager->event_count == NETWORK_EVENT_QUEUE_CAPACITY) {
        manager->dropped_events++;
        return false;
    }
    const size_t tail = (manager->event_head + manager->event_count) %
                        NETWORK_EVENT_QUEUE_CAPACITY;
    network_queued_event_t *queued = &manager->events[tail];
    queued->link_id = event->link_id;
    queued->type = event->type;
    queued->sequence = event->sequence;
    queued->dns_ready = event->dns_ready;
    copy_text(queued->interface_name, sizeof(queued->interface_name),
              event->interface_name);
    copy_text(queued->ipv4_address, sizeof(queued->ipv4_address),
              event->ipv4_address);
    copy_text(queued->ipv6_address, sizeof(queued->ipv6_address),
              event->ipv6_address);
    copy_text(queued->reason, sizeof(queued->reason), event->reason);
    manager->event_count++;
    return true;
}

static void apply_event(network_manager_t *manager,
                        const network_queued_event_t *event,
                        uint64_t now_ms)
{
    network_link_snapshot_t *link = &manager->links[event->link_id];
    if (!manager->config[event->link_id].enabled ||
        event->sequence <= link->last_event_sequence)
        return;
    link->last_event_sequence = event->sequence;
    if (event->interface_name[0] != '\0')
        copy_text(link->interface_name, sizeof(link->interface_name),
                  event->interface_name);
    switch (event->type) {
    case NETWORK_EVENT_STARTED:
        transition(link, NETWORK_LINK_CONNECTING, now_ms, event->reason);
        break;
    case NETWORK_EVENT_CONNECTING:
        if (link->state != NETWORK_LINK_ONLINE)
            transition(link, NETWORK_LINK_CONNECTING, now_ms, event->reason);
        break;
    case NETWORK_EVENT_ONLINE:
        copy_text(link->ipv4_address, sizeof(link->ipv4_address),
                  event->ipv4_address);
        copy_text(link->ipv6_address, sizeof(link->ipv6_address),
                  event->ipv6_address);
        link->dns_ready = event->dns_ready;
        transition(link, NETWORK_LINK_ONLINE, now_ms, event->reason);
        break;
    case NETWORK_EVENT_DEGRADED:
        link->dns_ready = event->dns_ready;
        transition(link, NETWORK_LINK_DEGRADED, now_ms, event->reason);
        break;
    case NETWORK_EVENT_CONNECTION_LOST:
    case NETWORK_EVENT_FAILED:
        enter_backoff(manager, event->link_id, now_ms, event->reason);
        break;
    case NETWORK_EVENT_STOPPED:
        transition(link, NETWORK_LINK_STOPPED, now_ms, event->reason);
        link->dns_ready = false;
        link->ipv4_address[0] = '\0';
        link->ipv6_address[0] = '\0';
        break;
    }
}

void network_manager_process(network_manager_t *manager, uint64_t now_ms)
{
    while (manager->event_count > 0) {
        const network_queued_event_t event = manager->events[manager->event_head];
        manager->event_head = (manager->event_head + 1) %
                              NETWORK_EVENT_QUEUE_CAPACITY;
        manager->event_count--;
        apply_event(manager, &event, now_ms);
    }
    for (network_link_id_t id = NETWORK_LINK_WIFI; id < NETWORK_LINK_COUNT; id++) {
        network_link_snapshot_t *link = &manager->links[id];
        if (link->state == NETWORK_LINK_BACKOFF && now_ms >= link->retry_at_ms)
            begin_start(manager, id, now_ms, "retry_due");
        if (link->state == NETWORK_LINK_ONLINE && link->retry_count > 0 &&
            now_ms - link->transitioned_at_ms >= manager->config[id].stable_online_ms)
            link->retry_count = 0;
    }
}

void network_manager_set_enabled(network_manager_t *manager,
                                 network_link_id_t link_id, bool enabled,
                                 uint64_t now_ms)
{
    if (!valid_link(link_id) || manager->config[link_id].enabled == enabled) return;
    manager->config[link_id].enabled = enabled;
    if (enabled) {
        begin_start(manager, link_id, now_ms, "manually_enabled");
    } else {
        transition(&manager->links[link_id], NETWORK_LINK_DISABLED, now_ms,
                   "manually_disabled");
        manager->links[link_id].dns_ready = false;
        manager->links[link_id].retry_at_ms = 0;
        if (manager->stop_link != NULL)
            manager->stop_link(link_id, manager->callback_context);
    }
}

void network_manager_shutdown(network_manager_t *manager, uint64_t now_ms)
{
    for (network_link_id_t id = NETWORK_LINK_WIFI; id < NETWORK_LINK_COUNT; id++) {
        if (manager->config[id].enabled && manager->stop_link != NULL)
            manager->stop_link(id, manager->callback_context);
        manager->config[id].enabled = false;
        transition(&manager->links[id], NETWORK_LINK_STOPPED, now_ms, "shutdown");
    }
    manager->event_count = 0;
}

network_link_snapshot_t network_manager_link_snapshot(
    const network_manager_t *manager, network_link_id_t link_id)
{
    network_link_snapshot_t empty = {0};
    return valid_link(link_id) ? manager->links[link_id] : empty;
}

static bool eligible(const network_manager_t *manager, network_link_id_t id,
                     bool require_dns)
{
    return manager->links[id].state == NETWORK_LINK_ONLINE &&
           (!require_dns || manager->links[id].dns_ready);
}

bool network_manager_select_link(const network_manager_t *manager,
                                 network_route_policy_t policy,
                                 bool require_dns,
                                 network_link_id_t *selected_link)
{
    if (manager == NULL || selected_link == NULL) return false;
    if (policy != NETWORK_ROUTE_AUTOMATIC) {
        if (policy != NETWORK_ROUTE_WIFI && policy != NETWORK_ROUTE_ETHERNET)
            return false;
        const network_link_id_t id = policy == NETWORK_ROUTE_WIFI
                                         ? NETWORK_LINK_WIFI
                                         : NETWORK_LINK_ETHERNET;
        if (!eligible(manager, id, require_dns)) return false;
        *selected_link = id;
        return true;
    }
    bool found = false;
    network_link_id_t best = NETWORK_LINK_WIFI;
    for (network_link_id_t id = NETWORK_LINK_WIFI; id < NETWORK_LINK_COUNT; id++) {
        if (eligible(manager, id, require_dns) &&
            (!found || manager->config[id].priority < manager->config[best].priority)) {
            best = id;
            found = true;
        }
    }
    if (found) *selected_link = best;
    return found;
}

const char *network_link_id_name(network_link_id_t link_id)
{
    if (link_id == NETWORK_LINK_WIFI) return "wifi";
    if (link_id == NETWORK_LINK_ETHERNET) return "ethernet";
    return "unknown";
}

const char *network_link_state_name(network_link_state_t state)
{
    static const char *const names[] = {
        "disabled", "starting", "connecting", "online", "degraded",
        "backoff", "stopped"
    };
    return state <= NETWORK_LINK_STOPPED ? names[state] : "unknown";
}
