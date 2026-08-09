#include "network/manager.h"

#include <string.h>

/* Percentage scale used to convert configured jitter into a delay range. */
enum
{
    PERCENT_SCALE                  = 100U,
    EXPONENTIAL_BACKOFF_MULTIPLIER = 2U
};

/* Transition reasons owned by the neutral supervisor rather than an adapter. */
static const char REASON_ENABLED[]           = "enabled";
static const char REASON_NOT_CONFIGURED[]    = "not_configured";
static const char REASON_RETRY_DUE[]         = "retry_due";
static const char REASON_MANUALLY_ENABLED[]  = "manually_enabled";
static const char REASON_MANUALLY_DISABLED[] = "manually_disabled";
static const char REASON_SHUTDOWN[]          = "shutdown";

/* Stable names expose neutral link identity without platform-specific types. */
static const char LINK_NAME_WIFI[]     = "wifi";
static const char LINK_NAME_ETHERNET[] = "ethernet";
static const char LINK_NAME_UNKNOWN[]  = "unknown";

/* Stable state names are ordered to match network_link_state_t. */
static const char STATE_DISABLED[]   = "disabled";
static const char STATE_STARTING[]   = "starting";
static const char STATE_CONNECTING[] = "connecting";
static const char STATE_ONLINE[]     = "online";
static const char STATE_DEGRADED[]   = "degraded";
static const char STATE_BACKOFF[]    = "backoff";
static const char STATE_STOPPED[]    = "stopped";

/* Copies optional callback text into owned bounded storage to prevent dangling data. */
static void copy_text(char *destination, size_t size, const char *source)
{
    /* An empty destination cannot accept even a terminator, so leave it untouched. */
    if (size == 0)
    {
        return;
    }

    /* Treat absent callback fields as empty because all queued fields are owned strings. */
    if (source == NULL)
    {
        source = "";
    }
    strncpy(destination, source, size - 1);
    /* Force termination because strncpy does not terminate truncated input. */
    destination[size - 1] = '\0';
}

/* Tests whether a link identifier can safely index manager-owned arrays. */
static bool is_valid_link(network_link_id_t link_id)
{
    return link_id >= NETWORK_LINK_WIFI && link_id < NETWORK_LINK_COUNT;
}

/* Records one state transition and its bounded diagnostic reason atomically to callers. */
static void set_link_state(network_link_snapshot_t *link, network_link_state_t state, uint64_t now_ms, const char *reason)
{
    link->state              = state;
    link->transitioned_at_ms = now_ms;
    copy_text(link->last_transition_reason, sizeof(link->last_transition_reason), reason);
}

/* Gets the bounded exponential retry delay with symmetric configured jitter. */
static uint32_t get_backoff_delay(network_manager_t *manager, network_link_id_t link_id)
{
    const network_link_config_t *config = &manager->config[link_id];
    uint64_t delay                      = config->initial_backoff_ms;
    uint32_t shift                      = manager->links[link_id].retry_count;

    if (shift > 0)
    {
        shift--;
    }

    while (shift-- > 0 && delay < config->maximum_backoff_ms)
    {
        /* Exponential growth spaces repeated failures without allowing an unbounded delay. */
        delay *= EXPONENTIAL_BACKOFF_MULTIPLIER;

        if (delay > config->maximum_backoff_ms)
        {
            delay = config->maximum_backoff_ms;
        }
    }
    const uint64_t range = delay * config->jitter_percent / PERCENT_SCALE;

    if (range != 0 && manager->random != NULL)
    {
        /* A symmetric inclusive range permits both the minimum and maximum jitter. */
        const uint64_t width = range * EXPONENTIAL_BACKOFF_MULTIPLIER + 1U;
        const int64_t offset = (int64_t)(manager->random(manager->callback_context) % width) - (int64_t)range;
        delay                = (uint64_t)((int64_t)delay + offset);
    }

    if (delay > config->maximum_backoff_ms)
    {
        delay = config->maximum_backoff_ms;
    }

    return (uint32_t)delay;
}

