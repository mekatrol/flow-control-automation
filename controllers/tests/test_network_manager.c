#include <assert.h>
#include <stdio.h>
#include <string.h>

#include "network_manager.h"

typedef struct {
    unsigned starts[NETWORK_LINK_COUNT];
    unsigned stops[NETWORK_LINK_COUNT];
    uint32_t random_value;
} fixture_t;

static void start_link(network_link_id_t id, void *context)
{
    ((fixture_t *)context)->starts[id]++;
}

static void stop_link(network_link_id_t id, void *context)
{
    ((fixture_t *)context)->stops[id]++;
}

static uint32_t fixed_random(void *context)
{
    return ((fixture_t *)context)->random_value;
}

static network_manager_t create_manager(fixture_t *fixture, bool wifi,
                                        bool ethernet)
{
    const network_link_config_t configs[NETWORK_LINK_COUNT] = {
        [NETWORK_LINK_WIFI] = {wifi, 20, 100, 800, 25, 1000},
        [NETWORK_LINK_ETHERNET] = {ethernet, 10, 100, 800, 25, 1000},
    };
    network_manager_t manager;
    network_manager_init(&manager, configs, start_link, stop_link, fixed_random,
                         fixture, 0);
    return manager;
}

static void event(network_manager_t *manager, network_link_id_t id,
                  network_event_type_t type, uint32_t sequence,
                  bool dns, const char *reason)
{
    const network_event_t value = {
        .link_id = id, .type = type, .sequence = sequence,
        .interface_name = id == NETWORK_LINK_WIFI ? "wlan0" : "eth0",
        .ipv4_address = "192.0.2.10", .ipv6_address = "2001:db8::10",
        .dns_ready = dns, .reason = reason,
    };
    assert(network_manager_enqueue_event(manager, &value));
}

static void test_transitions_and_stale_events(void)
{
    fixture_t fixture = {0};
    network_manager_t manager = create_manager(&fixture, true, false);
    assert(manager.links[NETWORK_LINK_WIFI].state == NETWORK_LINK_STARTING);
    event(&manager, NETWORK_LINK_WIFI, NETWORK_EVENT_STARTED, 1, false, "driver_ready");
    network_manager_process(&manager, 10);
    assert(manager.links[NETWORK_LINK_WIFI].state == NETWORK_LINK_CONNECTING);
    event(&manager, NETWORK_LINK_WIFI, NETWORK_EVENT_ONLINE, 2, true, "address_ready");
    network_manager_process(&manager, 20);
    assert(manager.links[NETWORK_LINK_WIFI].state == NETWORK_LINK_ONLINE);
    assert(strcmp(manager.links[NETWORK_LINK_WIFI].ipv4_address, "192.0.2.10") == 0);
    event(&manager, NETWORK_LINK_WIFI, NETWORK_EVENT_FAILED, 2, false, "duplicate");
    event(&manager, NETWORK_LINK_WIFI, NETWORK_EVENT_FAILED, 1, false, "stale");
    network_manager_process(&manager, 30);
    assert(manager.links[NETWORK_LINK_WIFI].state == NETWORK_LINK_ONLINE);
    event(&manager, NETWORK_LINK_WIFI, NETWORK_EVENT_DEGRADED, 3, false, "dns_lost");
    network_manager_process(&manager, 40);
    assert(manager.links[NETWORK_LINK_WIFI].state == NETWORK_LINK_DEGRADED);
    event(&manager, NETWORK_LINK_WIFI, NETWORK_EVENT_CONNECTION_LOST, 4, false, "link_lost");
    network_manager_process(&manager, 50);
    assert(manager.links[NETWORK_LINK_WIFI].state == NETWORK_LINK_BACKOFF);
}

static void test_backoff_jitter_stability_and_shutdown(void)
{
    fixture_t fixture = {.random_value = 0};
    network_manager_t manager = create_manager(&fixture, true, true);
    event(&manager, NETWORK_LINK_WIFI, NETWORK_EVENT_FAILED, 1, false, "failed");
    network_manager_process(&manager, 1000);
    assert(manager.links[NETWORK_LINK_WIFI].retry_at_ms == 1075); /* 100 - 25% */
    network_manager_process(&manager, 1074);
    assert(fixture.starts[NETWORK_LINK_WIFI] == 1);
    network_manager_process(&manager, 1075);
    assert(fixture.starts[NETWORK_LINK_WIFI] == 2);
    event(&manager, NETWORK_LINK_WIFI, NETWORK_EVENT_FAILED, 2, false, "failed_again");
    network_manager_process(&manager, 1100);
    assert(manager.links[NETWORK_LINK_WIFI].retry_at_ms == 1250); /* 200 - 25% */
    event(&manager, NETWORK_LINK_WIFI, NETWORK_EVENT_ONLINE, 3, true, "recovered");
    network_manager_process(&manager, 1300);
    assert(manager.links[NETWORK_LINK_WIFI].retry_count == 2);
    network_manager_process(&manager, 2299);
    assert(manager.links[NETWORK_LINK_WIFI].retry_count == 2);
    network_manager_process(&manager, 2300);
    assert(manager.links[NETWORK_LINK_WIFI].retry_count == 0);
    network_manager_shutdown(&manager, 2400);
    assert(manager.links[NETWORK_LINK_WIFI].state == NETWORK_LINK_STOPPED);
    assert(manager.links[NETWORK_LINK_ETHERNET].state == NETWORK_LINK_STOPPED);
    assert(fixture.stops[NETWORK_LINK_WIFI] == 1);
    assert(fixture.stops[NETWORK_LINK_ETHERNET] == 1);
}

