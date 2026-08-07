#pragma once

#include <stdbool.h>
#include <stddef.h>
#include <stdint.h>

/* One bounded durable flow is the initial controller capacity advertised to hosts. */
enum
{
    CONTROLLER_FLOW_ID_CAPACITY       = 65,
    CONTROLLER_FLOW_DIGEST_SIZE       = 32,
    CONTROLLER_FLOW_ARTIFACT_CAPACITY = 8192,
    CONTROLLER_FLOW_COVERAGE_SIZE     = CONTROLLER_FLOW_ARTIFACT_CAPACITY / 8,
};

typedef enum
{
    CONTROLLER_FLOW_OK,
    CONTROLLER_FLOW_INVALID_ARGUMENT,
    CONTROLLER_FLOW_WRONG_STATE,
    CONTROLLER_FLOW_REVISION_CONFLICT,
    CONTROLLER_FLOW_STORAGE_UNAVAILABLE,
    CONTROLLER_FLOW_STORAGE_FULL,
    CONTROLLER_FLOW_DIGEST_MISMATCH,
    CONTROLLER_FLOW_VALIDATION_FAILED,
    CONTROLLER_FLOW_NOT_FOUND,
} controller_flow_result_t;

typedef struct
{
    char id[CONTROLLER_FLOW_ID_CAPACITY];
    uint32_t revision;
    uint32_t artifact_schema;
    size_t size;
    uint8_t digest[CONTROLLER_FLOW_DIGEST_SIZE];
    bool is_active;
} controller_flow_metadata_t;

typedef bool (*controller_flow_digest_t)(void *context, const uint8_t *data, size_t size,
                                         uint8_t digest[CONTROLLER_FLOW_DIGEST_SIZE]);
typedef bool (*controller_flow_validate_t)(void *context, const controller_flow_metadata_t *metadata, const uint8_t *artifact);

/* Durable callbacks atomically load or replace one complete committed generation. */
typedef struct
{
    bool (*load)(void *context, controller_flow_metadata_t *metadata, uint8_t *artifact, size_t capacity);
    bool (*commit)(void *context, const controller_flow_metadata_t *metadata, const uint8_t *artifact);
    bool (*remove)(void *context);
    void *context;
} controller_flow_store_t;

typedef struct
{
    controller_flow_metadata_t committed;
    uint8_t committed_artifact[CONTROLLER_FLOW_ARTIFACT_CAPACITY];
    bool has_committed;
    controller_flow_metadata_t staging;
    uint8_t staging_artifact[CONTROLLER_FLOW_ARTIFACT_CAPACITY];
    uint8_t coverage[CONTROLLER_FLOW_COVERAGE_SIZE];
    size_t covered_bytes;
    uint32_t transfer_id;
    bool is_transfer_open;
    bool is_validated;
    controller_flow_digest_t get_digest;
    controller_flow_validate_t is_artifact_valid;
    void *digest_context;
    controller_flow_store_t store;
} controller_flow_t;

/* Initializes a bounded flow service and recovers one complete durable generation when present. */
bool controller_flow_init(controller_flow_t *flow, controller_flow_digest_t get_digest,
                          controller_flow_validate_t is_artifact_valid, void *digest_context,
                          const controller_flow_store_t *store);

/* Begins one staged transfer after checking size, identity, and optional current revision. */
controller_flow_result_t controller_flow_begin(controller_flow_t *flow, const controller_flow_metadata_t *metadata,
                                               bool has_expected_revision, uint32_t expected_revision, uint32_t transfer_id);

/* Writes one bounded chunk idempotently and rejects conflicting overlap. */
controller_flow_result_t controller_flow_write(controller_flow_t *flow, uint32_t transfer_id, size_t offset, const uint8_t *data,
                                               size_t size);

/* Verifies complete coverage and the declared SHA-256 before commit is permitted. */
controller_flow_result_t controller_flow_validate(controller_flow_t *flow, uint32_t transfer_id);

/* Atomically publishes the validated staging generation without activating it. */
controller_flow_result_t controller_flow_commit(controller_flow_t *flow, uint32_t transfer_id);

/* Abandons one staging transfer while retaining the complete committed generation. */
controller_flow_result_t controller_flow_abort(controller_flow_t *flow, uint32_t transfer_id);

/* Selects whether the committed generation is active and persists that metadata atomically. */
controller_flow_result_t controller_flow_set_active(controller_flow_t *flow, bool is_active);

/* Removes an inactive committed generation atomically. */
controller_flow_result_t controller_flow_remove(controller_flow_t *flow);

/* Gets committed metadata without exposing staging bytes. */
controller_flow_result_t controller_flow_get_metadata(const controller_flow_t *flow, controller_flow_metadata_t *metadata);

/* Copies one bounded range from the exact committed artifact. */
controller_flow_result_t controller_flow_read(const controller_flow_t *flow, size_t offset, uint8_t *output, size_t capacity,
                                              size_t *size);
