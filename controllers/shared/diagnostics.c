#include "diagnostics.h"

#include <stdarg.h>
#include <stdio.h>

#include "platform.h"

#define DIAGNOSTIC_MESSAGE_SIZE 192
#define DIAGNOSTIC_EVENT_SIZE 320

static platform_log_level_t platform_level(diagnostic_severity_t severity)
{
    switch (severity) {
    case DIAGNOSTIC_DEBUG: return PLATFORM_LOG_DEBUG;
    case DIAGNOSTIC_WARNING: return PLATFORM_LOG_WARNING;
    case DIAGNOSTIC_ERROR: return PLATFORM_LOG_ERROR;
    default: return PLATFORM_LOG_INFO;
    }
}

static void emit_message(diagnostic_severity_t severity, const char *component,
                         const char *event_code, const char *message)
{
    char event[DIAGNOSTIC_EVENT_SIZE];
    diagnostic_format_event(event, sizeof(event), severity, component, event_code,
                            platform_monotonic_ms(), message);
    platform_log(platform_level(severity), component, event);
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
    uint32_t suppressed = 0;
    if (!diagnostic_rate_limit(limiter, platform_monotonic_ms(), window_ms,
                               maximum_events, &suppressed)) {
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
