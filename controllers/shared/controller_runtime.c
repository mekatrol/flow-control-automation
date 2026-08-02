#include "controller_runtime.h"

#include "controller_health.h"
#include "diagnostics.h"
#include "platform.h"
#include "network_manager.h"

#define CONTROLLER_TASK_STACK_SIZE 4096
#define CONTROLLER_TASK_PRIORITY 5
#define STATUS_INTERVAL_MS 5000
#define CONTROLLER_TICK_MS 100

static network_manager_t controller_network_manager;

const network_manager_t *controller_runtime_network_manager(void)
{
    return &controller_network_manager;
}

static void controller_task(void *context)
{
    (void)context;
    char status[256];
    uint64_t next_status_ms = platform_monotonic_ms();
    for (;;) {
        const uint64_t now_ms = platform_monotonic_ms();
        network_manager_process(&controller_network_manager, now_ms);
        if (now_ms >= next_status_ms) {
            const controller_health_snapshot_t snapshot = controller_health_snapshot();
            controller_health_format(status, sizeof(status), &snapshot);
            diagnostics_emit(DIAGNOSTIC_INFO, "runtime", "heartbeat", "%s", status);
            next_status_ms = now_ms + STATUS_INTERVAL_MS;
        }
        platform_delay_ms(CONTROLLER_TICK_MS);
    }
}

bool controller_runtime_start(void)
{
    const network_link_config_t network_configs[NETWORK_LINK_COUNT] = {
        [NETWORK_LINK_WIFI] = {.priority = 20},
        [NETWORK_LINK_ETHERNET] = {.priority = 10},
    };
    network_manager_init(&controller_network_manager, network_configs, NULL,
                         NULL, NULL, NULL, platform_monotonic_ms());
    controller_health_init();
    return platform_start_task("controller_runtime", controller_task, NULL,
                               CONTROLLER_TASK_STACK_SIZE,
                               CONTROLLER_TASK_PRIORITY);
}
