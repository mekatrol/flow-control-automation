#pragma once

#include <stdbool.h>

#include "network_manager.h"

/* Starts the non-blocking controller runtime task and reports creation success. */
bool controller_runtime_start(void);

/* Gets the runtime-owned network manager for read-only consumer discovery. */
const network_manager_t *get_controller_runtime_network_manager(void);
