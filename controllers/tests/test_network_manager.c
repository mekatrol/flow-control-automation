#include <assert.h>
#include <stdio.h>
#include <string.h>

#include "network_manager.h"

/* Shared fixture values make supervisor timing and priority expectations explicit. */
enum {
    WIFI_PRIORITY = 20,
    ETHERNET_PRIORITY = 10,
    INITIAL_BACKOFF_MS = 100,
    MAXIMUM_BACKOFF_MS = 800,
    JITTER_PERCENT = 25,
    STABLE_ONLINE_MS = 1000,
    FIRST_SEQUENCE = 1,
    SECOND_SEQUENCE = 2,
    THIRD_SEQUENCE = 3,
    FOURTH_SEQUENCE = 4,
    FIRST_PROCESS_TIME_MS = 10,
    SECOND_PROCESS_TIME_MS = 20,
    THIRD_PROCESS_TIME_MS = 30,
    FOURTH_PROCESS_TIME_MS = 40,
    FIFTH_PROCESS_TIME_MS = 50,
    FAILURE_TIME_MS = 1000,
    FIRST_RETRY_TIME_MS = 1075,
    BEFORE_FIRST_RETRY_MS = 1074,
    SECOND_FAILURE_TIME_MS = 1100,
    SECOND_RETRY_TIME_MS = 1250,
    RECOVERY_TIME_MS = 1300,
    BEFORE_STABLE_RESET_MS = 2299,
    STABLE_RESET_TIME_MS = 2300,
    SHUTDOWN_TIME_MS = 2400,
    OVERFLOW_SEQUENCE = 99,
};

/* Synthetic interface data represents two independently addressable links. */
static const char WIFI_INTERFACE[] = "wlan0";
static const char ETHERNET_INTERFACE[] = "eth0";
static const char TEST_IPV4_ADDRESS[] = "192.0.2.10";
static const char TEST_IPV6_ADDRESS[] = "2001:db8::10";
static const char OWNED_IPV4_ADDRESS[] = "198.51.100.7";
static const char REASON_DRIVER_READY[] = "driver_ready";
static const char REASON_ADDRESS_READY[] = "address_ready";
static const char REASON_DUPLICATE[] = "duplicate";
static const char REASON_STALE[] = "stale";
static const char REASON_DNS_LOST[] = "dns_lost";
static const char REASON_LINK_LOST[] = "link_lost";
static const char REASON_FAILED[] = "failed";
static const char REASON_FAILED_AGAIN[] = "failed_again";
static const char REASON_RECOVERED[] = "recovered";
static const char REASON_WIFI_UP[] = "wifi_up";
static const char REASON_ETHERNET_UP[] = "ethernet_up";
static const char REASON_CABLE_LOST[] = "cable_lost";
static const char REASON_QUEUED[] = "queued";
static const char REASON_DHCP_READY[] = "dhcp_ready";
static const char TEST_SUCCESS_MESSAGE[] = "Network manager tests passed";

typedef struct {
    unsigned starts[NETWORK_LINK_COUNT];
    unsigned stops[NETWORK_LINK_COUNT];
    uint32_t random_value;
} fixture_t;

/* Records an adapter start request so tests can verify non-blocking supervision. */
static void start_link(network_link_id_t id, void *context)
{
    ((fixture_t *)context)->starts[id]++;
}

/* Records an adapter stop request so tests can verify isolated shutdown. */
static void stop_link(network_link_id_t id, void *context)
{
    ((fixture_t *)context)->stops[id]++;
}

/* Gets deterministic entropy so jitter boundary assertions remain reproducible. */
static uint32_t get_fixed_random(void *context)
{
    return ((fixture_t *)context)->random_value;
}