/* Moves a failed link into bounded backoff and clears stale reachability data. */
static void enter_backoff(network_manager_t *manager, network_link_id_t link_id, uint64_t now_ms, const char *reason)
{
    network_link_snapshot_t *link = &manager->links[link_id];

    if (link->retry_count != UINT32_MAX)
    {
        link->retry_count++;
    }
    set_link_state(link, NETWORK_LINK_BACKOFF, now_ms, reason);
    link->retry_at_ms     = now_ms + get_backoff_delay(manager, link_id);
    link->dns_ready       = false;
    link->ipv4_address[0] = '\0';
    link->ipv6_address[0] = '\0';

    /* Reset the adapter outside its callback so the next retry starts cleanly. */
    if (manager->stop_link != NULL)
    {
        manager->stop_link(link_id, manager->callback_context);
    }
}

/* Begins one non-blocking adapter start attempt and delegates work through its callback. */
static void begin_start(network_manager_t *manager, network_link_id_t link_id, uint64_t now_ms, const char *reason)
{
    set_link_state(&manager->links[link_id], NETWORK_LINK_STARTING, now_ms, reason);
    manager->links[link_id].retry_at_ms = 0;

    if (manager->start_link != NULL)
    {
        manager->start_link(link_id, manager->callback_context);
    }
}

/* Initializes independent link supervisors and starts each enabled adapter. */
void network_manager_init(network_manager_t *manager, const network_link_config_t configs[NETWORK_LINK_COUNT],
                          network_link_action_t start_link, network_link_action_t stop_link, network_random_t random,
                          void *callback_context, uint64_t now_ms)
{
    memset(manager, 0, sizeof(*manager));
    manager->start_link       = start_link;
    manager->stop_link        = stop_link;
    manager->random           = random;
    manager->callback_context = callback_context;

    for (network_link_id_t id = NETWORK_LINK_WIFI; id < NETWORK_LINK_COUNT; id++)
    {
        manager->config[id] = configs[id];

        /* Clamp invalid limits so exponential growth can never exceed its configured bound. */
        if (manager->config[id].maximum_backoff_ms < manager->config[id].initial_backoff_ms)
        {
            manager->config[id].maximum_backoff_ms = manager->config[id].initial_backoff_ms;
        }

        /* A percentage above the full delay would permit a negative retry duration. */
        if (manager->config[id].jitter_percent > PERCENT_SCALE)
        {
            manager->config[id].jitter_percent = PERCENT_SCALE;
        }
        manager->links[id].link_id = id;
        set_link_state(&manager->links[id], configs[id].enabled ? NETWORK_LINK_STARTING : NETWORK_LINK_DISABLED, now_ms,
                       configs[id].enabled ? REASON_ENABLED : REASON_NOT_CONFIGURED);

        if (configs[id].enabled && start_link != NULL)
        {
            start_link(id, callback_context);
        }
    }
}

/* Copies a short-lived adapter event into the bounded owned event queue. */
bool network_manager_enqueue_event(network_manager_t *manager, const network_event_t *event)
{
    if (manager == NULL || event == NULL || !is_valid_link(event->link_id))
    {
        return false;
    }

    /* Reject overload deterministically so callbacks never allocate or block. */
    if (manager->event_count == NETWORK_EVENT_QUEUE_CAPACITY)
    {
        manager->dropped_events++;

        return false;
    }
    const size_t tail = (manager->event_head + manager->event_count) % NETWORK_EVENT_QUEUE_CAPACITY;
    /* Copy every field because adapter-owned callback storage may expire on return. */
    network_queued_event_t *queued = &manager->events[tail];
    queued->link_id                = event->link_id;
    queued->type                   = event->type;
    queued->sequence               = event->sequence;
    queued->dns_ready              = event->dns_ready;
    copy_text(queued->interface_name, sizeof(queued->interface_name), event->interface_name);
    copy_text(queued->ipv4_address, sizeof(queued->ipv4_address), event->ipv4_address);
    copy_text(queued->ipv6_address, sizeof(queued->ipv6_address), event->ipv6_address);
    copy_text(queued->reason, sizeof(queued->reason), event->reason);
    manager->event_count++;

    return true;
}

