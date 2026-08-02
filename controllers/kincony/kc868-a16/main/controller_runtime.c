#include "controller_runtime.h"

#include "controller_health.h"
#include "diagnostics.h"
#include "freertos/FreeRTOS.h"
#include "freertos/task.h"

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
        vTaskDelay(pdMS_TO_TICKS(STATUS_INTERVAL_MS));
    }
}

esp_err_t controller_runtime_start(void)
{
    controller_health_init();
    const BaseType_t result = xTaskCreate(controller_task, "controller_runtime",
                                          CONTROLLER_TASK_STACK_SIZE, NULL,
                                          CONTROLLER_TASK_PRIORITY, NULL);
    return result == pdPASS ? ESP_OK : ESP_ERR_NO_MEM;
}
