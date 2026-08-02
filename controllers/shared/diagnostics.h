#pragma once

#include "diagnostics_core.h"

void diagnostics_emit(diagnostic_severity_t severity, const char *component,
                      const char *event_code, const char *format, ...);
void diagnostics_emit_limited(diagnostic_rate_limiter_t *limiter,
                              uint32_t window_ms, uint32_t maximum_events,
                              diagnostic_severity_t severity,
                              const char *component, const char *event_code,
                              const char *format, ...);
