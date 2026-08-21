#include "flow/service.h"

/*
 * Purpose: Implement the portable staging and commit service for the single
 * durable flow generation supported by the controller. This file owns upload
 * coverage, retry-safe chunk merging, digest and semantic validation, revision
 * checks, activation metadata, committed reads, and removal.
 *
 * Why this file exists: A partial, corrupt, conflicting, or interrupted upload
 * must never damage the last complete production generation. Durable deployment
 * must also remain a separate architectural path from volatile debugging so a
 * debug operation cannot accidentally activate or replace controller behavior.
 *
 * How it works: Committed and staging artifacts occupy distinct bounded buffers.
 * Byte coverage makes identical chunk retries idempotent and rejects conflicting
 * overlap. Validation proves full coverage, digest integrity, and artifact
 * semantics before the platform's atomic store callback publishes the candidate.
 * In-memory committed state changes only after persistence succeeds, keeping
 * protocol-visible state consistent with what reboot recovery will load.
 */

#include <string.h>

/* What: Validates one durable flow ID as non-empty and terminated within capacity. Why: Persistence and protocol code must never
 * scan untrusted metadata beyond its fixed field. How: It searches only the declared array and accepts a terminator after at
 * least one byte. */
static bool is_id_valid(const char *id)
{
    for (size_t index = 0; index < CONTROLLER_FLOW_ID_CAPACITY; index++)
    {
        if (id[index] == '\0')
        {
            return index > 0;
        }
    }

    return false;
}

/* Tests whether one byte position was supplied, allowing retries without treating a transport timeout as transfer failure. */
static bool is_covered(const controller_flow_t *flow, size_t offset)
{
    return (flow->coverage[offset / 8U] & (uint8_t)(1U << (offset % 8U))) != 0U;
}

/* What: Records one newly accepted staging byte. Why: Validation requires exact complete coverage despite chunk retries and
 * overlap. How: It sets the corresponding bit and increments the unique-byte count once. */
static void set_covered(controller_flow_t *flow, size_t offset)
{
    flow->coverage[offset / 8U] |= (uint8_t)(1U << (offset % 8U));
    flow->covered_bytes++;
}

/* What: Erases the in-progress candidate and transfer bookkeeping. Why: Abort, commit, or a new upload must not leak stale
 * candidate bytes, while production remains available. How: It clears only staging fields and deliberately leaves committed
 * storage untouched. */
static void clear_staging(controller_flow_t *flow)
{
    /* Candidate bytes are disposable; committed bytes are intentionally outside this reset boundary. */
    memset(&flow->staging, 0, sizeof(flow->staging));
    memset(flow->staging_artifact, 0, sizeof(flow->staging_artifact));
    memset(flow->coverage, 0, sizeof(flow->coverage));
    flow->covered_bytes    = 0;
    flow->transfer_id      = 0;
    flow->is_transfer_open = false;
    flow->is_validated     = false;
}

/*
 * Initializes deployment storage and validates any recovered generation before
 * exposing it. A corrupt or incomplete durable image is cleared from RAM and
 * reported as initialization failure rather than becoming executable state.
 */
bool controller_flow_init(controller_flow_t *flow, controller_flow_digest_t get_digest,
                          controller_flow_validate_t is_artifact_valid, void *digest_context,
                          const controller_flow_store_t *store)
{
    if (flow == NULL || get_digest == NULL || is_artifact_valid == NULL || store == NULL || store->commit == NULL ||
        store->remove == NULL)
    {
        return false;
    }

    *flow                   = (controller_flow_t){0};
    flow->get_digest        = get_digest;
    flow->is_artifact_valid = is_artifact_valid;
    flow->digest_context    = digest_context;
    flow->store             = *store;

    if (store->load != NULL &&
        store->load(store->context, &flow->committed, flow->committed_artifact, sizeof(flow->committed_artifact)))
    {
        if (!is_id_valid(flow->committed.id) || flow->committed.size == 0 ||
            flow->committed.size > sizeof(flow->committed_artifact))
        {
            memset(&flow->committed, 0, sizeof(flow->committed));
            memset(flow->committed_artifact, 0, sizeof(flow->committed_artifact));

            return false;
        }

        uint8_t digest[CONTROLLER_FLOW_DIGEST_SIZE];

        if (!get_digest(digest_context, flow->committed_artifact, flow->committed.size, digest) ||
            memcmp(digest, flow->committed.digest, sizeof(digest)) != 0)
        {
            memset(&flow->committed, 0, sizeof(flow->committed));
            memset(flow->committed_artifact, 0, sizeof(flow->committed_artifact));

            return false;
        }

        flow->has_committed = true;
    }

    return true;
}

