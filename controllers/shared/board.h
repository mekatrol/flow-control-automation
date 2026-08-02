#pragma once

#include <stddef.h>

/* Gets the stable board name used by diagnostics and configuration. */
const char *get_controller_board_name(void);

/* Formats a redacted board configuration summary into the supplied buffer. */
void controller_board_format_configuration(char *output, size_t output_size);
