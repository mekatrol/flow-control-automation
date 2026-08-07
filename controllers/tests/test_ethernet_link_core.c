#include <assert.h>
#include <stdio.h>

#include "ethernet/link.h"

/* Named test data documents the smallest valid board-provided configuration. */
enum
{
    TEST_CLOCK_GPIO       = 42,
    TEST_MOSI_GPIO        = 43,
    TEST_MISO_GPIO        = 44,
    TEST_CHIP_SELECT_GPIO = 15,
    TEST_INTERRUPT_GPIO   = 2,
    TEST_RESET_GPIO       = 1,
    TEST_SPI_CLOCK_HZ     = 20000000,
};

static const char TEST_HOSTNAME[]        = "controller-test";
static const char TEST_SUCCESS_MESSAGE[] = "ethernet_link_core tests passed";

/* Gets a complete valid Ethernet configuration for isolated validation tests. */
static ethernet_link_config_t get_valid_config(void)
{
    return (ethernet_link_config_t){
        .enabled          = true,
        .clock_gpio       = TEST_CLOCK_GPIO,
        .mosi_gpio        = TEST_MOSI_GPIO,
        .miso_gpio        = TEST_MISO_GPIO,
        .chip_select_gpio = TEST_CHIP_SELECT_GPIO,
        .interrupt_gpio   = TEST_INTERRUPT_GPIO,
        .reset_gpio       = TEST_RESET_GPIO,
        .spi_clock_hz     = TEST_SPI_CLOCK_HZ,
        .hostname         = TEST_HOSTNAME,
    };
}

/* Verifies board configuration validation rejects missing required values. */
static void test_configuration_validation(void)
{
    ethernet_link_config_t config = get_valid_config();
    assert(is_ethernet_link_config_valid(&config));
    config.hostname = NULL;
    assert(!is_ethernet_link_config_valid(&config));
    config                = get_valid_config();
    config.interrupt_gpio = -1;
    assert(!is_ethernet_link_config_valid(&config));
    config              = get_valid_config();
    config.spi_clock_hz = 0;
    assert(!is_ethernet_link_config_valid(&config));
}

/* Verifies every W5500 event maps into the transport-neutral contract. */
static void test_event_mapping(void)
{
    assert(ethernet_link_get_network_event_type(ETHERNET_PLATFORM_EVENT_DRIVER_STARTED) == NETWORK_EVENT_STARTED);
    assert(ethernet_link_get_network_event_type(ETHERNET_PLATFORM_EVENT_LINK_UP) == NETWORK_EVENT_CONNECTING);
    assert(ethernet_link_get_network_event_type(ETHERNET_PLATFORM_EVENT_ADDRESS_READY) == NETWORK_EVENT_ONLINE);
    assert(ethernet_link_get_network_event_type(ETHERNET_PLATFORM_EVENT_ADDRESS_LOST) == NETWORK_EVENT_CONNECTION_LOST);
    assert(ethernet_link_get_network_event_type(ETHERNET_PLATFORM_EVENT_LINK_DOWN) == NETWORK_EVENT_CONNECTION_LOST);
    assert(ethernet_link_get_network_event_type(ETHERNET_PLATFORM_EVENT_DRIVER_FAILED) == NETWORK_EVENT_FAILED);
    assert(ethernet_link_get_network_event_type(ETHERNET_PLATFORM_EVENT_STOPPED) == NETWORK_EVENT_STOPPED);
}

/* Runs the Ethernet unit checks and returns success when all assertions hold. */
int main(void)
{
    test_configuration_validation();
    test_event_mapping();
    (void)puts(TEST_SUCCESS_MESSAGE);
    return 0;
}