/* Applies one fresh queued event while preserving independent per-link state. */
static void apply_event(network_manager_t *manager, const network_queued_event_t *event, uint64_t now_ms)
{
    network_link_snapshot_t *link = &manager->links[event->link_id];

    /* Ignore disabled, duplicate, and stale events so late callbacks cannot regress state. */
    if (!manager->config[event->link_id].enabled || event->sequence <= link->last_event_sequence)
    {
        return;
    }
    link->last_event_sequence = event->sequence;

    if (event->interface_name[0] != '\0')
    {
        copy_text(link->interface_name, sizeof(link->interface_name), event->interface_name);
    }

    switch (event->type)
    {
        case NETWORK_EVENT_STARTED:
            set_link_state(link, NETWORK_LINK_CONNECTING, now_ms, event->reason);
            break;
        case NETWORK_EVENT_CONNECTING:

            if (link->state != NETWORK_LINK_ONLINE)
            {
                set_link_state(link, NETWORK_LINK_CONNECTING, now_ms, event->reason);
            }
            break;
        case NETWORK_EVENT_ONLINE:

            /* Address families arrive independently, so an empty field preserves its peer. */
            if (event->ipv4_address[0] != '\0')
            {
                copy_text(link->ipv4_address, sizeof(link->ipv4_address), event->ipv4_address);
            }

            if (event->ipv6_address[0] != '\0')
            {
                copy_text(link->ipv6_address, sizeof(link->ipv6_address), event->ipv6_address);
            }
            link->dns_ready = event->dns_ready;
            set_link_state(link, NETWORK_LINK_ONLINE, now_ms, event->reason);
            break;
        case NETWORK_EVENT_DEGRADED:
            link->dns_ready = event->dns_ready;
            set_link_state(link, NETWORK_LINK_DEGRADED, now_ms, event->reason);
            break;
        case NETWORK_EVENT_CONNECTION_LOST:
        case NETWORK_EVENT_FAILED:
            enter_backoff(manager, event->link_id, now_ms, event->reason);
            break;
        case NETWORK_EVENT_STOPPED:

            /* An expected stop during backoff must not replace its scheduled retry. */
            if (link->state == NETWORK_LINK_BACKOFF)
            {
                break;
            }

            if (link->retry_count != UINT32_MAX)
            {
                link->retry_count++;
            }
            set_link_state(link, NETWORK_LINK_BACKOFF, now_ms, event->reason);
            link->retry_at_ms     = now_ms + get_backoff_delay(manager, event->link_id);
            link->dns_ready       = false;
            link->ipv4_address[0] = '\0';
            link->ipv6_address[0] = '\0';
            break;
    }
}

/* Processes queued events and advances retry and stability timers. */
void network_manager_process(network_manager_t *manager, uint64_t now_ms)
{
    /* Drain owned events outside adapter callbacks so state work never blocks a driver. */
    while (manager->event_count > 0)
    {
        const network_queued_event_t event = manager->events[manager->event_head];
        manager->event_head                = (manager->event_head + 1) % NETWORK_EVENT_QUEUE_CAPACITY;
        manager->event_count--;
        apply_event(manager, &event, now_ms);
    }

    for (network_link_id_t id = NETWORK_LINK_WIFI; id < NETWORK_LINK_COUNT; id++)
    {
        network_link_snapshot_t *link = &manager->links[id];

        if (link->state == NETWORK_LINK_BACKOFF && now_ms >= link->retry_at_ms)
        {
            begin_start(manager, id, now_ms, REASON_RETRY_DUE);
        }

        /* A stable connection proves old failures are no longer useful health information. */
        if (link->state == NETWORK_LINK_ONLINE && link->retry_count > 0 &&
            now_ms - link->transitioned_at_ms >= manager->config[id].stable_online_ms)
        {
            link->retry_count = 0;
        }
    }
}

/* Enables or disables one link without changing the state of another link. */
void network_manager_set_enabled(network_manager_t *manager, network_link_id_t link_id, bool enabled, uint64_t now_ms)
{
    if (!is_valid_link(link_id) || manager->config[link_id].enabled == enabled)
    {
        return;
    }
    manager->config[link_id].enabled = enabled;

    if (enabled)
    {
        begin_start(manager, link_id, now_ms, REASON_MANUALLY_ENABLED);
    }
    else
    {
        set_link_state(&manager->links[link_id], NETWORK_LINK_DISABLED, now_ms, REASON_MANUALLY_DISABLED);
        manager->links[link_id].dns_ready   = false;
        manager->links[link_id].retry_at_ms = 0;

        if (manager->stop_link != NULL)
        {
            manager->stop_link(link_id, manager->callback_context);
        }
    }
}

