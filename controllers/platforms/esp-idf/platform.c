#include "platform.h"

#include <stdio.h>

#include "esp_app_desc.h"
#include "esp_chip_info.h"
#include "esp_flash.h"
#include "esp_heap_caps.h"
#include "esp_log.h"
#include "esp_random.h"
#include "esp_system.h"
#include "esp_timer.h"
#include "freertos/FreeRTOS.h"
#include "freertos/task.h"

/* ESP timer values are microseconds and shared code consumes milliseconds. */
enum
{
    MICROSECONDS_PER_MILLISECOND = 1000,
    SILICON_REVISION_SCALE       = 100
};

/* Stable reset names prevent ESP-IDF enum values from leaking into shared code. */
static const char RESET_POWER_ON[]           = "power_on";
static const char RESET_EXTERNAL[]           = "external";
static const char RESET_SOFTWARE[]           = "software";
static const char RESET_PANIC[]              = "panic";
static const char RESET_INTERRUPT_WATCHDOG[] = "interrupt_watchdog";
static const char RESET_TASK_WATCHDOG[]      = "task_watchdog";
static const char RESET_WATCHDOG[]           = "watchdog";
static const char RESET_DEEP_SLEEP[]         = "deep_sleep";
static const char RESET_BROWNOUT[]           = "brownout";
static const char RESET_UNKNOWN[]            = "unknown";
static const char LOG_LINE_FORMAT[]          = "%s\n";

/* Gets a portable reset reason name from the ESP-IDF reset enumeration. */
static const char *get_reset_reason_name(esp_reset_reason_t reason)
{
    switch (reason)
    {
        case ESP_RST_POWERON:
            return RESET_POWER_ON;
        case ESP_RST_EXT:
            return RESET_EXTERNAL;
        case ESP_RST_SW:
            return RESET_SOFTWARE;
        case ESP_RST_PANIC:
            return RESET_PANIC;
        case ESP_RST_INT_WDT:
            return RESET_INTERRUPT_WATCHDOG;
        case ESP_RST_TASK_WDT:
            return RESET_TASK_WATCHDOG;
        case ESP_RST_WDT:
            return RESET_WATCHDOG;
        case ESP_RST_DEEPSLEEP:
            return RESET_DEEP_SLEEP;
        case ESP_RST_BROWNOUT:
            return RESET_BROWNOUT;
        default:
            return RESET_UNKNOWN;
    }
}

/* Gets immutable platform and firmware properties used by the startup banner. */
void platform_get_startup_info(platform_startup_info_t *info)
{
    esp_chip_info_t chip;
    uint32_t flash_size = 0;
    esp_chip_info(&chip);
    (void)esp_flash_get_size(NULL, &flash_size);
    const esp_app_desc_t *app = esp_app_get_description();
    *info                     = (platform_startup_info_t){
                            .firmware_name          = app->project_name,
                            .firmware_version       = app->version,
                            .processor              = CONFIG_IDF_TARGET,
                            .reset_reason           = get_reset_reason_name(esp_reset_reason()),
                            .processor_cores        = (uint32_t)chip.cores,
                            .silicon_revision_major = (uint32_t)(chip.revision / SILICON_REVISION_SCALE),
                            .silicon_revision_minor = (uint32_t)(chip.revision % SILICON_REVISION_SCALE),
                            .flash_bytes            = flash_size,
                            .external_ram_bytes     = heap_caps_get_total_size(MALLOC_CAP_SPIRAM),
    };
}

/* Gets elapsed platform time from a monotonic clock in milliseconds. */
uint64_t platform_get_monotonic_ms(void)
{
    return (uint64_t)(esp_timer_get_time() / MICROSECONDS_PER_MILLISECOND);
}

/* Gets currently available general-purpose heap memory in bytes. */
uint64_t platform_get_free_heap_bytes(void)
{
    return heap_caps_get_free_size(MALLOC_CAP_8BIT);
}

/* Gets ESP32 hardware entropy for randomized supervisor retry delays. */
uint32_t platform_get_random_u32(void)
{
    return esp_random();
}

/* Starts a platform task and reports whether task creation succeeded. */
bool platform_start_task(const char *name, platform_task_function_t function, void *context, size_t stack_size, unsigned priority)
{
    return xTaskCreate(function, name, (uint32_t)stack_size, context, (UBaseType_t)priority, NULL) == pdPASS;
}

/* Delays only the calling task for at least the requested duration. */
void platform_delay_ms(uint32_t delay_ms)
{
    vTaskDelay(pdMS_TO_TICKS(delay_ms));
}

/* Writes a diagnostic message through the ESP-IDF logging transport. */
void platform_log(platform_log_level_t level, const char *component, const char *message)
{
    esp_log_level_t esp_level = ESP_LOG_INFO;
    /* Translate portable levels here so shared services never include ESP headers. */
    if (level == PLATFORM_LOG_DEBUG)
    {
        esp_level = ESP_LOG_DEBUG;
    }
    if (level == PLATFORM_LOG_WARNING)
    {
        esp_level = ESP_LOG_WARN;
    }
    if (level == PLATFORM_LOG_ERROR)
    {
        esp_level = ESP_LOG_ERROR;
    }
    esp_log_write(esp_level, component, LOG_LINE_FORMAT, message);
}
