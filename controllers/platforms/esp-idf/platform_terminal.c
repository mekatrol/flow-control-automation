#include "platform/terminal.h"

#include "driver/usb_serial_jtag.h"

/* The USB driver buffers absorb short bursts without letting a peer block runtime work. */
enum
{
    TERMINAL_USB_RX_BUFFER_SIZE = 256,
    TERMINAL_USB_TX_BUFFER_SIZE = 4096,
};
static bool is_terminal_ready;

/* Initializes the board terminal transport with bounded non-blocking buffers. */
bool platform_terminal_initialize(void)
{
    usb_serial_jtag_driver_config_t config = {.tx_buffer_size = TERMINAL_USB_TX_BUFFER_SIZE,
                                              .rx_buffer_size = TERMINAL_USB_RX_BUFFER_SIZE};
    is_terminal_ready                      = usb_serial_jtag_driver_install(&config) == ESP_OK;
    return is_terminal_ready;
}

/* Reads immediately available terminal bytes without waiting for a peer. */
size_t platform_terminal_read(uint8_t *data, size_t capacity)
{
    if (!is_terminal_ready)
    {
        return 0;
    }
    const int size = usb_serial_jtag_read_bytes(data, capacity, 0);
    return size > 0 ? (size_t)size : 0;
}

/* Queues or writes one bounded terminal record and reports slow-reader failure. */
bool platform_terminal_write(const char *data, size_t size)
{
    return is_terminal_ready && usb_serial_jtag_write_bytes(data, size, 0) == (int)size;
}
