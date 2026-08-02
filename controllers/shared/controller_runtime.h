#pragma once

#include <stdbool.h>

#include "network_manager.h"

bool controller_runtime_start(void);
const network_manager_t *controller_runtime_network_manager(void);
