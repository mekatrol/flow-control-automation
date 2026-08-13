#pragma once

/*
 * Purpose: Define the portable API and caller-owned state for staging,
 * validating, committing, activating, reading, and removing one durable
 * controller flow generation.
 *
 * Why this contract exists: Deployment requires atomic persistence and
 * optimistic revision control, while the portable layer must remain independent
 * of a board's flash layout. It is intentionally unrelated to flow/debug.h;
 * temporary debugging must never mutate production metadata or artifact bytes.
 *
 * How callers use it: Firmware supplies digest, artifact-validation, and atomic
 * storage callbacks. The service receives a candidate into separate staging
 * storage, tracks exact byte coverage, validates the complete artifact, and
 * calls the persistence boundary before updating its committed RAM mirror.
 * Public operations expose stable outcomes suitable for FCP error mapping.
 */

#include <stdbool.h>
#include <stddef.h>
#include <stdint.h>

/* One bounded durable flow is the initial controller capacity advertised to hosts. */
enum
{
    CONTROLLER_FLOW_ID_CAPACITY       = 65,
    CONTROLLER_FLOW_DIGEST_SIZE       = 32,
    CONTROLLER_FLOW_ARTIFACT_CAPACITY = 16384,
    CONTROLLER_FLOW_COVERAGE_SIZE     = CONTROLLER_FLOW_ARTIFACT_CAPACITY / 8,
};

typedef enum
{
    /* Results distinguish optimistic-concurrency, integrity, capacity, and persistence failures for stable protocol mapping. */
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
    /* Metadata identifies one complete generation; activation is persisted separately from artifact validation. */
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
    /* Store callbacks define the atomic persistence boundary and keep this portable service independent of flash layout. */
    bool (*load)(void *context, controller_flow_metadata_t *metadata, uint8_t *artifact, size_t capacity);
    bool (*commit)(void *context, const controller_flow_metadata_t *metadata, const uint8_t *artifact);
    bool (*remove)(void *context);
    void *context;
} controller_flow_store_t;

typedef struct
{
    /* Staging and committed buffers coexist so an interrupted upload cannot damage the last durable generation. */
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

/* What: Initializes deployment state. Why: Recovered production bytes must be proven intact before exposure. How: Installs
 * callbacks and loads, bounds-checks, and digests the durable generation. */
bool controller_flow_init(controller_flow_t *flow, controller_flow_digest_t get_digest,
                          controller_flow_validate_t is_artifact_valid, void *digest_context,
                          const controller_flow_store_t *store);

/* What: Opens a deployment upload. Why: Capacity and optimistic revision conflicts must fail before staging changes. How:
 * Validates metadata and creates an isolated transfer candidate. */
controller_flow_result_t controller_flow_begin(controller_flow_t *flow, const controller_flow_metadata_t *metadata,
                                               bool has_expected_revision, uint32_t expected_revision, uint32_t transfer_id);

/* What: Writes a staging chunk. Why: Retries must not make content depend on arrival order. How: Accepts identical overlap,
 * rejects conflicts, and records unique-byte coverage. */
controller_flow_result_t controller_flow_write(controller_flow_t *flow, uint32_t transfer_id, size_t offset, const uint8_t *data,
                                               size_t size);

/* What: Validates the staged generation. Why: Persistence may publish only complete, intact, semantically valid artifacts. How:
 * Checks coverage, digest, and the installed artifact validator. */
controller_flow_result_t controller_flow_validate(controller_flow_t *flow, uint32_t transfer_id);

/* What: Commits the staged generation. Why: Interrupted upload must not replace production and deployment must not imply
 * activation. How: Persists atomically before updating the committed RAM mirror. */
controller_flow_result_t controller_flow_commit(controller_flow_t *flow, uint32_t transfer_id);

/* What: Aborts staging. Why: Cancellation must be recoverable and production-safe. How: Verifies the transfer ID and clears only
 * candidate state. */
controller_flow_result_t controller_flow_abort(controller_flow_t *flow, uint32_t transfer_id);

/* What: Changes committed activation. Why: Active state must survive reboot and never reference staging. How: Persists copied
 * metadata before updating RAM. */
controller_flow_result_t controller_flow_set_active(controller_flow_t *flow, bool is_active);

/* What: Removes the durable flow. Why: Active production behavior must not be deleted. How: Rejects active state, persists
 * removal, then clears the RAM mirror. */
controller_flow_result_t controller_flow_remove(controller_flow_t *flow);

/* What: Reads committed metadata. Why: Partial candidates must not look deployable. How: Returns only the complete committed
 * record or not-found. */
controller_flow_result_t controller_flow_get_metadata(const controller_flow_t *flow, controller_flow_metadata_t *metadata);

/* What: Reads committed artifact bytes. Why: Protocol chunking must not expose staging or exceed bounds. How: Validates
 * offset/capacity and copies the available range. */
controller_flow_result_t controller_flow_read(const controller_flow_t *flow, size_t offset, uint8_t *output, size_t capacity,
                                              size_t *size);
