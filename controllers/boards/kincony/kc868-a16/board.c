#include "board.h"

#include "diagnostics_core.h"
#include "sdkconfig.h"

/* Stable identity distinguishes this board revision in diagnostics. */
static const char BOARD_NAME[] = "kincony-kc868-a16-v3";

/* Gets the stable board name used by diagnostics and configuration. */
const char *get_controller_board_name(void)
{
    return BOARD_NAME;
}

/* Formats a redacted board configuration summary into the supplied buffer. */
void controller_board_format_configuration(char *output, size_t output_size)
{
    (void)diagnostic_format_redacted_network_config(
        output, output_size, CONFIG_CONTROLLER_WIFI_SSID,
        CONFIG_CONTROLLER_WIFI_PASSWORD);
}