/* Gets a manager configured with deterministic link policies for a test case. */
static network_manager_t get_test_manager(fixture_t *fixture, bool wifi,
                                          bool ethernet)
{
    const network_link_config_t configs[NETWORK_LINK_COUNT] = {
        [NETWORK_LINK_WIFI] = {wifi, WIFI_PRIORITY, INITIAL_BACKOFF_MS,
                              MAXIMUM_BACKOFF_MS, JITTER_PERCENT,
                              STABLE_ONLINE_MS},
        [NETWORK_LINK_ETHERNET] = {ethernet, ETHERNET_PRIORITY,
                                  INITIAL_BACKOFF_MS, MAXIMUM_BACKOFF_MS,
                                  JITTER_PERCENT, STABLE_ONLINE_MS},
    };
    network_manager_t manager;
    network_manager_init(&manager, configs, start_link, stop_link,
                         get_fixed_random,
                         fixture, 0);
    return manager;
}

/* Enqueues one synthetic adapter event and requires the bounded queue to accept it. */
static void enqueue_event(network_manager_t *manager, network_link_id_t id,
                          network_event_type_t type, uint32_t sequence,
                          bool dns, const char *reason)
{
    const network_event_t value = {
        .link_id = id, .type = type, .sequence = sequence,
        .interface_name = id == NETWORK_LINK_WIFI ? WIFI_INTERFACE
                                                  : ETHERNET_INTERFACE,
        .ipv4_address = TEST_IPV4_ADDRESS,
        .ipv6_address = TEST_IPV6_ADDRESS,
        .dns_ready = dns, .reason = reason,
    };
    assert(network_manager_enqueue_event(manager, &value));
}

/* Verifies valid transitions and rejection of duplicate or stale event sequences. */
static void test_transitions_and_stale_events(void)
{
    fixture_t fixture = {0};
    network_manager_t manager = get_test_manager(&fixture, true, false);
    assert(manager.links[NETWORK_LINK_WIFI].state == NETWORK_LINK_STARTING);
    enqueue_event(&manager, NETWORK_LINK_WIFI, NETWORK_EVENT_STARTED,
                  FIRST_SEQUENCE, false,
                  REASON_DRIVER_READY);
    network_manager_process(&manager, FIRST_PROCESS_TIME_MS);
    assert(manager.links[NETWORK_LINK_WIFI].state == NETWORK_LINK_CONNECTING);
    enqueue_event(&manager, NETWORK_LINK_WIFI, NETWORK_EVENT_ONLINE,
                  SECOND_SEQUENCE, true,
                  REASON_ADDRESS_READY);
    network_manager_process(&manager, SECOND_PROCESS_TIME_MS);
    assert(manager.links[NETWORK_LINK_WIFI].state == NETWORK_LINK_ONLINE);
    assert(strcmp(manager.links[NETWORK_LINK_WIFI].ipv4_address,
                  TEST_IPV4_ADDRESS) == 0);
    enqueue_event(&manager, NETWORK_LINK_WIFI, NETWORK_EVENT_FAILED,
                  SECOND_SEQUENCE, false,
                  REASON_DUPLICATE);
    enqueue_event(&manager, NETWORK_LINK_WIFI, NETWORK_EVENT_FAILED,
                  FIRST_SEQUENCE, false,
                  REASON_STALE);
    network_manager_process(&manager, THIRD_PROCESS_TIME_MS);
    assert(manager.links[NETWORK_LINK_WIFI].state == NETWORK_LINK_ONLINE);
    enqueue_event(&manager, NETWORK_LINK_WIFI, NETWORK_EVENT_DEGRADED,
                  THIRD_SEQUENCE, false,
                  REASON_DNS_LOST);
    network_manager_process(&manager, FOURTH_PROCESS_TIME_MS);
    assert(manager.links[NETWORK_LINK_WIFI].state == NETWORK_LINK_DEGRADED);
    enqueue_event(&manager, NETWORK_LINK_WIFI, NETWORK_EVENT_CONNECTION_LOST,
                  FOURTH_SEQUENCE, false, REASON_LINK_LOST);
    network_manager_process(&manager, FIFTH_PROCESS_TIME_MS);
    assert(manager.links[NETWORK_LINK_WIFI].state == NETWORK_LINK_BACKOFF);
}

