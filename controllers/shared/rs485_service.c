#include "rs485_service.h"

#include <string.h>

/* Baud limits reject values outside the ESP32 UART and intended field-bus operating range. */
enum
{
    RS485_MINIMUM_BAUD_RATE = 300,
    RS485_MAXIMUM_BAUD_RATE = 3000000,
};

static const char STATE_DISABLED[] = "disabled";
static const char STATE_STARTING[] = "starting";
static const char STATE_ONLINE[]   = "online";
static const char STATE_DEGRADED[] = "degraded";

/* Tests whether a board and Kconfig-derived RS485 configuration is safe and bounded. */
bool is_rs485_config_valid(const rs485_config_t *config)
{
    return config != NULL && config->transmit_gpio >= 0 && config->receive_gpio >= 0 &&
           config->transmit_gpio != config->receive_gpio && config->baud_rate >= RS485_MINIMUM_BAUD_RATE &&
           config->baud_rate <= RS485_MAXIMUM_BAUD_RATE &&
           (config->data_bits == RS485_DATA_BITS_7 || config->data_bits == RS485_DATA_BITS_8) &&
           config->parity <= RS485_PARITY_ODD && config->stop_bits <= RS485_STOP_BITS_2 && config->receive_timeout_ms > 0 &&
           config->maximum_frame_size > 0 && config->maximum_frame_size <= RS485_FRAME_CAPACITY &&
           config->transmit_queue_depth > 0 && config->transmit_queue_depth <= RS485_TRANSMIT_QUEUE_LIMIT &&
           config->receive_queue_depth > 0 && config->receive_queue_depth <= RS485_RECEIVE_QUEUE_LIMIT &&
           config->protocol == RS485_PROTOCOL_RAW;
}

/* Initializes framing and bounded queues after the platform UART is ready. */
bool rs485_service_init(rs485_service_t *service, const rs485_config_t *config, rs485_transport_write_t transport_write)
{
    if (service == NULL)
    {
        return false;
    }
    *service = (rs485_service_t){0};
    if (config == NULL || !config->enabled)
    {
        service->state = RS485_STATE_DISABLED;
        return true;
    }
    if (!is_rs485_config_valid(config) || transport_write == NULL)
    {
        service->state = RS485_STATE_DEGRADED;
        return false;
    }
    service->config          = *config;
    service->transport_write = transport_write;
    service->state           = RS485_STATE_ONLINE;
    return true;
}

/* Copies one complete outbound frame into the bounded transmit queue. */
bool rs485_service_send(rs485_service_t *service, const uint8_t *data, size_t size)
{
    if (service == NULL || service->state == RS485_STATE_DISABLED || data == NULL || size == 0 ||
        size > service->config.maximum_frame_size)
    {
        return false;
    }
    if (service->transmit_count >= service->config.transmit_queue_depth)
    {
        service->counters.transmit_queue_drop_count++;
        return false;
    }
    const size_t tail = (service->transmit_head + service->transmit_count) % service->config.transmit_queue_depth;
    service->transmit_queue[tail].size = size;
    (void)memcpy(service->transmit_queue[tail].data, data, size);
    service->transmit_count++;
    return true;
}

/* Completes the current timeout-delimited frame and transfers it to the receive queue. */
static void complete_receive_frame(rs485_service_t *service)
{
    if (service->receive_size == 0)
    {
        return;
    }
    if (service->receive_count >= service->config.receive_queue_depth)
    {
        service->counters.receive_queue_drop_count++;
        service->receive_size = 0;
        return;
    }
    const size_t tail                 = (service->receive_head + service->receive_count) % service->config.receive_queue_depth;
    service->receive_queue[tail].size = service->receive_size;
    (void)memcpy(service->receive_queue[tail].data, service->receive_buffer, service->receive_size);
    service->receive_count++;
    service->receive_size = 0;
}

