#include <assert.h>
#include <stdio.h>
#include <string.h>

#include "diagnostics/core.h"

/* Fixture sizes provide bounded output storage for each formatter contract. */
enum
{
    EVENT_OUTPUT_SIZE           = 256,
    CONFIG_OUTPUT_SIZE          = 96,
    EVENT_TIMESTAMP_MS          = 1234,
    RATE_WINDOW_STARTED_MS      = 100,
    RATE_WINDOW_MS              = 1000,
    RATE_MAXIMUM_EVENTS         = 2,
    RATE_SECOND_EVENT_MS        = 101,
    RATE_FIRST_REJECTED_MS      = 102,
    RATE_SECOND_REJECTED_MS     = 103,
    RATE_NEXT_WINDOW_MS         = 1100,
    INITIAL_SUPPRESSED_SENTINEL = 99,
    EXPECTED_SUPPRESSED_EVENTS  = 2,
};

/* Diagnostic fixtures exercise sanitization, redaction, and stable schemas. */
static const char COMPONENT_WIFI[]           = "wifi";
static const char EVENT_ASSOCIATION_FAILED[] = "association_failed";
static const char UNSAFE_MESSAGE[]           = "retry\nrequested \"soon\"";
static const char EXPECTED_EVENT[]           = "diag timestamp_ms=1234 severity=warning component=wifi "
                                               "event=association_failed message=\"retry requested  soon \"";
static const char PRIVATE_SSID[]             = "private-network";
static const char PRIVATE_PASSWORD[]         = "super-secret";
static const char ENABLED_FIELD[]            = "wifi=enabled";
static const char REDACTED_FIELD[]           = "wifi_credentials=<redacted>";
static const char TEST_SUCCESS_MESSAGE[]     = "Diagnostics core tests passed";

/* Verifies diagnostic output sanitizes control characters into one stable line. */
static void test_event_formatting(void)
{
    char output[EVENT_OUTPUT_SIZE];
    assert(diagnostic_format_event(output, sizeof(output), DIAGNOSTIC_WARNING, COMPONENT_WIFI, EVENT_ASSOCIATION_FAILED,
                                   EVENT_TIMESTAMP_MS, UNSAFE_MESSAGE) > 0);
    assert(strcmp(output, EXPECTED_EVENT) == 0);
}

/* Verifies network credentials never appear in formatted configuration output. */
static void test_redaction(void)
{
    char output[CONFIG_OUTPUT_SIZE];
    const char *const ssid     = PRIVATE_SSID;
    const char *const password = PRIVATE_PASSWORD;
    assert(diagnostic_format_redacted_network_config(output, sizeof(output), ssid, password) > 0);
    assert(strstr(output, ENABLED_FIELD) != NULL);
    assert(strstr(output, REDACTED_FIELD) != NULL);
    assert(strstr(output, ssid) == NULL);
    assert(strstr(output, password) == NULL);
}

/* Verifies bounded emission and suppressed-count reporting across windows. */
static void test_rate_limiting(void)
{
    diagnostic_rate_limiter_t limiter = {0};
    uint32_t suppressed               = INITIAL_SUPPRESSED_SENTINEL;
    assert(is_diagnostic_event_allowed(&limiter, RATE_WINDOW_STARTED_MS, RATE_WINDOW_MS, RATE_MAXIMUM_EVENTS, &suppressed));
    assert(suppressed == 0);
    assert(is_diagnostic_event_allowed(&limiter, RATE_SECOND_EVENT_MS, RATE_WINDOW_MS, RATE_MAXIMUM_EVENTS, &suppressed));
    assert(!is_diagnostic_event_allowed(&limiter, RATE_FIRST_REJECTED_MS, RATE_WINDOW_MS, RATE_MAXIMUM_EVENTS, &suppressed));
    assert(!is_diagnostic_event_allowed(&limiter, RATE_SECOND_REJECTED_MS, RATE_WINDOW_MS, RATE_MAXIMUM_EVENTS, &suppressed));
    assert(is_diagnostic_event_allowed(&limiter, RATE_NEXT_WINDOW_MS, RATE_WINDOW_MS, RATE_MAXIMUM_EVENTS, &suppressed));
    assert(suppressed == EXPECTED_SUPPRESSED_EVENTS);
}

/* Runs all diagnostics cases and returns success when assertions hold. */
int main(void)
{
    test_event_formatting();
    test_redaction();
    test_rate_limiting();
    puts(TEST_SUCCESS_MESSAGE);
    return 0;
}
