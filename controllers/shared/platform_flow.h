#pragma once

#include <stdbool.h>
#include <stddef.h>
#include <stdint.h>

#include "controller_flow.h"

/* Opens the dedicated durable flow namespace and returns atomic store callbacks. */
bool platform_flow_initialize(controller_flow_store_t *store);

/* Calculates SHA-256 for staged and recovered artifact integrity checks. */
bool platform_flow_get_digest(void *context, const uint8_t *data, size_t size, uint8_t digest[CONTROLLER_FLOW_DIGEST_SIZE]);

/* Validates the supported opaque transfer schema until an evaluator artifact schema is introduced. */
bool platform_flow_is_artifact_valid(void *context, const controller_flow_metadata_t *metadata, const uint8_t *artifact);
