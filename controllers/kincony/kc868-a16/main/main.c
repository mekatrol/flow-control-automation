#include <inttypes.h>

#include "esp_chip_info.h"
#include "esp_flash.h"
#include "esp_heap_caps.h"
#include "esp_system.h"
#include "esp_app_desc.h"

#include "controller_runtime.h"
#include "diagnostics.h"
#include "diagnostics_core.h"

void app_main(void)
{
    esp_chip_info_t chip_info;
    uint32_t flash_size = 0;

    esp_chip_info(&chip_info);

    const esp_err_t flash_result = esp_flash_get_size(NULL, &flash_size);
    const esp_app_desc_t *app = esp_app_get_description();
    char configuration[96];
    diagnostic_format_redacted_network_config(configuration, sizeof(configuration),
                                              CONFIG_KC868_A16_WIFI_SSID,
                                              CONFIG_KC868_A16_WIFI_PASSWORD);

    diagnostics_emit(DIAGNOSTIC_INFO, "startup", "banner",
                     "firmware=%s version=%s reset_reason=%d",
                     app->project_name, app->version, (int)esp_reset_reason());
    diagnostics_emit(DIAGNOSTIC_INFO, "startup", "chip",
        "model=ESP32-S3 cores=%d revision=%d.%d",
        chip_info.cores,
        chip_info.revision / 100,
        chip_info.revision % 100);
    diagnostics_emit(flash_result == ESP_OK ? DIAGNOSTIC_INFO : DIAGNOSTIC_WARNING,
                     "startup", "memory",
                     "flash_bytes=%" PRIu32 " psram_bytes=%u flash_status=%s",
                     flash_size,
                     (unsigned int)heap_caps_get_total_size(MALLOC_CAP_SPIRAM),
                     esp_err_to_name(flash_result));
    diagnostics_emit(DIAGNOSTIC_INFO, "startup", "configuration", "%s",
                     configuration);

    const esp_err_t runtime_result = controller_runtime_start();
    if (runtime_result != ESP_OK) {
        diagnostics_emit(DIAGNOSTIC_ERROR, "runtime", "start_failed", "error=%s",
                         esp_err_to_name(runtime_result));
        return;
    }
    diagnostics_emit(DIAGNOSTIC_INFO, "runtime", "started",
                     "controller task active; app_main returning");
}
