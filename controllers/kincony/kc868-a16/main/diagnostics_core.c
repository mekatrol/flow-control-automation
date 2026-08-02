#include "diagnostics_core.h"

#include <inttypes.h>
#include <stdio.h>

const char *diagnostic_severity_name(diagnostic_severity_t severity)
{
    switch (severity) {
    case DIAGNOSTIC_DEBUG: return "debug";
    case DIAGNOSTIC_INFO: return "info";
    case DIAGNOSTIC_WARNING: return "warning";
    case DIAGNOSTIC_ERROR: return "error";
    default: return "unknown";
    }
}

int diagnostic_format_event(char *output, size_t output_size,
                            diagnostic_severity_t severity,
                            const char *component, const char *event_code,
                            uint64_t timestamp_ms, const char *message)
{
    if (output == NULL || output_size == 0 || component == NULL ||
        event_code == NULL || message == NULL) {
        return -1;
    }
    char safe_message[192];
    size_t target = 0;
    for (size_t source = 0; message[source] != '\0' &&
                            target + 1 < sizeof(safe_message); ++source) {
        const char character = message[source];
        safe_message[target++] =
            (character == '\n' || character == '\r' || character == '"') ? ' ' : character;
    }
    safe_message[target] = '\0';
    return snprintf(output, output_size,
                    "diag timestamp_ms=%" PRIu64 " severity=%s component=%s event=%s message=\"%s\"",
                    timestamp_ms, diagnostic_severity_name(severity), component,
                    event_code, safe_message);
}

int diagnostic_format_redacted_network_config(char *output, size_t output_size,
                                              const char *wifi_ssid,
                                              const char *wifi_password)
{
    if (output == NULL || output_size == 0) {
        return -1;
    }
    const bool wifi_enabled = wifi_ssid != NULL && wifi_ssid[0] != '\0';
    const bool credential_configured = wifi_password != NULL && wifi_password[0] != '\0';
    return snprintf(output, output_size, "wifi=%s wifi_credentials=%s",
                    wifi_enabled ? "enabled" : "disabled",
                    credential_configured ? "<redacted>" : "not-configured");
}

bool diagnostic_rate_limit(diagnostic_rate_limiter_t *limiter, uint64_t now_ms,
                           uint32_t window_ms, uint32_t maximum_events,
                           uint32_t *previously_suppressed)
{
    if (previously_suppressed != NULL) {
        *previously_suppressed = 0;
    }
    if (limiter == NULL || window_ms == 0 || maximum_events == 0) {
        return false;
    }
    if (!limiter->initialized || now_ms < limiter->window_started_ms ||
        now_ms - limiter->window_started_ms >= window_ms) {
        if (previously_suppressed != NULL && limiter->initialized) {
            *previously_suppressed = limiter->suppressed;
        }
        limiter->window_started_ms = now_ms;
        limiter->emitted = 0;
        limiter->suppressed = 0;
        limiter->initialized = true;
    }
    if (limiter->emitted < maximum_events) {
        ++limiter->emitted;
        return true;
    }
    ++limiter->suppressed;
    return false;
}
