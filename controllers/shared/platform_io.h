#pragma once

#include <stdbool.h>
#include <stdint.h>

typedef struct
{
    int sda_gpio;
    int scl_gpio;
    uint32_t clock_hz;
    uint8_t input_addresses[2];
    uint8_t output_addresses[2];
    bool are_inputs_active_low;
    bool are_outputs_active_low;
} platform_io_config_t;

/* Initializes the board I2C bus and all four PCF8574 devices without driving outputs active. */
bool platform_io_initialize(const platform_io_config_t *config);

/* Reads all input and output expanders into logical active-high bitmaps. */
void platform_io_read(uint16_t *inputs, bool *are_inputs_valid, uint16_t *outputs, bool *are_outputs_valid);

/* Writes all sixteen logical output states and reports whether both expander banks accepted them. */
bool platform_io_write_outputs(uint16_t outputs);
