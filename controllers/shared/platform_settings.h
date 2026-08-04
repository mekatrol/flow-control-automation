#pragma once

#include <stdbool.h>

#include "board.h"
#include "settings_service.h"

/* Platform initialization results identify redacted SD and provisioning failures. */
typedef enum
{
    PLATFORM_SETTINGS_READY,
    PLATFORM_SETTINGS_DISABLED,
    PLATFORM_SETTINGS_DETECT_CONFIGURATION_FAILED,
    PLATFORM_SETTINGS_CARD_ABSENT,
    PLATFORM_SETTINGS_KEY_INVALID,
    PLATFORM_SETTINGS_SPI_INITIALIZATION_FAILED,
    PLATFORM_SETTINGS_DEVICE_INITIALIZATION_FAILED,
    PLATFORM_SETTINGS_CARD_INITIALIZATION_FAILED,
    PLATFORM_SETTINGS_CARD_INITIALIZATION_TIMEOUT,
    PLATFORM_SETTINGS_CARD_INITIALIZATION_INVALID_RESPONSE,
    PLATFORM_SETTINGS_CARD_INITIALIZATION_CRC_FAILED,
    PLATFORM_SETTINGS_CARD_INITIALIZATION_UNSUPPORTED,
    PLATFORM_SETTINGS_CARD_TOO_SMALL,
    PLATFORM_SETTINGS_MEDIA_INVALID,
} platform_settings_result_t;

/* Initializes encrypted raw SD settings storage without waiting for card insertion. */
platform_settings_result_t platform_settings_initialize(const settings_storage_config_t *config, settings_store_t *store);

/* Clears and verifies only the reserved settings slots after explicit user authorization. */
bool platform_settings_initialize_media(void);

/* Releases the active SD device and leaves chip select inactive before a software reboot. */
void platform_settings_prepare_reboot(void);

/* Gets a stable redacted reason name for platform settings initialization. */
const char *platform_settings_get_result_name(platform_settings_result_t result);
