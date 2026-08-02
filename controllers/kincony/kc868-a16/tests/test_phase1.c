#include <assert.h>
#include <stdio.h>
#include <string.h>

#include "controller_health.h"
#include "diagnostics_core.h"

static void test_event_formatting(void)
{
    char output[256];
    assert(diagnostic_format_event(output, sizeof(output), DIAGNOSTIC_WARNING,
                                   "wifi", "association_failed", 1234,
                                   "retry\nrequested \"soon\"") > 0);
    assert(strcmp(output,
                  "diag timestamp_ms=1234 severity=warning component=wifi "
                  "event=association_failed message=\"retry requested  soon \"") == 0);
}

static void test_redaction(void)
{
    char output[96];
    const char *const ssid = "private-network";
    const char *const password = "super-secret";
    assert(diagnostic_format_redacted_network_config(output, sizeof(output),
                                                     ssid, password) > 0);
    assert(strstr(output, "wifi=enabled") != NULL);
    assert(strstr(output, "wifi_credentials=<redacted>") != NULL);
    assert(strstr(output, ssid) == NULL);
    assert(strstr(output, password) == NULL);

    assert(diagnostic_format_redacted_network_config(output, sizeof(output), "", "") > 0);
    assert(strcmp(output, "wifi=disabled wifi_credentials=not-configured") == 0);
}

static void test_rate_limiting(void)
{
    diagnostic_rate_limiter_t limiter = {0};
    uint32_t suppressed = 99;
    assert(diagnostic_rate_limit(&limiter, 100, 1000, 2, &suppressed));
    assert(suppressed == 0);
    assert(diagnostic_rate_limit(&limiter, 101, 1000, 2, &suppressed));
    assert(!diagnostic_rate_limit(&limiter, 102, 1000, 2, &suppressed));
    assert(!diagnostic_rate_limit(&limiter, 103, 1000, 2, &suppressed));
    assert(diagnostic_rate_limit(&limiter, 1100, 1000, 2, &suppressed));
    assert(suppressed == 2);
    assert(diagnostic_rate_limit(&limiter, 10, 1000, 2, &suppressed));
}

static void test_health_formatting(void)
{
    const controller_health_snapshot_t snapshot = {
        .uptime_ms = 9876,
        .free_heap_bytes = 123456,
        .wifi_state = "disabled",
        .ethernet_state = "online",
        .mqtt_state = "backoff",
        .rs485_state = "stopped",
        .rs485_errors = 4,
        .rs485_queue_drops = 2,
    };
    char output[256];
    assert(controller_health_format(output, sizeof(output), &snapshot) > 0);
    assert(strcmp(output,
                  "status uptime_ms=9876 free_heap_bytes=123456 wifi=disabled "
                  "ethernet=online mqtt=backoff rs485=stopped rs485_errors=4 "
                  "rs485_queue_drops=2") == 0);
}

int main(void)
{
    test_event_formatting();
    test_redaction();
    test_rate_limiting();
    test_health_formatting();
    puts("Phase 1 host tests passed");
    return 0;
}
