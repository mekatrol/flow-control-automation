#include <assert.h>
#include <stdio.h>
#include <string.h>

#include "wifi/link.h"

/* Valid fixtures exercise enabled, intentionally disabled, and open stations. */
static const char VALID_SSID[]           = "test-network";
static const char VALID_PASSWORD[]       = "test-password";
static const char EMPTY_VALUE[]          = "";
static const char VALID_HOSTNAME[]       = "test-controller";
static const char TEST_SUCCESS_MESSAGE[] = "Wi-Fi link core tests passed";

/* Fills a string beyond the supplied configuration limit for validation tests. */
static void fill_oversized_value(char *value, size_t size)
{
    /* Repeated safe characters isolate length validation from content concerns. */
    (void)memset(value, 'x', size - 1U);
    value[size - 1U] = '\0';
}

/* Verifies valid, disabled, open, oversized, and incomplete configurations. */
static void test_configuration_validation(void)
{
    wifi_link_config_t config = {
        .ssid       = VALID_SSID,
        .password   = VALID_PASSWORD,
        .hostname   = VALID_HOSTNAME,
        .power_save = WIFI_POWER_SAVE_MINIMUM_MODEM,
    };
    assert(is_wifi_link_config_valid(&config));
    assert(is_wifi_link_config_enabled(&config));
    config.ssid = EMPTY_VALUE;
    assert(is_wifi_link_config_valid(&config));
    assert(!is_wifi_link_config_enabled(&config));
    config.ssid     = VALID_SSID;
    config.password = EMPTY_VALUE;
    assert(is_wifi_link_config_valid(&config));

    char oversized_ssid[WIFI_SSID_MAX_LENGTH + 2U];
    fill_oversized_value(oversized_ssid, sizeof(oversized_ssid));
    config.ssid = oversized_ssid;
    assert(!is_wifi_link_config_valid(&config));
    config.ssid     = VALID_SSID;
    config.hostname = NULL;
    assert(!is_wifi_link_config_valid(&config));
}

/* Verifies each platform event maps to the intended neutral supervisor event. */
static void test_platform_event_mapping(void)
{
    assert(wifi_link_get_network_event_type(WIFI_PLATFORM_EVENT_DRIVER_STARTED) == NETWORK_EVENT_STARTED);
    assert(wifi_link_get_network_event_type(WIFI_PLATFORM_EVENT_ASSOCIATING) == NETWORK_EVENT_CONNECTING);
    assert(wifi_link_get_network_event_type(WIFI_PLATFORM_EVENT_ASSOCIATED) == NETWORK_EVENT_CONNECTING);
    assert(wifi_link_get_network_event_type(WIFI_PLATFORM_EVENT_ADDRESS_READY) == NETWORK_EVENT_ONLINE);
    assert(wifi_link_get_network_event_type(WIFI_PLATFORM_EVENT_ADDRESS_LOST) == NETWORK_EVENT_CONNECTION_LOST);
    assert(wifi_link_get_network_event_type(WIFI_PLATFORM_EVENT_AUTHENTICATION_FAILED) == NETWORK_EVENT_FAILED);
    assert(wifi_link_get_network_event_type(WIFI_PLATFORM_EVENT_ASSOCIATION_FAILED) == NETWORK_EVENT_FAILED);
    assert(wifi_link_get_network_event_type(WIFI_PLATFORM_EVENT_DRIVER_FAILED) == NETWORK_EVENT_FAILED);
    assert(wifi_link_get_network_event_type(WIFI_PLATFORM_EVENT_STOPPED) == NETWORK_EVENT_STOPPED);
}

/* Runs all Wi-Fi core cases and returns success when assertions hold. */
int main(void)
{
    test_configuration_validation();
    test_platform_event_mapping();
    puts(TEST_SUCCESS_MESSAGE);
    return 0;
}