/*
 * Opens an isolated candidate generation after capacity and optimistic-revision
 * checks. The existing committed generation remains readable and unchanged
 * throughout upload, so interruption cannot remove production behavior.
 */
controller_flow_result_t controller_flow_begin(controller_flow_t *flow, const controller_flow_metadata_t *metadata,
                                               bool has_expected_revision, uint32_t expected_revision, uint32_t transfer_id)
{
    if (flow == NULL || metadata == NULL || !is_id_valid(metadata->id) || metadata->revision == 0 ||
        metadata->artifact_schema == 0 || metadata->size == 0 || metadata->size > CONTROLLER_FLOW_ARTIFACT_CAPACITY ||
        transfer_id == 0)
    {
        return metadata != NULL && metadata->size > CONTROLLER_FLOW_ARTIFACT_CAPACITY ? CONTROLLER_FLOW_STORAGE_FULL
                                                                                      : CONTROLLER_FLOW_INVALID_ARGUMENT;
    }

    if (flow->is_transfer_open)
    {
        return CONTROLLER_FLOW_WRONG_STATE;
    }

    if (has_expected_revision && (!flow->has_committed || flow->committed.revision != expected_revision))
    {
        return CONTROLLER_FLOW_REVISION_CONFLICT;
    }

    if (flow->has_committed && strcmp(flow->committed.id, metadata->id) != 0)
    {
        return CONTROLLER_FLOW_STORAGE_FULL;
    }

    clear_staging(flow);
    flow->staging           = *metadata;
    flow->staging.is_active = false;
    flow->transfer_id       = transfer_id;
    flow->is_transfer_open  = true;

    return CONTROLLER_FLOW_OK;
}

/*
 * Merges an offset chunk into staging with byte-level coverage. Identical
 * overlap is retry-safe; conflicting overlap is rejected because accepting it
 * would make the final digest depend on packet arrival order.
 */
controller_flow_result_t controller_flow_write(controller_flow_t *flow, uint32_t transfer_id, size_t offset, const uint8_t *data,
                                               size_t size)
{
    if (flow == NULL || data == NULL || size == 0 || !flow->is_transfer_open || flow->transfer_id != transfer_id)
    {
        return CONTROLLER_FLOW_WRONG_STATE;
    }

    if (offset > flow->staging.size || size > flow->staging.size - offset)
    {
        return CONTROLLER_FLOW_INVALID_ARGUMENT;
    }

    for (size_t index = 0; index < size; index++)
    {
        if (is_covered(flow, offset + index) && flow->staging_artifact[offset + index] != data[index])
        {
            return CONTROLLER_FLOW_INVALID_ARGUMENT;
        }
    }

    for (size_t index = 0; index < size; index++)
    {
        if (!is_covered(flow, offset + index))
        {
            flow->staging_artifact[offset + index] = data[index];
            set_covered(flow, offset + index);
        }
    }

    flow->is_validated = false;

    return CONTROLLER_FLOW_OK;
}

/*
 * Proves the candidate is complete, byte-identical to the declared digest, and
 * semantically acceptable to the installed artifact validator. Validation is
 * a gate only; durable state changes exclusively in commit.
 */
controller_flow_result_t controller_flow_validate(controller_flow_t *flow, uint32_t transfer_id)
{
    if (flow == NULL || !flow->is_transfer_open || flow->transfer_id != transfer_id)
    {
        return CONTROLLER_FLOW_WRONG_STATE;
    }

    if (flow->covered_bytes != flow->staging.size)
    {
        return CONTROLLER_FLOW_WRONG_STATE;
    }

    uint8_t digest[CONTROLLER_FLOW_DIGEST_SIZE];

    if (!flow->get_digest(flow->digest_context, flow->staging_artifact, flow->staging.size, digest))
    {
        return CONTROLLER_FLOW_VALIDATION_FAILED;
    }

    if (memcmp(digest, flow->staging.digest, sizeof(digest)) != 0)
    {
        return CONTROLLER_FLOW_DIGEST_MISMATCH;
    }

    if (!flow->is_artifact_valid(flow->digest_context, &flow->staging, flow->staging_artifact))
    {
        return CONTROLLER_FLOW_VALIDATION_FAILED;
    }

    flow->is_validated = true;

    return CONTROLLER_FLOW_OK;
}

/*
 * Asks the platform store to atomically publish a validated generation, then
 * mirrors it in RAM and discards staging. RAM changes only after persistence
 * succeeds, keeping protocol-visible state aligned with reboot recovery.
 */