/* Copies received UART bytes into the current timeout-delimited raw frame. */
void rs485_service_receive_bytes(rs485_service_t *service, const uint8_t *data, size_t size, uint64_t now_ms)
{
    if (service == NULL || service->state == RS485_STATE_DISABLED || data == NULL || size == 0)
    {
        return;
    }
    /* Flush an expired frame before appending because the byte gap is the raw framing boundary. */
    if (service->receive_size > 0 && now_ms >= service->receive_deadline_ms)
    {
        complete_receive_frame(service);
    }
    if (size > service->config.maximum_frame_size - service->receive_size)
    {
        service->counters.overflow_count++;
        service->receive_size = 0;
        service->state        = RS485_STATE_DEGRADED;
        return;
    }
    (void)memcpy(&service->receive_buffer[service->receive_size], data, size);
    service->receive_size += size;
    service->receive_deadline_ms = now_ms + service->config.receive_timeout_ms;
}

/* Records a reason-coded transport error and discards an invalid partial frame when required. */
void rs485_service_report_error(rs485_service_t *service, rs485_transport_error_t error)
{
    if (service == NULL)
    {
        return;
    }
    switch (error)
    {
        case RS485_TRANSPORT_ERROR_FRAMING:
            service->counters.framing_error_count++;
            break;
        case RS485_TRANSPORT_ERROR_PARITY:
            service->counters.parity_error_count++;
            break;
        case RS485_TRANSPORT_ERROR_OVERFLOW:
            service->counters.overflow_count++;
            break;
        case RS485_TRANSPORT_ERROR_COLLISION:
            service->counters.collision_count++;
            break;
        case RS485_TRANSPORT_ERROR_PROTOCOL:
            service->counters.protocol_error_count++;
            break;
    }
    /* UART errors invalidate the pending byte sequence, preventing corrupt frames reaching consumers. */
    service->receive_size = 0;
    service->state        = RS485_STATE_DEGRADED;
}

/* Adds platform callback queue losses in one bounded operation after congestion clears. */
void rs485_service_report_queue_drops(rs485_service_t *service, uint32_t drop_count)
{
    if (service != NULL)
    {
        service->counters.receive_queue_drop_count += drop_count;
        service->state = RS485_STATE_DEGRADED;
    }
}

/* Advances timeout framing and transmits at most one queued frame without waiting for a peer. */
void rs485_service_process(rs485_service_t *service, uint64_t now_ms)
{
    if (service == NULL || service->state == RS485_STATE_DISABLED)
    {
        return;
    }
    if (service->receive_size > 0 && now_ms >= service->receive_deadline_ms)
    {
        service->counters.timeout_count++;
        complete_receive_frame(service);
    }
    if (service->transmit_count > 0)
    {
        const rs485_frame_t *frame = &service->transmit_queue[service->transmit_head];
        if (!service->transport_write(frame->data, frame->size))
        {
            service->state = RS485_STATE_DEGRADED;
            return;
        }
        service->transmit_head = (service->transmit_head + 1) % service->config.transmit_queue_depth;
        service->transmit_count--;
    }
    /* Successful bounded processing proves the local UART path remains serviceable after an error. */
    service->state = RS485_STATE_ONLINE;
}

/* Gets and removes the oldest complete received frame, copying ownership to the caller. */
bool rs485_service_get_received(rs485_service_t *service, rs485_frame_t *frame)
{
    if (service == NULL || frame == NULL || service->receive_count == 0)
    {
        return false;
    }
    *frame                = service->receive_queue[service->receive_head];
    service->receive_head = (service->receive_head + 1) % service->config.receive_queue_depth;
    service->receive_count--;
    return true;
}

/* Gets a read-only health snapshot containing all RS485 error and queue counters. */
rs485_health_t rs485_service_get_health(const rs485_service_t *service)
{
    if (service == NULL)
    {
        return (rs485_health_t){.state = RS485_STATE_DISABLED};
    }
    rs485_health_t health       = service->counters;
    health.state                = service->state;
    health.transmit_queue_depth = service->transmit_count;
    health.receive_queue_depth  = service->receive_count;
    return health;
}

/* Gets the stable diagnostic name for an RS485 service state. */
const char *rs485_get_state_name(rs485_state_t state)
{
    static const char *const names[] = {STATE_DISABLED, STATE_STARTING, STATE_ONLINE, STATE_DEGRADED};
    return state <= RS485_STATE_DEGRADED ? names[state] : STATE_DEGRADED;
}
