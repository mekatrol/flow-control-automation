#include "board.h"

#include "diagnostics_core.h"
#include "sdkconfig.h"

const char *controller_board_name(void)
{
    return "kincony-kc868-a16-v3";
}

void controller_board_format_configuration(char *output, size_t output_size)
{
    (void)diagnostic_format_redacted_network_config(
        output, output_size, CONFIG_CONTROLLER_WIFI_SSID,
        CONFIG_CONTROLLER_WIFI_PASSWORD);
}
