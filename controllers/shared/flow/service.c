#include "flow/service.h"

#include <string.h>

/* Tests a bounded identifier for termination and non-empty content. */
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

/* Tests whether one byte position has already been supplied by a previous chunk. */
static bool is_covered(const controller_flow_t *flow, size_t offset)
{
    return (flow->coverage[offset / 8U] & (uint8_t)(1U << (offset % 8U))) != 0U;
}

/* Marks one newly accepted byte position as covered exactly once. */
static void set_covered(controller_flow_t *flow, size_t offset)
{
    flow->coverage[offset / 8U] |= (uint8_t)(1U << (offset % 8U));
    flow->covered_bytes++;
}

/* Clears staging state without disturbing the last complete committed generation. */
static void clear_staging(controller_flow_t *flow)
{
    memset(&flow->staging, 0, sizeof(flow->staging));
    memset(flow->staging_artifact, 0, sizeof(flow->staging_artifact));
    memset(flow->coverage, 0, sizeof(flow->coverage));
    flow->covered_bytes    = 0;
    flow->transfer_id      = 0;
    flow->is_transfer_open = false;
    flow->is_validated     = false;
}

/* Initializes a bounded flow service and recovers one complete durable generation when present. */
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

/* Begins one staged transfer after checking size, identity, and optional current revision. */
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

/* Writes one bounded chunk idempotently and rejects conflicting overlap. */
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

/* Verifies complete coverage and the declared SHA-256 before commit is permitted. */
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

/* Atomically publishes the validated staging generation without activating it. */
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

/* Abandons one staging transfer while retaining the complete committed generation. */
controller_flow_result_t controller_flow_abort(controller_flow_t *flow, uint32_t transfer_id)
{
    if (flow == NULL || !flow->is_transfer_open || flow->transfer_id != transfer_id)
    {
        return CONTROLLER_FLOW_WRONG_STATE;
    }
    clear_staging(flow);

    return CONTROLLER_FLOW_OK;
}

/* Selects whether the committed generation is active and persists that metadata atomically. */
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

/* Removes an inactive committed generation atomically. */
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

/* Gets committed metadata without exposing staging bytes. */
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

/* Copies one bounded range from the exact committed artifact. */
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
    *size                  = remaining < capacity ? remaining : capacity;
    memcpy(output, &flow->committed_artifact[offset], *size);

    return CONTROLLER_FLOW_OK;
}