/* Verifies jitter bounds, exponential growth, stable reset, and clean shutdown. */
static void test_backoff_jitter_stability_and_shutdown(void)
{
    fixture_t fixture = {.random_value = 0};
    network_manager_t manager = get_test_manager(&fixture, true, true);
    enqueue_event(&manager, NETWORK_LINK_WIFI, NETWORK_EVENT_FAILED,
                  FIRST_SEQUENCE, false,
                  REASON_FAILED);
    network_manager_process(&manager, FAILURE_TIME_MS);
    assert(manager.links[NETWORK_LINK_WIFI].retry_at_ms == FIRST_RETRY_TIME_MS);
    network_manager_process(&manager, BEFORE_FIRST_RETRY_MS);
    assert(fixture.starts[NETWORK_LINK_WIFI] == 1);
    network_manager_process(&manager, FIRST_RETRY_TIME_MS);
    assert(fixture.starts[NETWORK_LINK_WIFI] == 2);
    enqueue_event(&manager, NETWORK_LINK_WIFI, NETWORK_EVENT_FAILED,
                  SECOND_SEQUENCE, false,
                  REASON_FAILED_AGAIN);
    network_manager_process(&manager, SECOND_FAILURE_TIME_MS);
    assert(manager.links[NETWORK_LINK_WIFI].retry_at_ms == SECOND_RETRY_TIME_MS);
    enqueue_event(&manager, NETWORK_LINK_WIFI, NETWORK_EVENT_ONLINE,
                  THIRD_SEQUENCE, true,
                  REASON_RECOVERED);
    network_manager_process(&manager, RECOVERY_TIME_MS);
    assert(manager.links[NETWORK_LINK_WIFI].retry_count == 2);
    network_manager_process(&manager, BEFORE_STABLE_RESET_MS);
    assert(manager.links[NETWORK_LINK_WIFI].retry_count == 2);
    network_manager_process(&manager, STABLE_RESET_TIME_MS);
    assert(manager.links[NETWORK_LINK_WIFI].retry_count == 0);
    network_manager_shutdown(&manager, SHUTDOWN_TIME_MS);
    assert(manager.links[NETWORK_LINK_WIFI].state == NETWORK_LINK_STOPPED);
    assert(manager.links[NETWORK_LINK_ETHERNET].state == NETWORK_LINK_STOPPED);
    assert(fixture.stops[NETWORK_LINK_WIFI] == 1);
    assert(fixture.stops[NETWORK_LINK_ETHERNET] == 1);
}

/* Verifies explicit policies and failover preserve independent link state. */
static void test_link_selection_and_independence(void)
{
    fixture_t fixture = {0};
    network_manager_t manager = get_test_manager(&fixture, true, true);
    network_link_id_t selected = NETWORK_LINK_WIFI;
    assert(!network_manager_get_selected_link(&manager, NETWORK_ROUTE_AUTOMATIC, true,
                                        &selected));
    enqueue_event(&manager, NETWORK_LINK_WIFI, NETWORK_EVENT_ONLINE,
                  FIRST_SEQUENCE, true,
                  REASON_WIFI_UP);
    network_manager_process(&manager, FIRST_PROCESS_TIME_MS);
    assert(network_manager_get_selected_link(&manager, NETWORK_ROUTE_AUTOMATIC, true,
                                       &selected));
    assert(selected == NETWORK_LINK_WIFI);
    enqueue_event(&manager, NETWORK_LINK_ETHERNET, NETWORK_EVENT_ONLINE,
                  FIRST_SEQUENCE,
                  true, REASON_ETHERNET_UP);
    network_manager_process(&manager, SECOND_PROCESS_TIME_MS);
    assert(network_manager_get_selected_link(&manager, NETWORK_ROUTE_AUTOMATIC, true,
                                       &selected));
    assert(selected == NETWORK_LINK_ETHERNET);
    assert(network_manager_get_selected_link(&manager, NETWORK_ROUTE_WIFI, true, &selected));
    assert(selected == NETWORK_LINK_WIFI);
    enqueue_event(&manager, NETWORK_LINK_ETHERNET, NETWORK_EVENT_FAILED,
                  SECOND_SEQUENCE,
                  false, REASON_CABLE_LOST);
    network_manager_process(&manager, THIRD_PROCESS_TIME_MS);
    assert(manager.links[NETWORK_LINK_WIFI].state == NETWORK_LINK_ONLINE);
    assert(network_manager_get_selected_link(&manager, NETWORK_ROUTE_AUTOMATIC, true,
                                       &selected));
    assert(selected == NETWORK_LINK_WIFI);
    assert(!network_manager_get_selected_link(&manager, NETWORK_ROUTE_ETHERNET, true,
                                        &selected));
}

