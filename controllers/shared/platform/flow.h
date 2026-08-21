#pragma once

#include "flow/virtual_points.h"

#include "flow/virtual_points.h"

#include <stdbool.h>
#include <stddef.h>
#include <stdint.h>

#include "flow/service.h"

/* Opens the dedicated durable flow namespace and returns atomic store callbacks. */
bool platform_flow_initialize(controller_flow_store_t *store);

/* Calculates SHA-256 for staged and recovered artifact integrity checks. */
bool platform_flow_get_digest(void *context, const uint8_t *data, size_t size, uint8_t digest[CONTROLLER_FLOW_DIGEST_SIZE]);

/* Validates the supported opaque transfer schema until an evaluator artifact schema is introduced. */
bool platform_flow_is_artifact_valid(void *context, const controller_flow_metadata_t *metadata, const uint8_t *artifact);

/**
 * Restores the versioned retained virtual-point image from NVS after contracts are allocated.
 * @param store Non-NULL initialized instance-global store with compatible retained contracts.
 * @return true when no image exists or a compatible image is restored; false on storage or compatibility failure.
 */
bool platform_flow_restore_virtual_points(flow_virtual_point_store_t *store);

/**
 * Atomically persists the current typed retained virtual-point image to NVS.
 * @param store Non-NULL initialized instance-global store serialized by the controller runtime task.
 * @return true when the image is durably committed; false on encoding or NVS failure.
 */
bool platform_flow_persist_virtual_points(const flow_virtual_point_store_t *store);

/**
 * Restores the versioned retained virtual-point image from NVS after contracts are allocated.
 * @param store Non-NULL initialized instance-global store with compatible retained contracts.
 * @return true when no image exists or a compatible image is restored; false on storage or compatibility failure.
 */
bool platform_flow_restore_virtual_points(flow_virtual_point_store_t *store);

/**
 * Atomically persists the current typed retained virtual-point image to NVS.
 * @param store Non-NULL initialized instance-global store serialized by the controller runtime task.
 * @return true when the image is durably committed; false on encoding or NVS failure.
 */
bool platform_flow_persist_virtual_points(const flow_virtual_point_store_t *store);
