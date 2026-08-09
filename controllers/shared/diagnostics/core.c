#include "diagnostics/core.h"

#include <inttypes.h>
#include <stdio.h>

/* Stable schema values used by diagnostic serialization and consumers. */
static const char SEVERITY_DEBUG[]        = "debug";
static const char SEVERITY_INFO[]         = "info";
static const char SEVERITY_WARNING[]      = "warning";
static const char SEVERITY_ERROR[]        = "error";
static const char VALUE_UNKNOWN[]         = "unknown";
static const char VALUE_ENABLED[]         = "enabled";
static const char VALUE_DISABLED[]        = "disabled";
static const char VALUE_REDACTED[]        = "<redacted>";
static const char VALUE_NOT_CONFIGURED[]  = "not-configured";
static const char EVENT_FORMAT[]          = "diag timestamp_ms=%" PRIu64 " severity=%s component=%s event=%s message=\"%s\"";
static const char NETWORK_CONFIG_FORMAT[] = "wifi=%s wifi_credentials=%s";

/* Maximum sanitized message storage, including its terminator. */
enum
{
    SAFE_MESSAGE_SIZE = 192
};

/* Gets the stable diagnostic name associated with a severity value. */
const char *get_diagnostic_severity_name(diagnostic_severity_t severity)
{
    switch (severity)
    {
        case DIAGNOSTIC_DEBUG:
            return SEVERITY_DEBUG;
        case DIAGNOSTIC_INFO:
            return SEVERITY_INFO;
        case DIAGNOSTIC_WARNING:
            return SEVERITY_WARNING;
        case DIAGNOSTIC_ERROR:
            return SEVERITY_ERROR;
        default:
            return VALUE_UNKNOWN;
    }
}

/* Formats and sanitizes one diagnostic event into a bounded output buffer. */
int diagnostic_format_event(char *output, size_t output_size, diagnostic_severity_t severity, const char *component,
                            const char *event_code, uint64_t timestamp_ms, const char *message)
{
    if (output == NULL || output_size == 0 || component == NULL || event_code == NULL || message == NULL)
    {
        return -1;
    }
    char safe_message[SAFE_MESSAGE_SIZE];
    size_t target = 0;

    /* Replace line-breaking characters so every event remains one parseable log line. */
    for (size_t source = 0; message[source] != '\0' && target + 1 < sizeof(safe_message); ++source)
    {
        const char character   = message[source];
        safe_message[target++] = (character == '\n' || character == '\r' || character == '"') ? ' ' : character;
    }
    safe_message[target] = '\0';
    return snprintf(output, output_size, EVENT_FORMAT, timestamp_ms, get_diagnostic_severity_name(severity), component,
                    event_code, safe_message);
}

/* Formats Wi-Fi configuration presence without exposing credential values. */
int diagnostic_format_redacted_network_config(char *output, size_t output_size, const char *wifi_ssid, const char *wifi_password)
{
    if (output == NULL || output_size == 0)
    {
        return -1;
    }
    const bool wifi_enabled          = wifi_ssid != NULL && wifi_ssid[0] != '\0';
    const bool credential_configured = wifi_password != NULL && wifi_password[0] != '\0';
    return snprintf(output, output_size, NETWORK_CONFIG_FORMAT, wifi_enabled ? VALUE_ENABLED : VALUE_DISABLED,
                    credential_configured ? VALUE_REDACTED : VALUE_NOT_CONFIGURED);
}

/* Tests whether an event is allowed and advances the bounded limiter state. */
bool is_diagnostic_event_allowed(diagnostic_rate_limiter_t *limiter, uint64_t now_ms, uint32_t window_ms, uint32_t maximum_events,
                                 uint32_t *previously_suppressed)
{
    if (previously_suppressed != NULL)
    {
        *previously_suppressed = 0;
    }

    if (limiter == NULL || window_ms == 0 || maximum_events == 0)
    {
        return false;
    }

    /* Clock rollback or elapsed duration both begin a fresh bounded window. */
    if (!limiter->initialized || now_ms < limiter->window_started_ms || now_ms - limiter->window_started_ms >= window_ms)
    {
        if (previously_suppressed != NULL && limiter->initialized)
        {
            *previously_suppressed = limiter->suppressed;
        }
        limiter->window_started_ms = now_ms;
        limiter->emitted           = 0;
        limiter->suppressed        = 0;
        limiter->initialized       = true;
    }

    /* Permit only the configured count and retain the rest as one summary count. */
    if (limiter->emitted < maximum_events)
    {
        ++limiter->emitted;
        return true;
    }
    ++limiter->suppressed;
    return false;
}
