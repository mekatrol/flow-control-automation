#pragma once

#include <stddef.h>

#include "ethernet_link.h"

/* Gets the stable board name used by diagnostics and configuration. */
const char *get_controller_board_name(void);

/* Formats a redacted board configuration summary into the supplied buffer. */
void controller_board_format_configuration(char *output, size_t output_size);

/* Gets the board-described W5500 wiring and Ethernet configuration. */
void controller_board_get_ethernet_config(ethernet_link_config_t *config);
