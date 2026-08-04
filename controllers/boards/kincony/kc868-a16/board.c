#include "board.h"

#include <stdio.h>

#include "sdkconfig.h"

#ifndef CONFIG_CONTROLLER_SETTINGS_FIRST_RESERVED_SECTOR
#define CONFIG_CONTROLLER_SETTINGS_FIRST_RESERVED_SECTOR 0
#endif

/* Stable identity distinguishes this board revision in diagnostics. */
static const char BOARD_NAME[]                   = "kincony-kc868-a16-v3";
static const char DEFAULT_HOSTNAME[]             = "flow-controller";
static const char FORMAT_NETWORK_CONFIGURATION[] = "wifi=runtime_disabled ethernet=%s mqtt=%s credentials=persistent";
static const char FEATURE_ENABLED[]              = "enabled";
static const char FEATURE_DISABLED[]             = "disabled";
static const char FEATURE_PERSISTENT[]           = "persistent";
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
    SETTINGS_MOSI_GPIO        = 12,
    SETTINGS_CLOCK_GPIO       = 13,
    SETTINGS_MISO_GPIO        = 14,
    SETTINGS_CHIP_SELECT_GPIO = 11,
    SETTINGS_CARD_DETECT_GPIO = 21,
    SETTINGS_SPI_CLOCK_HZ     = 10000000,
};

/* Gets the stable board name used by diagnostics and configuration. */
const char *get_controller_board_name(void)
{
    return BOARD_NAME;
}

/* Gets the safe fallback hostname used until the user persists a device-specific value. */
const char *get_controller_default_hostname(void)
{
    return DEFAULT_HOSTNAME;
}

/* Formats a credential-free board configuration summary into the supplied buffer. */
void controller_board_format_configuration(char *output, size_t output_size)
{
    (void)snprintf(output, output_size, FORMAT_NETWORK_CONFIGURATION,
                   CONFIG_CONTROLLER_ETHERNET_ENABLED ? FEATURE_ENABLED : FEATURE_DISABLED, FEATURE_PERSISTENT);
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
        .hostname         = DEFAULT_HOSTNAME,
    };
}

/* Gets the board-described raw SD storage wiring. */
void controller_board_get_settings_storage_config(settings_storage_config_t *config)
{
    *config = (settings_storage_config_t){.mosi_gpio             = SETTINGS_MOSI_GPIO,
                                          .clock_gpio            = SETTINGS_CLOCK_GPIO,
                                          .miso_gpio             = SETTINGS_MISO_GPIO,
                                          .chip_select_gpio      = SETTINGS_CHIP_SELECT_GPIO,
                                          .card_detect_gpio      = SETTINGS_CARD_DETECT_GPIO,
                                          .spi_clock_hz          = SETTINGS_SPI_CLOCK_HZ,
                                          .first_reserved_sector = CONFIG_CONTROLLER_SETTINGS_FIRST_RESERVED_SECTOR};
}

/* Gets blank first-initialization defaults because all user credentials are provisioned through the terminal. */
void controller_board_get_settings_defaults(settings_defaults_t *defaults)
{
    *defaults                 = (settings_defaults_t){0};
    defaults->hostname.is_set = true;
    (void)snprintf(defaults->hostname.value, sizeof(defaults->hostname.value), "%s", DEFAULT_HOSTNAME);
}
