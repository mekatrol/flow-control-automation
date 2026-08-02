#include "controller_runtime.h"

#include "controller_health.h"
#include "diagnostics.h"
#include "platform.h"
#include "network_manager.h"

/* Runtime scheduling values balance responsive supervision with bounded CPU use. */
enum {
    CONTROLLER_TASK_STACK_SIZE = 4096,
    CONTROLLER_TASK_PRIORITY = 5,
    STATUS_INTERVAL_MS = 5000,
    CONTROLLER_TICK_MS = 100,
    STATUS_BUFFER_SIZE = 256,
    WIFI_ROUTE_PRIORITY = 20,
    ETHERNET_ROUTE_PRIORITY = 10,
};

/* Runtime diagnostic identifiers define the stable heartbeat event schema. */
static const char CONTROLLER_TASK_NAME[] = "controller_runtime";
static const char COMPONENT_RUNTIME[] = "runtime";
static const char EVENT_HEARTBEAT[] = "heartbeat";
static const char FORMAT_STATUS[] = "%s";

static network_manager_t controller_network_manager;

/* Gets the runtime-owned network manager for read-only consumer discovery. */
const network_manager_t *get_controller_runtime_network_manager(void)
{
    return &controller_network_manager;
}

/* Services communications state machines and emits heartbeat status indefinitely. */
static void controller_task(void *context)
{
    (void)context;
    char status[STATUS_BUFFER_SIZE];
    uint64_t next_status_ms = platform_get_monotonic_ms();
    for (;;) {
        const uint64_t now_ms = platform_get_monotonic_ms();
        /* Frequent bounded processing keeps retries responsive without blocking the task. */
        network_manager_process(&controller_network_manager, now_ms);
        if (now_ms >= next_status_ms) {
            const controller_health_snapshot_t snapshot = get_controller_health_snapshot();
            controller_health_format(status, sizeof(status), &snapshot);
            diagnostics_emit(DIAGNOSTIC_INFO, COMPONENT_RUNTIME,
                             EVENT_HEARTBEAT, FORMAT_STATUS, status);
            next_status_ms = now_ms + STATUS_INTERVAL_MS;
        }
        platform_delay_ms(CONTROLLER_TICK_MS);
    }
}

/* Starts the non-blocking controller runtime task and reports creation success. */
bool controller_runtime_start(void)
{
    const network_link_config_t network_configs[NETWORK_LINK_COUNT] = {
        [NETWORK_LINK_WIFI] = {.priority = WIFI_ROUTE_PRIORITY},
        [NETWORK_LINK_ETHERNET] = {.priority = ETHERNET_ROUTE_PRIORITY},
    };
    network_manager_init(&controller_network_manager, network_configs, NULL,
                         NULL, NULL, NULL, platform_get_monotonic_ms());
    controller_health_init();
    return platform_start_task(CONTROLLER_TASK_NAME, controller_task, NULL,
                               CONTROLLER_TASK_STACK_SIZE,
                               CONTROLLER_TASK_PRIORITY);
}