/* Stops and immediately restarts one enabled link under supervisor ownership. */
void network_manager_reconnect(network_manager_t *manager, network_link_id_t link_id, uint64_t now_ms)
{
    if (!is_valid_link(link_id) || !manager->config[link_id].enabled)
    {
        return;
    }
    /* Backoff ownership prevents the asynchronous stop event from cancelling restart. */
    set_link_state(&manager->links[link_id], NETWORK_LINK_BACKOFF, now_ms, REASON_MANUALLY_ENABLED);
    manager->links[link_id].retry_at_ms = now_ms;

    /* Stop first so an address and association cannot survive a manual reconnect. */
    if (manager->stop_link != NULL)
    {
        manager->stop_link(link_id, manager->callback_context);
    }
}

/* Stops all enabled adapters and discards queued events during shutdown. */
void network_manager_shutdown(network_manager_t *manager, uint64_t now_ms)
{
    for (network_link_id_t id = NETWORK_LINK_WIFI; id < NETWORK_LINK_COUNT; id++)
    {
        if (manager->config[id].enabled && manager->stop_link != NULL)
        {
            manager->stop_link(id, manager->callback_context);
        }
        manager->config[id].enabled = false;
        set_link_state(&manager->links[id], NETWORK_LINK_STOPPED, now_ms, REASON_SHUTDOWN);
    }
    manager->event_count = 0;
}

/* Gets an owned snapshot for a valid link, or an empty snapshot otherwise. */
network_link_snapshot_t network_manager_get_link_snapshot(const network_manager_t *manager, network_link_id_t link_id)
{
    network_link_snapshot_t empty = {0};

    return is_valid_link(link_id) ? manager->links[link_id] : empty;
}

/* Tests whether a link satisfies online and optional DNS route requirements. */
static bool is_link_eligible(const network_manager_t *manager, network_link_id_t id, bool require_dns)
{
    return manager->links[id].state == NETWORK_LINK_ONLINE && (!require_dns || manager->links[id].dns_ready);
}

/* Gets an eligible link for a route policy and reports whether one exists. */
bool network_manager_get_selected_link(const network_manager_t *manager, network_route_policy_t policy, bool require_dns,
                                       network_link_id_t *selected_link)
{
    if (manager == NULL || selected_link == NULL)
    {
        return false;
    }

    if (policy != NETWORK_ROUTE_AUTOMATIC)
    {
        if (policy != NETWORK_ROUTE_WIFI && policy != NETWORK_ROUTE_ETHERNET)
        {
            return false;
        }
        const network_link_id_t id = policy == NETWORK_ROUTE_WIFI ? NETWORK_LINK_WIFI : NETWORK_LINK_ETHERNET;

        if (!is_link_eligible(manager, id, require_dns))
        {
            return false;
        }

        *selected_link = id;
        return true;
    }
    /* Automatic selection chooses the lowest configured priority value. */
    bool found             = false;
    network_link_id_t best = NETWORK_LINK_WIFI;

    for (network_link_id_t id = NETWORK_LINK_WIFI; id < NETWORK_LINK_COUNT; id++)
    {
        if (is_link_eligible(manager, id, require_dns) &&
            (!found || manager->config[id].priority < manager->config[best].priority))
        {
            best  = id;
            found = true;
        }
    }

    if (found)
    {
        *selected_link = best;
    }

    return found;
}

/* Gets the stable diagnostic name associated with a link identifier. */
const char *network_get_link_id_name(network_link_id_t link_id)
{
    if (link_id == NETWORK_LINK_WIFI)
    {
        return LINK_NAME_WIFI;
    }

    if (link_id == NETWORK_LINK_ETHERNET)
    {
        return LINK_NAME_ETHERNET;
    }

    return LINK_NAME_UNKNOWN;
}

/* Gets the stable diagnostic name associated with a link state. */
const char *network_get_link_state_name(network_link_state_t state)
{
    static const char *const names[] = {STATE_DISABLED, STATE_STARTING, STATE_CONNECTING, STATE_ONLINE,
                                        STATE_DEGRADED, STATE_BACKOFF,  STATE_STOPPED};

    return state <= NETWORK_LINK_STOPPED ? names[state] : LINK_NAME_UNKNOWN;
}
