#include "controller_runtime.h"

#include "board.h"
#include "controller_health.h"
#include "diagnostics.h"
#include "ethernet_link.h"
#include "network_manager.h"
#include "platform.h"

/* Runtime scheduling values balance responsive supervision with bounded CPU use. */
enum
{
    CONTROLLER_TASK_STACK_SIZE      = 4096,
    CONTROLLER_TASK_PRIORITY        = 5,
    STATUS_INTERVAL_MS              = 5000,
    CONTROLLER_TICK_MS              = 100,
    STATUS_BUFFER_SIZE              = 256,
    ETHERNET_ROUTE_PRIORITY         = 10,
    ETHERNET_INITIAL_BACKOFF_MS     = 1000,
    ETHERNET_MAXIMUM_BACKOFF_MS     = 60000,
    ETHERNET_BACKOFF_JITTER_PERCENT = 20,
    ETHERNET_STABLE_ONLINE_MS       = 30000,
};

/* Runtime diagnostic identifiers define the stable heartbeat event schema. */
static const char CONTROLLER_TASK_NAME[] = "controller_runtime";
static const char COMPONENT_RUNTIME[]    = "runtime";
static const char EVENT_HEARTBEAT[]      = "heartbeat";
static const char FORMAT_STATUS[]        = "%s";

static network_manager_t controller_network_manager;
static ethernet_link_t controller_ethernet_link;

/* Dispatches a supervisor start action to its independent link adapter. */
static void start_network_link(network_link_id_t link_id, void * /* context */)
{
    /* Dispatch only Ethernet because Wi-Fi is intentionally dormant. */
    if (link_id == NETWORK_LINK_ETHERNET)
    {
        ethernet_link_start(&controller_ethernet_link);
    }
}

/* Dispatches a supervisor stop action to its independent link adapter. */
static void stop_network_link(network_link_id_t link_id, void * /* context */)
{
    /* Stop only the independently owned Ethernet interface. */
    if (link_id == NETWORK_LINK_ETHERNET)
    {
        ethernet_link_stop(&controller_ethernet_link);
    }
}

/* Gets platform entropy through the callback signature required by the supervisor. */
static uint32_t get_network_random(void * /* context */)
{
    return platform_get_random_u32();
}

/* Initializes networking after task startup so boot never waits for association. */
static void initialize_networking(void)
{
    ethernet_link_config_t ethernet_config;
    controller_board_get_ethernet_config(&ethernet_config);
    const bool is_ethernet_ready                                    = ethernet_link_init(&controller_ethernet_link, &controller_network_manager, &ethernet_config);
    const network_link_config_t network_configs[NETWORK_LINK_COUNT] = {
        [NETWORK_LINK_WIFI] =
            {
                .enabled = false,
            },
        [NETWORK_LINK_ETHERNET] =
            {
                .enabled            = is_ethernet_ready && ethernet_config.enabled,
                .priority           = ETHERNET_ROUTE_PRIORITY,
                .initial_backoff_ms = ETHERNET_INITIAL_BACKOFF_MS,
                .maximum_backoff_ms = ETHERNET_MAXIMUM_BACKOFF_MS,
                .jitter_percent     = ETHERNET_BACKOFF_JITTER_PERCENT,
                .stable_online_ms   = ETHERNET_STABLE_ONLINE_MS,
            },
    };
    network_manager_init(&controller_network_manager, network_configs, start_network_link, stop_network_link, get_network_random, NULL, platform_get_monotonic_ms());
}

/* Gets the runtime-owned network manager for read-only consumer discovery. */
const network_manager_t *get_controller_runtime_network_manager(void)
{
    return &controller_network_manager;
}

/* Services communications state machines and emits heartbeat status indefinitely. */
static void controller_task(void * /* context */)
{
    char status[STATUS_BUFFER_SIZE];
    uint64_t next_status_ms = platform_get_monotonic_ms();
    initialize_networking();
    for (;;)
    {
        const uint64_t now_ms = platform_get_monotonic_ms();
        /* Ethernet callbacks are drained first so supervision sees current link state. */
        ethernet_link_process(&controller_ethernet_link);
        /* Frequent bounded processing keeps retries responsive without blocking the task. */
        network_manager_process(&controller_network_manager, now_ms);
        if (now_ms >= next_status_ms)
        {
            const controller_health_snapshot_t snapshot = get_controller_health_snapshot();
            controller_health_format(status, sizeof(status), &snapshot);
            diagnostics_emit(DIAGNOSTIC_INFO, COMPONENT_RUNTIME, EVENT_HEARTBEAT, FORMAT_STATUS, status);
            next_status_ms = now_ms + STATUS_INTERVAL_MS;
        }
        platform_delay_ms(CONTROLLER_TICK_MS);
    }
}

/* Starts the non-blocking controller runtime task and reports creation success. */
bool controller_runtime_start(void)
{
    controller_health_init();
    return platform_start_task(CONTROLLER_TASK_NAME, controller_task, NULL, CONTROLLER_TASK_STACK_SIZE, CONTROLLER_TASK_PRIORITY);
}
