#pragma once

#include <stdbool.h>
#include <stddef.h>
#include <stdint.h>

typedef void (*platform_task_function_t)(void *context);

typedef enum {
    PLATFORM_LOG_DEBUG,
    PLATFORM_LOG_INFO,
    PLATFORM_LOG_WARNING,
    PLATFORM_LOG_ERROR,
} platform_log_level_t;

typedef struct {
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

void platform_get_startup_info(platform_startup_info_t *info);
uint64_t platform_monotonic_ms(void);
uint64_t platform_free_heap_bytes(void);
bool platform_start_task(const char *name, platform_task_function_t function,
                         void *context, size_t stack_size, unsigned priority);
void platform_delay_ms(uint32_t delay_ms);
void platform_log(platform_log_level_t level, const char *component,
                  const char *message);
