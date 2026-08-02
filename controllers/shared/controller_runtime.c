#include "controller_runtime.h"

#include "controller_health.h"
#include "diagnostics.h"
#include "platform.h"

#define CONTROLLER_TASK_STACK_SIZE 4096
#define CONTROLLER_TASK_PRIORITY 5
#define STATUS_INTERVAL_MS 5000

static void controller_task(void *context)
{
    (void)context;
    char status[256];
    for (;;) {
        const controller_health_snapshot_t snapshot = controller_health_snapshot();
        controller_health_format(status, sizeof(status), &snapshot);
        diagnostics_emit(DIAGNOSTIC_INFO, "runtime", "heartbeat", "%s", status);
        platform_delay_ms(STATUS_INTERVAL_MS);
    }
}

bool controller_runtime_start(void)
{
    controller_health_init();
    return platform_start_task("controller_runtime", controller_task, NULL,
                               CONTROLLER_TASK_STACK_SIZE,
                               CONTROLLER_TASK_PRIORITY);
}
