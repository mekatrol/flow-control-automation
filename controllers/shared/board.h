#pragma once

#include <stddef.h>

#include "ethernet_link.h"
#include "settings_service.h"

typedef struct
{
    int mosi_gpio;
    int clock_gpio;
    int miso_gpio;
    int chip_select_gpio;
    int card_detect_gpio;
    uint32_t spi_clock_hz;
    uint32_t first_reserved_sector;
} settings_storage_config_t;

/* Gets the stable board name used by diagnostics and configuration. */
const char *get_controller_board_name(void);

/* Formats a redacted board configuration summary into the supplied buffer. */
void controller_board_format_configuration(char *output, size_t output_size);

/* Gets the board-described W5500 wiring and Ethernet configuration. */
void controller_board_get_ethernet_config(ethernet_link_config_t *config);

/* Gets the board-described raw SD storage wiring. */
void controller_board_get_settings_storage_config(settings_storage_config_t *config);

/* Gets typed first-initialization defaults while retaining null versus empty. */
void controller_board_get_settings_defaults(settings_defaults_t *defaults);
