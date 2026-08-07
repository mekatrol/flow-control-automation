#pragma once

#include <stdbool.h>
#include <stddef.h>
#include <stdint.h>

/* Compile-time capacities bound frame ownership and queue memory. */
enum
{
    RS485_FRAME_CAPACITY       = 256,
    RS485_TRANSMIT_QUEUE_LIMIT = 8,
    RS485_RECEIVE_QUEUE_LIMIT  = 8,
};

/* Supported data widths map directly to portable UART configuration. */
typedef enum
{
    RS485_DATA_BITS_7 = 7,
    RS485_DATA_BITS_8 = 8,
} rs485_data_bits_t;

/* Supported parity modes keep platform UART constants out of shared code. */
typedef enum
{
    RS485_PARITY_NONE,
    RS485_PARITY_EVEN,
    RS485_PARITY_ODD,
} rs485_parity_t;

/* Supported stop-bit formats keep platform UART constants out of shared code. */
typedef enum
{
    RS485_STOP_BITS_1,
    RS485_STOP_BITS_2,
} rs485_stop_bits_t;

/* Protocol selection reserves an explicit boundary above the raw byte transport. */
typedef enum
{
    RS485_PROTOCOL_RAW,
} rs485_protocol_t;

typedef struct
{
    bool enabled;
    int transmit_gpio;
    int receive_gpio;
    uint32_t baud_rate;
    rs485_data_bits_t data_bits;
    rs485_parity_t parity;
    rs485_stop_bits_t stop_bits;
    uint32_t receive_timeout_ms;
    size_t maximum_frame_size;
    size_t transmit_queue_depth;
    size_t receive_queue_depth;
    rs485_protocol_t protocol;
} rs485_config_t;

typedef struct
{
    size_t size;
    uint8_t data[RS485_FRAME_CAPACITY];
} rs485_frame_t;

/* Transport error categories allow platform adapters to report UART failures without SDK types. */
typedef enum
{
    RS485_TRANSPORT_ERROR_FRAMING,
    RS485_TRANSPORT_ERROR_PARITY,
    RS485_TRANSPORT_ERROR_OVERFLOW,
    RS485_TRANSPORT_ERROR_COLLISION,
    RS485_TRANSPORT_ERROR_PROTOCOL,
} rs485_transport_error_t;

/* Service states describe whether framing is available independently of peer traffic. */
typedef enum
{
    RS485_STATE_DISABLED,
    RS485_STATE_STARTING,
    RS485_STATE_ONLINE,
    RS485_STATE_DEGRADED,
} rs485_state_t;

typedef struct
{
    rs485_state_t state;
    uint32_t framing_error_count;
    uint32_t parity_error_count;
    uint32_t overflow_count;
    uint32_t timeout_count;
    uint32_t collision_count;
    uint32_t protocol_error_count;
    uint32_t transmit_queue_drop_count;
    uint32_t receive_queue_drop_count;
    size_t transmit_queue_depth;
    size_t receive_queue_depth;
} rs485_health_t;

/* Byte writes transfer immediately into platform-owned storage and never retain the caller buffer. */
typedef bool (*rs485_transport_write_t)(const uint8_t *data, size_t size);

typedef struct
{
    rs485_config_t config;
    rs485_transport_write_t transport_write;
    rs485_state_t state;
    rs485_frame_t transmit_queue[RS485_TRANSMIT_QUEUE_LIMIT];
    rs485_frame_t receive_queue[RS485_RECEIVE_QUEUE_LIMIT];
    size_t transmit_head;
    size_t transmit_count;
    size_t receive_head;
    size_t receive_count;
    uint8_t receive_buffer[RS485_FRAME_CAPACITY];
    size_t receive_size;
    uint64_t receive_deadline_ms;
    rs485_health_t counters;
} rs485_service_t;

/* Tests whether a board and Kconfig-derived RS485 configuration is safe and bounded. */
bool is_rs485_config_valid(const rs485_config_t *config);

/* Initializes framing and bounded queues after the platform UART is ready. */
bool rs485_service_init(rs485_service_t *service, const rs485_config_t *config, rs485_transport_write_t transport_write);

/* Copies one complete outbound frame into the bounded transmit queue. */
bool rs485_service_send(rs485_service_t *service, const uint8_t *data, size_t size);

/* Copies received UART bytes into the current timeout-delimited raw frame. */
void rs485_service_receive_bytes(rs485_service_t *service, const uint8_t *data, size_t size, uint64_t now_ms);

/* Records a reason-coded transport error and discards an invalid partial frame when required. */
void rs485_service_report_error(rs485_service_t *service, rs485_transport_error_t error);

/* Adds platform callback queue losses in one bounded operation after congestion clears. */
void rs485_service_report_queue_drops(rs485_service_t *service, uint32_t drop_count);

/* Advances timeout framing and transmits at most one queued frame without waiting for a peer. */
void rs485_service_process(rs485_service_t *service, uint64_t now_ms);

/* Gets and removes the oldest complete received frame, copying ownership to the caller. */
bool rs485_service_get_received(rs485_service_t *service, rs485_frame_t *frame);

/* Gets a read-only health snapshot containing all RS485 error and queue counters. */
rs485_health_t rs485_service_get_health(const rs485_service_t *service);

/* Gets the stable diagnostic name for an RS485 service state. */
const char *rs485_get_state_name(rs485_state_t state);
