#include "platform.h"

#include <stdio.h>

#include "esp_app_desc.h"
#include "esp_chip_info.h"
#include "esp_flash.h"
#include "esp_heap_caps.h"
#include "esp_log.h"
#include "esp_system.h"
#include "esp_timer.h"
#include "freertos/FreeRTOS.h"
#include "freertos/task.h"

static const char *reset_reason_name(esp_reset_reason_t reason)
{
    switch (reason) {
    case ESP_RST_POWERON: return "power_on";
    case ESP_RST_EXT: return "external";
    case ESP_RST_SW: return "software";
    case ESP_RST_PANIC: return "panic";
    case ESP_RST_INT_WDT: return "interrupt_watchdog";
    case ESP_RST_TASK_WDT: return "task_watchdog";
    case ESP_RST_WDT: return "watchdog";
    case ESP_RST_DEEPSLEEP: return "deep_sleep";
    case ESP_RST_BROWNOUT: return "brownout";
    default: return "unknown";
    }
}

void platform_get_startup_info(platform_startup_info_t *info)
{
    esp_chip_info_t chip;
    uint32_t flash_size = 0;
    esp_chip_info(&chip);
    (void)esp_flash_get_size(NULL, &flash_size);
    const esp_app_desc_t *app = esp_app_get_description();
    *info = (platform_startup_info_t) {
        .firmware_name = app->project_name,
        .firmware_version = app->version,
        .processor = CONFIG_IDF_TARGET,
        .reset_reason = reset_reason_name(esp_reset_reason()),
        .processor_cores = (uint32_t)chip.cores,
        .silicon_revision_major = (uint32_t)(chip.revision / 100),
        .silicon_revision_minor = (uint32_t)(chip.revision % 100),
        .flash_bytes = flash_size,
        .external_ram_bytes = heap_caps_get_total_size(MALLOC_CAP_SPIRAM),
    };
}

uint64_t platform_monotonic_ms(void)
{
    return (uint64_t)(esp_timer_get_time() / 1000);
}

uint64_t platform_free_heap_bytes(void)
{
    return heap_caps_get_free_size(MALLOC_CAP_8BIT);
}

bool platform_start_task(const char *name, platform_task_function_t function,
                         void *context, size_t stack_size, unsigned priority)
{
    return xTaskCreate(function, name, (uint32_t)stack_size, context,
                       (UBaseType_t)priority, NULL) == pdPASS;
}

void platform_delay_ms(uint32_t delay_ms)
{
    vTaskDelay(pdMS_TO_TICKS(delay_ms));
}

void platform_log(platform_log_level_t level, const char *component,
                  const char *message)
{
    esp_log_level_t esp_level = ESP_LOG_INFO;
    if (level == PLATFORM_LOG_DEBUG) esp_level = ESP_LOG_DEBUG;
    if (level == PLATFORM_LOG_WARNING) esp_level = ESP_LOG_WARN;
    if (level == PLATFORM_LOG_ERROR) esp_level = ESP_LOG_ERROR;
    esp_log_write(esp_level, component, "%s\n", message);
}
