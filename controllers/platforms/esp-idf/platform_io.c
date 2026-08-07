#include "platform_io.h"

#include <stddef.h>

#include "driver/i2c_master.h"

/* I2C resource limits keep every field poll bounded. */
enum
{
    IO_BANK_COUNT = 2,
    IO_TIMEOUT_MS = 20,
};

static i2c_master_bus_handle_t io_bus;
static i2c_master_dev_handle_t input_devices[IO_BANK_COUNT];
static i2c_master_dev_handle_t output_devices[IO_BANK_COUNT];
static bool inputs_active_low;
static bool outputs_active_low;

/* Adds one PCF8574 address to the initialized board bus. */
static bool add_device(uint8_t address, uint32_t clock_hz, i2c_master_dev_handle_t *device)
{
    const i2c_device_config_t config = {
        .dev_addr_length = I2C_ADDR_BIT_LEN_7, .device_address = address, .scl_speed_hz = clock_hz};
    return i2c_master_bus_add_device(io_bus, &config, device) == ESP_OK;
}

/* Initializes the board I2C bus and all four PCF8574 devices without driving outputs active. */
bool platform_io_initialize(const platform_io_config_t *config)
{
    if (config == NULL || config->sda_gpio < 0 || config->scl_gpio < 0 || config->clock_hz == 0)
    {
        return false;
    }
    const i2c_master_bus_config_t bus_config = {.i2c_port                     = I2C_NUM_0,
                                                .sda_io_num                   = config->sda_gpio,
                                                .scl_io_num                   = config->scl_gpio,
                                                .clk_source                   = I2C_CLK_SRC_DEFAULT,
                                                .glitch_ignore_cnt            = 7,
                                                .flags.enable_internal_pullup = true};
    if (i2c_new_master_bus(&bus_config, &io_bus) != ESP_OK)
    {
        return false;
    }
    inputs_active_low  = config->are_inputs_active_low;
    outputs_active_low = config->are_outputs_active_low;
    for (size_t bank = 0; bank < IO_BANK_COUNT; bank++)
    {
        if (!add_device(config->input_addresses[bank], config->clock_hz, &input_devices[bank]) ||
            !add_device(config->output_addresses[bank], config->clock_hz, &output_devices[bank]))
        {
            return false;
        }
        const uint8_t released_inputs = UINT8_MAX;
        /* PCF8574 input pins must be released high so external optocouplers can pull them low. */
        if (i2c_master_transmit(input_devices[bank], &released_inputs, sizeof(released_inputs), IO_TIMEOUT_MS) != ESP_OK)
        {
            return false;
        }
    }
    return true;
}

/* Reads two PCF8574 devices and converts their bytes into one logical bitmap. */
static bool read_banks(i2c_master_dev_handle_t *devices, uint16_t *value)
{
    uint8_t banks[IO_BANK_COUNT];
    for (size_t bank = 0; bank < IO_BANK_COUNT; bank++)
    {
        if (i2c_master_receive(devices[bank], &banks[bank], 1, IO_TIMEOUT_MS) != ESP_OK)
        {
            return false;
        }
    }
    *value = (uint16_t)banks[0] | ((uint16_t)banks[1] << 8U);
    return true;
}

/* Reads all input and output expanders into logical active-high bitmaps. */
void platform_io_read(uint16_t *inputs, bool *are_inputs_valid, uint16_t *outputs, bool *are_outputs_valid)
{
    if (inputs == NULL || are_inputs_valid == NULL || outputs == NULL || are_outputs_valid == NULL)
    {
        return;
    }
    *are_inputs_valid  = read_banks(input_devices, inputs);
    *are_outputs_valid = read_banks(output_devices, outputs);
    if (*are_inputs_valid && inputs_active_low)
    {
        *inputs = (uint16_t)~*inputs;
    }
    if (*are_outputs_valid && outputs_active_low)
    {
        *outputs = (uint16_t)~*outputs;
    }
}

/* Writes all sixteen logical output states and reports whether both expander banks accepted them. */
bool platform_io_write_outputs(uint16_t outputs)
{
    const uint16_t electrical_outputs = outputs_active_low ? (uint16_t)~outputs : outputs;
    for (size_t bank = 0; bank < IO_BANK_COUNT; bank++)
    {
        const uint8_t value = (uint8_t)(electrical_outputs >> (bank * 8U));
        if (i2c_master_transmit(output_devices[bank], &value, sizeof(value), IO_TIMEOUT_MS) != ESP_OK)
        {
            return false;
        }
    }
    return true;
}
