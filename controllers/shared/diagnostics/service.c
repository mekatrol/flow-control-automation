#include "diagnostics/service.h"

#include <stdarg.h>
#include <stdio.h>

#include "platform/core.h"

#define DIAGNOSTIC_MESSAGE_SIZE 192
#define DIAGNOSTIC_EVENT_SIZE 320

/* Identifies limiter summaries without coupling callers to the diagnostic schema. */
static const char EVENT_MESSAGES_SUPPRESSED[] = "messages_suppressed";
/* Describes the limiter summary fields in the structured diagnostic payload. */
static const char FORMAT_MESSAGES_SUPPRESSED[] = "suppressed=%u previous_event=%s";
static diagnostics_sink_function_t live_sink;
static void *live_sink_context;

/* Selects an optional bounded live stream sink while retaining platform logging. */
void diagnostics_set_sink(diagnostics_sink_function_t sink, void *context)
{
    live_sink         = sink;
    live_sink_context = context;
}

/* Gets the platform log level corresponding to a portable severity. */
static platform_log_level_t get_platform_level(diagnostic_severity_t severity)
{
    switch (severity)
    {
        case DIAGNOSTIC_DEBUG:
            return PLATFORM_LOG_DEBUG;
        case DIAGNOSTIC_WARNING:
            return PLATFORM_LOG_WARNING;
        case DIAGNOSTIC_ERROR:
            return PLATFORM_LOG_ERROR;
        default:
            return PLATFORM_LOG_INFO;
    }
}

/* Formats and writes one already-expanded diagnostic message. */
static void emit_message(diagnostic_severity_t severity, const char *component, const char *event_code, const char *message)
{
    char event[DIAGNOSTIC_EVENT_SIZE];
    diagnostic_format_event(event, sizeof(event), severity, component, event_code, platform_get_monotonic_ms(), message);
    platform_log(get_platform_level(severity), component, event);

    if (live_sink != NULL)
    {
        live_sink(live_sink_context, event);
    }
}

/* Expands variadic arguments into bounded storage before emitting the event. */
static void format_and_emit(diagnostic_severity_t severity, const char *component, const char *event_code, const char *format,
                            va_list args)
{
    char message[DIAGNOSTIC_MESSAGE_SIZE];
    vsnprintf(message, sizeof(message), format, args);
    emit_message(severity, component, event_code, message);
}

/* Emits one structured diagnostic event through the platform logger. */
void diagnostics_emit(diagnostic_severity_t severity, const char *component, const char *event_code, const char *format, ...)
{
    va_list args;
    va_start(args, format);
    format_and_emit(severity, component, event_code, format, args);
    va_end(args);
}

/* Emits an event only when its bounded rate limiter permits another message. */
void diagnostics_emit_limited(diagnostic_rate_limiter_t *limiter, uint32_t window_ms, uint32_t maximum_events,
                              diagnostic_severity_t severity, const char *component, const char *event_code, const char *format,
                              ...)
{
    uint32_t suppressed = 0;

    /* Rate limiting preserves logging capacity during repeated subsystem failures. */
    if (!is_diagnostic_event_allowed(limiter, platform_get_monotonic_ms(), window_ms, maximum_events, &suppressed))
    {
        return;
    }

    if (suppressed > 0)
    {
        diagnostics_emit(DIAGNOSTIC_WARNING, component, EVENT_MESSAGES_SUPPRESSED, FORMAT_MESSAGES_SUPPRESSED, suppressed,
                         event_code);
    }
    va_list args;
    va_start(args, format);
    format_and_emit(severity, component, event_code, format, args);
    va_end(args);
}
