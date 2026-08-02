#include <inttypes.h>

#include "esp_chip_info.h"
#include "esp_flash.h"
#include "esp_heap_caps.h"
#include "esp_log.h"
#include "freertos/FreeRTOS.h"
#include "freertos/task.h"

static const char *const TAG = "kc868_a16";

void app_main(void)
{
    esp_chip_info_t chip_info;
    uint32_t flash_size = 0;

    esp_chip_info(&chip_info);

    ESP_ERROR_CHECK(esp_flash_get_size(NULL, &flash_size));

    ESP_LOGI(TAG, "Flow Control Automation board bring-up");
    ESP_LOGI(
        TAG,
        "ESP32-S3 with %d CPU cores, silicon revision %d.%d",
        chip_info.cores,
        chip_info.revision / 100,
        chip_info.revision % 100);
    ESP_LOGI(TAG, "Flash: %" PRIu32 " MiB", flash_size / (1024U * 1024U));
    ESP_LOGI(
        TAG,
        "PSRAM: %u bytes",
        (unsigned int)heap_caps_get_total_size(MALLOC_CAP_SPIRAM));

    for (uint32_t heartbeat = 1;; ++heartbeat) {
        ESP_LOGI(TAG, "Heartbeat %" PRIu32, heartbeat);
        vTaskDelay(pdMS_TO_TICKS(1000));
    }
}
