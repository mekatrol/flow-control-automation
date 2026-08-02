#include "diagnostics.h"

#include <stdarg.h>
#include <stdio.h>

#include "esp_log.h"
#include "esp_timer.h"

#define DIAGNOSTIC_MESSAGE_SIZE 192
#define DIAGNOSTIC_EVENT_SIZE 320

static void emit_message(diagnostic_severity_t severity, const char *component,
                         const char *event_code, const char *message)
{
    char event[DIAGNOSTIC_EVENT_SIZE];
    diagnostic_format_event(event, sizeof(event), severity, component, event_code,
                            (uint64_t)(esp_timer_get_time() / 1000), message);
    esp_log_level_t level = ESP_LOG_INFO;
    if (severity == DIAGNOSTIC_DEBUG) level = ESP_LOG_DEBUG;
    if (severity == DIAGNOSTIC_WARNING) level = ESP_LOG_WARN;
    if (severity == DIAGNOSTIC_ERROR) level = ESP_LOG_ERROR;
    esp_log_write(level, component, "%s\n", event);
}

static void format_and_emit(diagnostic_severity_t severity, const char *component,
                            const char *event_code, const char *format, va_list args)
{
    char message[DIAGNOSTIC_MESSAGE_SIZE];
    vsnprintf(message, sizeof(message), format, args);
    emit_message(severity, component, event_code, message);
}

void diagnostics_emit(diagnostic_severity_t severity, const char *component,
                      const char *event_code, const char *format, ...)
{
    va_list args;
    va_start(args, format);
    format_and_emit(severity, component, event_code, format, args);
    va_end(args);
}

void diagnostics_emit_limited(diagnostic_rate_limiter_t *limiter,
                              uint32_t window_ms, uint32_t maximum_events,
                              diagnostic_severity_t severity,
                              const char *component, const char *event_code,
                              const char *format, ...)
{
    const uint64_t now_ms = (uint64_t)(esp_timer_get_time() / 1000);
    uint32_t suppressed = 0;
    if (!diagnostic_rate_limit(limiter, now_ms, window_ms, maximum_events,
                               &suppressed)) {
        return;
    }
    if (suppressed > 0) {
        diagnostics_emit(DIAGNOSTIC_WARNING, component, "messages_suppressed",
                         "suppressed=%u previous_event=%s", suppressed, event_code);
    }
    va_list args;
    va_start(args, format);
    format_and_emit(severity, component, event_code, format, args);
    va_end(args);
}
