#include <assert.h>
#include <stdio.h>
#include <string.h>

#include "flow/service.h"

static controller_flow_metadata_t durable_metadata;
static uint8_t durable_artifact[CONTROLLER_FLOW_ARTIFACT_CAPACITY];
static bool has_durable;
static const char TEST_SUCCESS_MESSAGE[] = "Controller flow tests passed";

/* Produces a deterministic 32-byte fixture digest for transfer-state tests. */
static bool get_digest(void *context, const uint8_t *data, size_t size, uint8_t digest[CONTROLLER_FLOW_DIGEST_SIZE])
{
    assert(context == NULL);
    memset(digest, 0, CONTROLLER_FLOW_DIGEST_SIZE);

    for (size_t index = 0; index < size; index++)
    {
        digest[index % CONTROLLER_FLOW_DIGEST_SIZE] ^= data[index];
    }
    return true;
}

/* Accepts only fixture schema one so semantic validation failures remain testable. */
static bool is_artifact_valid(void *context, const controller_flow_metadata_t *metadata, const uint8_t *artifact)
{
    assert(context == NULL && artifact != NULL);
    return metadata->artifact_schema == 1;
}

/* Loads the last atomically published fixture generation when one exists. */
static bool load_flow(void *context, controller_flow_metadata_t *metadata, uint8_t *artifact, size_t capacity)
{
    assert(context == NULL);

    if (!has_durable || durable_metadata.size > capacity)
    {
        return false;
    }
    *metadata = durable_metadata;
    memcpy(artifact, durable_artifact, metadata->size);
    return true;
}

/* Atomically replaces the fixture generation used for reboot-recovery tests. */
static bool commit_flow(void *context, const controller_flow_metadata_t *metadata, const uint8_t *artifact)
{
    assert(context == NULL);
    durable_metadata = *metadata;
    memcpy(durable_artifact, artifact, metadata->size);
    has_durable = true;
    return true;
}

/* Removes the fixture generation after service-level active-state validation. */
static bool remove_flow(void *context)
{
    assert(context == NULL);
    has_durable = false;
    return true;
}

/* Builds a clean service over the deterministic durable fixture store. */
static controller_flow_t get_flow(void)
{
    controller_flow_t flow;
    const controller_flow_store_t store = {.load = load_flow, .commit = commit_flow, .remove = remove_flow};
    assert(controller_flow_init(&flow, get_digest, is_artifact_valid, NULL, &store));
    return flow;
}

/* Creates metadata whose digest matches the supplied fixture artifact. */
static controller_flow_metadata_t get_metadata(const uint8_t *artifact, size_t size, uint32_t revision)
{
    controller_flow_metadata_t metadata = {.revision = revision, .artifact_schema = 1, .size = size};
    memcpy(metadata.id, "flow-1", sizeof("flow-1"));
    assert(get_digest(NULL, artifact, size, metadata.digest));
    return metadata;
}

/* Verifies out-of-order chunks, exact duplicates, validation, commit, download, and reboot recovery. */
static void test_transfer_round_trip(void)
{
    has_durable                               = false;
    controller_flow_t flow                    = get_flow();
    const uint8_t artifact[]                  = {1, 2, 3, 4, 5, 6};
    const controller_flow_metadata_t metadata = get_metadata(artifact, sizeof(artifact), 1);
    assert(controller_flow_begin(&flow, &metadata, false, 0, 10) == CONTROLLER_FLOW_OK);
    assert(controller_flow_write(&flow, 10, 3, &artifact[3], 3) == CONTROLLER_FLOW_OK);
    assert(controller_flow_write(&flow, 10, 0, artifact, 3) == CONTROLLER_FLOW_OK);
    assert(controller_flow_write(&flow, 10, 0, artifact, 3) == CONTROLLER_FLOW_OK);
    assert(controller_flow_validate(&flow, 10) == CONTROLLER_FLOW_OK);
    assert(controller_flow_commit(&flow, 10) == CONTROLLER_FLOW_OK);
    uint8_t downloaded[sizeof(artifact)];
    size_t downloaded_size = 0;
    assert(controller_flow_read(&flow, 0, downloaded, sizeof(downloaded), &downloaded_size) == CONTROLLER_FLOW_OK);
    assert(downloaded_size == sizeof(artifact) && memcmp(downloaded, artifact, sizeof(artifact)) == 0);
    controller_flow_t recovered = get_flow();
    controller_flow_metadata_t recovered_metadata;
    assert(controller_flow_get_metadata(&recovered, &recovered_metadata) == CONTROLLER_FLOW_OK);
    assert(recovered_metadata.revision == 1);
}

/* Verifies conflicting overlap, incomplete coverage, digest mismatch, abort, and revision checks. */
static void test_rejections(void)
{
    has_durable                         = false;
    controller_flow_t flow              = get_flow();
    const uint8_t artifact[]            = {1, 2, 3};
    controller_flow_metadata_t metadata = get_metadata(artifact, sizeof(artifact), 1);
    assert(controller_flow_begin(&flow, &metadata, false, 0, 11) == CONTROLLER_FLOW_OK);
    assert(controller_flow_write(&flow, 11, 0, artifact, 2) == CONTROLLER_FLOW_OK);
    const uint8_t conflict = 9;
    assert(controller_flow_write(&flow, 11, 1, &conflict, 1) == CONTROLLER_FLOW_INVALID_ARGUMENT);
    assert(controller_flow_validate(&flow, 11) == CONTROLLER_FLOW_WRONG_STATE);
    assert(controller_flow_abort(&flow, 11) == CONTROLLER_FLOW_OK);
    metadata.digest[0] ^= 1U;
    assert(controller_flow_begin(&flow, &metadata, false, 0, 12) == CONTROLLER_FLOW_OK);
    assert(controller_flow_write(&flow, 12, 0, artifact, sizeof(artifact)) == CONTROLLER_FLOW_OK);
    assert(controller_flow_validate(&flow, 12) == CONTROLLER_FLOW_DIGEST_MISMATCH);
    assert(controller_flow_abort(&flow, 12) == CONTROLLER_FLOW_OK);
    metadata                 = get_metadata(artifact, sizeof(artifact), 2);
    metadata.artifact_schema = 2;
    assert(controller_flow_begin(&flow, &metadata, false, 0, 13) == CONTROLLER_FLOW_OK);
    assert(controller_flow_write(&flow, 13, 0, artifact, sizeof(artifact)) == CONTROLLER_FLOW_OK);
    assert(controller_flow_validate(&flow, 13) == CONTROLLER_FLOW_VALIDATION_FAILED);
}

/* Runs bounded transactional flow tests and returns success. */
int main(void)
{
    test_transfer_round_trip();
    test_rejections();
    puts(TEST_SUCCESS_MESSAGE);
    return 0;
}
