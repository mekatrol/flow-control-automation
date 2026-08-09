#pragma once

#include <stdbool.h>
#include <stddef.h>
#include <stdint.h>

/* Portable task entry signature implemented by platform task facilities. */
typedef void (*platform_task_function_t)(void *context);

/* Portable severity values translated by each platform logging adapter. */
typedef enum
{
    PLATFORM_LOG_DEBUG,
    PLATFORM_LOG_INFO,
    PLATFORM_LOG_WARNING,
    PLATFORM_LOG_ERROR,
} platform_log_level_t;

typedef struct
{
    const char *firmware_name;
    const char *firmware_version;
    const char *processor;
    const char *reset_reason;
    uint32_t processor_cores;
    uint32_t silicon_revision_major;
    uint32_t silicon_revision_minor;
    uint64_t flash_bytes;
    uint64_t external_ram_bytes;
} platform_startup_info_t;

/* Gets immutable platform and firmware properties used by the startup banner. */
void platform_get_startup_info(platform_startup_info_t *info);

/* Formats a stable non-secret device identity for discovery and protocol responses. */
void platform_get_device_id(char *output, size_t capacity);

/* Gets elapsed platform time from a monotonic clock in milliseconds. */
uint64_t platform_get_monotonic_ms(void);

/* Gets monotonic microseconds for bounded execution-duration measurements. */
uint64_t platform_get_monotonic_us(void);

/* Gets currently available general-purpose heap memory in bytes. */
uint64_t platform_get_free_heap_bytes(void);

/* Gets platform hardware entropy for randomized supervisor retry delays. */
uint32_t platform_get_random_u32(void);

/* Starts a platform task and reports whether task creation succeeded. */
bool platform_start_task(const char *name, platform_task_function_t function, void *context, size_t stack_size,
                         unsigned priority);

/* Delays only the calling task for at least the requested duration. */
void platform_delay_ms(uint32_t delay_ms);

/* Writes a diagnostic message through the platform's logging transport. */
void platform_log(platform_log_level_t level, const char *component, const char *message);

/* Requests a normal platform reboot after callers complete bounded output flushing. */
bool platform_reboot(void);
