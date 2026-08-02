#pragma once

#include <stdbool.h>
#include <stddef.h>
#include <stdint.h>

typedef enum {
    DIAGNOSTIC_DEBUG,
    DIAGNOSTIC_INFO,
    DIAGNOSTIC_WARNING,
    DIAGNOSTIC_ERROR,
} diagnostic_severity_t;

typedef struct {
    uint64_t window_started_ms;
    uint32_t emitted;
    uint32_t suppressed;
    bool initialized;
} diagnostic_rate_limiter_t;

const char *diagnostic_severity_name(diagnostic_severity_t severity);
int diagnostic_format_event(char *output, size_t output_size,
                            diagnostic_severity_t severity,
                            const char *component, const char *event_code,
                            uint64_t timestamp_ms, const char *message);
int diagnostic_format_redacted_network_config(char *output, size_t output_size,
                                              const char *wifi_ssid,
                                              const char *wifi_password);
bool diagnostic_rate_limit(diagnostic_rate_limiter_t *limiter, uint64_t now_ms,
                           uint32_t window_ms, uint32_t maximum_events,
                           uint32_t *previously_suppressed);
