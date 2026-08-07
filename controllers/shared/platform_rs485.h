#pragma once

#include <stdbool.h>
#include <stddef.h>
#include <stdint.h>

#include "rs485_service.h"

/* Platform events own received bytes so no ESP-IDF callback storage escapes its lifetime. */
typedef enum
{
    PLATFORM_RS485_EVENT_DATA,
    PLATFORM_RS485_EVENT_FRAMING_ERROR,
    PLATFORM_RS485_EVENT_PARITY_ERROR,
    PLATFORM_RS485_EVENT_OVERFLOW,
    PLATFORM_RS485_EVENT_QUEUE_DROP,
} platform_rs485_event_type_t;

typedef struct
{
    platform_rs485_event_type_t type;
    size_t size;
    uint8_t data[RS485_FRAME_CAPACITY];
} platform_rs485_event_t;

/* Initializes the board UART and bounded driver queues without waiting for bus traffic. */
bool platform_rs485_initialize(const rs485_config_t *config);

/* Applies a validated UART format to an initialized port without waiting for traffic. */
bool platform_rs485_reconfigure(const rs485_config_t *config);

/* Copies bytes to the UART driver transmit buffer without waiting for wire completion. */
bool platform_rs485_write(const uint8_t *data, size_t size);

/* Gets one owned UART event without blocking, or reports an empty queue. */
bool platform_rs485_get_event(platform_rs485_event_t *event);
