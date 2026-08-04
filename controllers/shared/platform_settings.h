#pragma once

#include <stdbool.h>

#include "board.h"
#include "settings_service.h"

/* Initializes encrypted raw SD settings storage without waiting for card insertion. */
bool platform_settings_initialize(const settings_storage_config_t *config, settings_store_t *store);
