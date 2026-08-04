#pragma once

#include "diagnostics_core.h"

typedef void (*diagnostics_sink_function_t)(void *context, const char *record);

/* Selects an optional bounded live stream sink while retaining platform logging. */
void diagnostics_set_sink(diagnostics_sink_function_t sink, void *context);

/* Emits one structured diagnostic event through the platform logger. */
void diagnostics_emit(diagnostic_severity_t severity, const char *component, const char *event_code, const char *format, ...);

/* Emits an event only when its bounded rate limiter permits another message. */
void diagnostics_emit_limited(diagnostic_rate_limiter_t *limiter, uint32_t window_ms, uint32_t maximum_events,
                              diagnostic_severity_t severity, const char *component, const char *event_code, const char *format,
                              ...);