/* Verifies manual control and deterministic rejection when the queue is full. */
static void test_queue_bounds_and_enable_disable(void)
{
    fixture_t fixture = {0};
    network_manager_t manager = get_test_manager(&fixture, false, false);
    network_manager_set_enabled(&manager, NETWORK_LINK_WIFI, true,
                                FIRST_PROCESS_TIME_MS);
    assert(manager.links[NETWORK_LINK_WIFI].state == NETWORK_LINK_STARTING);
    network_manager_set_enabled(&manager, NETWORK_LINK_WIFI, false,
                                SECOND_PROCESS_TIME_MS);
    assert(manager.links[NETWORK_LINK_WIFI].state == NETWORK_LINK_DISABLED);
    for (uint32_t i = 1; i <= NETWORK_EVENT_QUEUE_CAPACITY; i++)
        enqueue_event(&manager, NETWORK_LINK_ETHERNET, NETWORK_EVENT_STARTED, i,
                      false, REASON_QUEUED);
    const network_event_t overflow = {.link_id = NETWORK_LINK_ETHERNET,
                                      .type = NETWORK_EVENT_STARTED,
                                      .sequence = OVERFLOW_SEQUENCE};
    assert(!network_manager_enqueue_event(&manager, &overflow));
    assert(manager.dropped_events == 1);
}

/* Verifies queued events own callback strings after adapter storage changes. */
static void test_event_queue_owns_callback_data(void)
{
    fixture_t fixture = {0};
    network_manager_t manager = get_test_manager(&fixture, true, false);
    char address[sizeof(OWNED_IPV4_ADDRESS)];
    char reason[sizeof(REASON_DHCP_READY)];
    /* Mutable copies simulate callback-owned storage that changes after enqueueing. */
    (void)strcpy(address, OWNED_IPV4_ADDRESS);
    (void)strcpy(reason, REASON_DHCP_READY);
    const network_event_t value = {
        .link_id = NETWORK_LINK_WIFI, .type = NETWORK_EVENT_ONLINE,
        .sequence = FIRST_SEQUENCE, .interface_name = WIFI_INTERFACE,
        .ipv4_address = address,
        .dns_ready = true, .reason = reason,
    };
    assert(network_manager_enqueue_event(&manager, &value));
    address[0] = 'X';
    reason[0] = 'X';
    network_manager_process(&manager, FIRST_PROCESS_TIME_MS);
    assert(strcmp(manager.links[NETWORK_LINK_WIFI].ipv4_address,
                  OWNED_IPV4_ADDRESS) == 0);
    assert(strcmp(manager.links[NETWORK_LINK_WIFI].last_transition_reason,
                  REASON_DHCP_READY) == 0);
}

/* Runs all network manager unit cases and returns success when assertions hold. */
int main(void)
{
    test_transitions_and_stale_events();
    test_backoff_jitter_stability_and_shutdown();
    test_link_selection_and_independence();
    test_queue_bounds_and_enable_disable();
    test_event_queue_owns_callback_data();
    puts(TEST_SUCCESS_MESSAGE);
    return 0;
}