controller_flow_result_t controller_flow_commit(controller_flow_t *flow, uint32_t transfer_id)
{
    if (flow == NULL || !flow->is_transfer_open || flow->transfer_id != transfer_id || !flow->is_validated)
    {
        return CONTROLLER_FLOW_WRONG_STATE;
    }

    if (!flow->store.commit(flow->store.context, &flow->staging, flow->staging_artifact))
    {
        return CONTROLLER_FLOW_STORAGE_UNAVAILABLE;
    }

    flow->committed = flow->staging;
    memcpy(flow->committed_artifact, flow->staging_artifact, flow->staging.size);
    flow->has_committed = true;
    clear_staging(flow);

    return CONTROLLER_FLOW_OK;
}

/* What: Aborts the matching open upload. Why: Clients need recoverable cancellation that cannot remove the last production
 * generation. How: It validates transfer identity and clears only staging state. */
controller_flow_result_t controller_flow_abort(controller_flow_t *flow, uint32_t transfer_id)
{
    if (flow == NULL || !flow->is_transfer_open || flow->transfer_id != transfer_id)
    {
        return CONTROLLER_FLOW_WRONG_STATE;
    }

    clear_staging(flow);

    return CONTROLLER_FLOW_OK;
}

/* What: Changes activation state for the committed generation. Why: Activation must survive reboot and must never refer to
 * staging bytes. How: It persists a copied metadata record first and updates the RAM mirror only after success. */
controller_flow_result_t controller_flow_set_active(controller_flow_t *flow, bool is_active)
{
    if (flow == NULL || !flow->has_committed)
    {
        return CONTROLLER_FLOW_NOT_FOUND;
    }

    controller_flow_metadata_t updated = flow->committed;
    updated.is_active                  = is_active;

    if (!flow->store.commit(flow->store.context, &updated, flow->committed_artifact))
    {
        return CONTROLLER_FLOW_STORAGE_UNAVAILABLE;
    }

    flow->committed = updated;

    return CONTROLLER_FLOW_OK;
}

/* What: Removes the durable generation only when it is inactive. Why: Deleting active production behavior would violate the
 * deployment safety boundary. How: It asks the atomic store to remove first, then clears the committed RAM mirror. */
controller_flow_result_t controller_flow_remove(controller_flow_t *flow)
{
    if (flow == NULL || !flow->has_committed)
    {
        return CONTROLLER_FLOW_NOT_FOUND;
    }

    if (flow->committed.is_active)
    {
        return CONTROLLER_FLOW_WRONG_STATE;
    }

    if (!flow->store.remove(flow->store.context))
    {
        return CONTROLLER_FLOW_STORAGE_UNAVAILABLE;
    }

    memset(&flow->committed, 0, sizeof(flow->committed));
    memset(flow->committed_artifact, 0, sizeof(flow->committed_artifact));
    flow->has_committed = false;

    return CONTROLLER_FLOW_OK;
}

/* What: Copies metadata for the complete committed generation. Why: Protocol inspection must not observe a partial upload as
 * deployable state. How: It rejects absent storage and returns only the committed record. */
controller_flow_result_t controller_flow_get_metadata(const controller_flow_t *flow, controller_flow_metadata_t *metadata)
{
    if (flow == NULL || metadata == NULL)
    {
        return CONTROLLER_FLOW_INVALID_ARGUMENT;
    }

    if (!flow->has_committed)
    {
        return CONTROLLER_FLOW_NOT_FOUND;
    }

    *metadata = flow->committed;
    return CONTROLLER_FLOW_OK;
}

/* What: Reads a bounded chunk of the committed artifact. Why: Transport callers need chunking without access to staging or
 * out-of-range memory. How: It validates the offset and capacity, copies at most the remaining bytes, and reports the exact
 * count. */
controller_flow_result_t controller_flow_read(const controller_flow_t *flow, size_t offset, uint8_t *output, size_t capacity,
                                              size_t *size)
{
    if (flow == NULL || output == NULL || size == NULL || capacity == 0)
    {
        return CONTROLLER_FLOW_INVALID_ARGUMENT;
    }

    if (!flow->has_committed)
    {
        return CONTROLLER_FLOW_NOT_FOUND;
    }

    if (offset > flow->committed.size)
    {
        return CONTROLLER_FLOW_INVALID_ARGUMENT;
    }

    const size_t remaining = flow->committed.size - offset;

    *size = remaining < capacity ? remaining : capacity;
    memcpy(output, &flow->committed_artifact[offset], *size);

    return CONTROLLER_FLOW_OK;
}