static void test_link_selection_and_independence(void)
{
    fixture_t fixture = {0};
    network_manager_t manager = create_manager(&fixture, true, true);
    network_link_id_t selected = NETWORK_LINK_WIFI;
    assert(!network_manager_select_link(&manager, NETWORK_ROUTE_AUTOMATIC, true,
                                        &selected));
    event(&manager, NETWORK_LINK_WIFI, NETWORK_EVENT_ONLINE, 1, true, "wifi_up");
    network_manager_process(&manager, 10);
    assert(network_manager_select_link(&manager, NETWORK_ROUTE_AUTOMATIC, true,
                                       &selected));
    assert(selected == NETWORK_LINK_WIFI);
    event(&manager, NETWORK_LINK_ETHERNET, NETWORK_EVENT_ONLINE, 1, true, "ethernet_up");
    network_manager_process(&manager, 20);
    assert(network_manager_select_link(&manager, NETWORK_ROUTE_AUTOMATIC, true,
                                       &selected));
    assert(selected == NETWORK_LINK_ETHERNET);
    assert(network_manager_select_link(&manager, NETWORK_ROUTE_WIFI, true, &selected));
    assert(selected == NETWORK_LINK_WIFI);
    event(&manager, NETWORK_LINK_ETHERNET, NETWORK_EVENT_FAILED, 2, false, "cable_lost");
    network_manager_process(&manager, 30);
    assert(manager.links[NETWORK_LINK_WIFI].state == NETWORK_LINK_ONLINE);
    assert(network_manager_select_link(&manager, NETWORK_ROUTE_AUTOMATIC, true,
                                       &selected));
    assert(selected == NETWORK_LINK_WIFI);
    assert(!network_manager_select_link(&manager, NETWORK_ROUTE_ETHERNET, true,
                                        &selected));
}

static void test_queue_bounds_and_enable_disable(void)
{
    fixture_t fixture = {0};
    network_manager_t manager = create_manager(&fixture, false, false);
    network_manager_set_enabled(&manager, NETWORK_LINK_WIFI, true, 10);
    assert(manager.links[NETWORK_LINK_WIFI].state == NETWORK_LINK_STARTING);
    network_manager_set_enabled(&manager, NETWORK_LINK_WIFI, false, 20);
    assert(manager.links[NETWORK_LINK_WIFI].state == NETWORK_LINK_DISABLED);
    for (uint32_t i = 1; i <= NETWORK_EVENT_QUEUE_CAPACITY; i++)
        event(&manager, NETWORK_LINK_ETHERNET, NETWORK_EVENT_STARTED, i, false, "queued");
    const network_event_t overflow = {.link_id = NETWORK_LINK_ETHERNET,
                                      .type = NETWORK_EVENT_STARTED,
                                      .sequence = 99};
    assert(!network_manager_enqueue_event(&manager, &overflow));
    assert(manager.dropped_events == 1);
}

static void test_event_queue_owns_callback_data(void)
{
    fixture_t fixture = {0};
    network_manager_t manager = create_manager(&fixture, true, false);
    char address[] = "198.51.100.7";
    char reason[] = "dhcp_ready";
    const network_event_t value = {
        .link_id = NETWORK_LINK_WIFI, .type = NETWORK_EVENT_ONLINE,
        .sequence = 1, .interface_name = "wlan0", .ipv4_address = address,
        .dns_ready = true, .reason = reason,
    };
    assert(network_manager_enqueue_event(&manager, &value));
    address[0] = 'X';
    reason[0] = 'X';
    network_manager_process(&manager, 10);
    assert(strcmp(manager.links[NETWORK_LINK_WIFI].ipv4_address,
                  "198.51.100.7") == 0);
    assert(strcmp(manager.links[NETWORK_LINK_WIFI].last_transition_reason,
                  "dhcp_ready") == 0);
}

int main(void)
{
    test_transitions_and_stale_events();
    test_backoff_jitter_stability_and_shutdown();
    test_link_selection_and_independence();
    test_queue_bounds_and_enable_disable();
    test_event_queue_owns_callback_data();
    puts("Network manager tests passed");
    return 0;
}
