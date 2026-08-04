#pragma once

#include <stdbool.h>
#include <stddef.h>
#include <stdint.h>

/* Initializes the board terminal transport with bounded non-blocking buffers. */
bool platform_terminal_initialize(void);

/* Reads immediately available terminal bytes without waiting for a peer. */
size_t platform_terminal_read(uint8_t *data, size_t capacity);

/* Queues or writes one bounded terminal record and reports slow-reader failure. */
bool platform_terminal_write(const char *data, size_t size);
