#include "platform/rs485.h"

#include <stdatomic.h>

#include "driver/uart.h"
#include "freertos/FreeRTOS.h"
#include "freertos/queue.h"

/* UART1 is dedicated to the board RS485 transceiver; queue sizes bound driver memory. */
enum
{
    RS485_UART_NUMBER      = UART_NUM_1,
    RS485_UART_BUFFER_SIZE = 1024,
    /* The event task needs room for one owned maximum-size frame plus ESP-IDF UART and queue call frames. */
    RS485_UART_TASK_STACK_SIZE = 4096,
    RS485_UART_EVENT_DEPTH     = 16,
    RS485_EVENT_QUEUE_DEPTH    = 16,
};

static QueueHandle_t uart_event_queue;
static QueueHandle_t platform_event_queue;
static atomic_uint platform_event_drop_count;

/* Maps portable data width to the ESP-IDF UART driver. */
static uart_word_length_t get_uart_data_bits(rs485_data_bits_t data_bits)
{
    return data_bits == RS485_DATA_BITS_7 ? UART_DATA_7_BITS : UART_DATA_8_BITS;
}

/* Maps portable parity to the ESP-IDF UART driver. */
static uart_parity_t get_uart_parity(rs485_parity_t parity)
{
    static const uart_parity_t values[] = {UART_PARITY_DISABLE, UART_PARITY_EVEN, UART_PARITY_ODD};
    return values[parity];
}

/* Maps portable stop bits to the ESP-IDF UART driver. */
static uart_stop_bits_t get_uart_stop_bits(rs485_stop_bits_t stop_bits)
{
    return stop_bits == RS485_STOP_BITS_2 ? UART_STOP_BITS_2 : UART_STOP_BITS_1;
}

/* Copies a translated driver event into the bounded service-facing queue without blocking. */
static void enqueue_platform_event(const platform_rs485_event_t *event)
{
    if (platform_event_queue != NULL)
    {
        if (xQueueSend(platform_event_queue, event, 0) != pdTRUE)
        {
            (void)atomic_fetch_add(&platform_event_drop_count, 1U);
        }
    }
}

/* Drains UART events in a dedicated task so the controller task never blocks on driver reads. */
static void rs485_uart_event_task(void * /* context */)
{
    uart_event_t uart_event;
    for (;;)
    {
        if (xQueueReceive(uart_event_queue, &uart_event, portMAX_DELAY) != pdTRUE)
        {
            continue;
        }
        platform_rs485_event_t event = {0};
        switch (uart_event.type)
        {
            case UART_DATA:
                event.type = PLATFORM_RS485_EVENT_DATA;
                event.size = uart_event.size > sizeof(event.data) ? sizeof(event.data) : uart_event.size;
                if (uart_read_bytes(RS485_UART_NUMBER, event.data, event.size, 0) > 0)
                {
                    enqueue_platform_event(&event);
                }
                break;
            case UART_FRAME_ERR:
                event.type = PLATFORM_RS485_EVENT_FRAMING_ERROR;
                enqueue_platform_event(&event);
                break;
            case UART_PARITY_ERR:
                event.type = PLATFORM_RS485_EVENT_PARITY_ERROR;
                enqueue_platform_event(&event);
                break;
            case UART_FIFO_OVF:
            case UART_BUFFER_FULL:
                event.type = PLATFORM_RS485_EVENT_OVERFLOW;
                (void)uart_flush_input(RS485_UART_NUMBER);
                (void)xQueueReset(uart_event_queue);
                enqueue_platform_event(&event);
                break;
            default:
                break;
        }
    }
}

/* Initializes the board UART and bounded driver queues without waiting for bus traffic. */
bool platform_rs485_initialize(const rs485_config_t *config)
{
    if (!is_rs485_config_valid(config) || !config->enabled)
    {
        return false;
    }
    platform_event_queue = xQueueCreate(RS485_EVENT_QUEUE_DEPTH, sizeof(platform_rs485_event_t));
    if (platform_event_queue == NULL)
    {
        return false;
    }
    const uart_config_t uart_config = {.baud_rate  = (int)config->baud_rate,
                                       .data_bits  = get_uart_data_bits(config->data_bits),
                                       .parity     = get_uart_parity(config->parity),
                                       .stop_bits  = get_uart_stop_bits(config->stop_bits),
                                       .flow_ctrl  = UART_HW_FLOWCTRL_DISABLE,
                                       .source_clk = UART_SCLK_DEFAULT};
    if (uart_driver_install(RS485_UART_NUMBER, RS485_UART_BUFFER_SIZE, RS485_UART_BUFFER_SIZE, RS485_UART_EVENT_DEPTH,
                            &uart_event_queue, 0) != ESP_OK ||
        uart_param_config(RS485_UART_NUMBER, &uart_config) != ESP_OK ||
        /* MAX13487 automatic direction needs only TX/RX; RTS must remain disconnected. */
        uart_set_pin(RS485_UART_NUMBER, config->transmit_gpio, config->receive_gpio, UART_PIN_NO_CHANGE, UART_PIN_NO_CHANGE) !=
            ESP_OK)
    {
        return false;
    }
    return xTaskCreate(rs485_uart_event_task, "rs485_uart_events", RS485_UART_TASK_STACK_SIZE, NULL, 6, NULL) == pdPASS;
}

/* Applies a validated UART format to an initialized port without waiting for traffic. */
bool platform_rs485_reconfigure(const rs485_config_t *config)
{
    if (!is_rs485_config_valid(config))
    {
        return false;
    }
    const uart_config_t uart_config = {.baud_rate  = (int)config->baud_rate,
                                       .data_bits  = get_uart_data_bits(config->data_bits),
                                       .parity     = get_uart_parity(config->parity),
                                       .stop_bits  = get_uart_stop_bits(config->stop_bits),
                                       .flow_ctrl  = UART_HW_FLOWCTRL_DISABLE,
                                       .source_clk = UART_SCLK_DEFAULT};
    return uart_param_config(RS485_UART_NUMBER, &uart_config) == ESP_OK;
}

/* Copies bytes to the UART driver transmit buffer without waiting for wire completion. */
bool platform_rs485_write(const uint8_t *data, size_t size)
{
    return data != NULL && size > 0 && uart_write_bytes(RS485_UART_NUMBER, data, size) == (int)size;
}

/* Gets one owned UART event without blocking, or reports an empty queue. */
bool platform_rs485_get_event(platform_rs485_event_t *event)
{
    if (platform_event_queue != NULL && xQueueReceive(platform_event_queue, event, 0) == pdTRUE)
    {
        return true;
    }
    /* Surface one counter event after congestion clears instead of silently losing callback data. */
    const unsigned drop_count = atomic_exchange(&platform_event_drop_count, 0U);
    if (drop_count > 0)
    {
        *event = (platform_rs485_event_t){.type = PLATFORM_RS485_EVENT_QUEUE_DROP, .size = drop_count};
        return true;
    }
    return false;
}
