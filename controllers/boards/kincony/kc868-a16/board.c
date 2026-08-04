#include "board.h"

#include <stdio.h>

#include "sdkconfig.h"

/* Stable identity distinguishes this board revision in diagnostics. */
static const char BOARD_NAME[] = "kincony-kc868-a16-v3";
static const char FORMAT_NETWORK_CONFIGURATION[] =
    "wifi=runtime_disabled wifi_credentials=%s ethernet=%s mqtt=%s mqtt_credentials=%s";
static const char CREDENTIALS_CONFIGURED[]     = "<redacted>";
static const char CREDENTIALS_NOT_CONFIGURED[] = "not_configured";
static const char FEATURE_ENABLED[]            = "enabled";
static const char FEATURE_DISABLED[]           = "disabled";

/* KC868-A16v3 W5500 wiring is fixed by the board schematic. */
enum
{
    ETHERNET_CLOCK_GPIO       = 42,
    ETHERNET_MOSI_GPIO        = 43,
    ETHERNET_MISO_GPIO        = 44,
    ETHERNET_CHIP_SELECT_GPIO = 15,
    ETHERNET_INTERRUPT_GPIO   = 2,
    ETHERNET_RESET_GPIO       = 1,
    ETHERNET_SPI_CLOCK_HZ     = 20000000,
};

/* Gets the stable board name used by diagnostics and configuration. */
const char *get_controller_board_name(void)
{
    return BOARD_NAME;
}

/* Formats a redacted board configuration summary into the supplied buffer. */
void controller_board_format_configuration(char *output, size_t output_size)
{
    const bool is_wifi_configured = CONFIG_CONTROLLER_WIFI_SSID[0] != '\0' && CONFIG_CONTROLLER_WIFI_PASSWORD[0] != '\0';
    const bool is_mqtt_configured = CONFIG_CONTROLLER_MQTT_HOST[0] != '\0' && CONFIG_CONTROLLER_MQTT_CLIENT_ID[0] != '\0';
    const bool is_mqtt_credentials_configured =
        CONFIG_CONTROLLER_MQTT_USERNAME[0] != '\0' || CONFIG_CONTROLLER_MQTT_PASSWORD[0] != '\0';
    /* Report runtime selection while keeping saved credentials out of logs. */
    (void)snprintf(output, output_size, FORMAT_NETWORK_CONFIGURATION,
                   is_wifi_configured ? CREDENTIALS_CONFIGURED : CREDENTIALS_NOT_CONFIGURED,
                   CONFIG_CONTROLLER_ETHERNET_ENABLED ? FEATURE_ENABLED : FEATURE_DISABLED,
                   is_mqtt_configured ? FEATURE_ENABLED : FEATURE_DISABLED,
                   is_mqtt_credentials_configured ? CREDENTIALS_CONFIGURED : CREDENTIALS_NOT_CONFIGURED);
}

/* Gets the board-described W5500 wiring and Ethernet configuration. */
void controller_board_get_ethernet_config(ethernet_link_config_t *config)
{
    *config = (ethernet_link_config_t){
        .enabled          = CONFIG_CONTROLLER_ETHERNET_ENABLED,
        .clock_gpio       = ETHERNET_CLOCK_GPIO,
        .mosi_gpio        = ETHERNET_MOSI_GPIO,
        .miso_gpio        = ETHERNET_MISO_GPIO,
        .chip_select_gpio = ETHERNET_CHIP_SELECT_GPIO,
        .interrupt_gpio   = ETHERNET_INTERRUPT_GPIO,
        .reset_gpio       = ETHERNET_RESET_GPIO,
        .spi_clock_hz     = ETHERNET_SPI_CLOCK_HZ,
        .hostname         = CONFIG_CONTROLLER_ETHERNET_HOSTNAME,
    };
}
