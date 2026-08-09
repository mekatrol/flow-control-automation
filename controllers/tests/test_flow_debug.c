#include "flow/debug.h"
#include "flow/sha256.h"

#include <assert.h>
#include <stdio.h>
#include <string.h>

#ifndef FLOW_CONTRACT_FIXTURE_DIRECTORY
#error "FLOW_CONTRACT_FIXTURE_DIRECTORY must identify the shared fixture directory"
#endif

enum
{
    TEST_OWNER            = 7,
    TEST_OTHER_OWNER      = 8,
    TEST_ALL_CAPABILITIES = 0x1f,
};

static const flow_target_point_t TARGET_POINTS[] = {{.id = "input-01", .direction = 1, .value_type = 2},
                                                    {.id = "input-08", .direction = 1, .value_type = 2},
                                                    {.id = "output-01", .direction = 2, .value_type = 2}};
static const flow_target_t TARGET                = {.points                 = TARGET_POINTS,
                                                    .point_count            = sizeof(TARGET_POINTS) / sizeof(TARGET_POINTS[0]),
                                                    .supported_capabilities = TEST_ALL_CAPABILITIES,
                                                    .maximum_snapshot_bytes = FLOW_DEBUG_SNAPSHOT_CAPACITY};
static flow_input_sample_t INPUTS[]              = {{.point_id = "input-01", .quality = FLOW_QUALITY_GOOD},
                                                    {.point_id = "input-08", .quality = FLOW_QUALITY_GOOD}};

/* Supplies one coherent input image without exposing physical output adapters. */
static bool get_input(void * /* context */, flow_input_frame_t *frame)
{
    *frame = (flow_input_frame_t){
        .samples = INPUTS, .sample_count = sizeof(INPUTS) / sizeof(INPUTS[0]), .sampled_at_ms = 1000, .is_coherent = true};
    return true;
}

/* Loads one bounded golden artifact into caller storage. */
static size_t get_artifact(uint8_t *artifact, size_t capacity)
{
    char path[512];
    const int path_size = snprintf(path, sizeof(path), "%s/valid-two-button-and/artifact.bin", FLOW_CONTRACT_FIXTURE_DIRECTORY);
    assert(path_size > 0 && (size_t)path_size < sizeof(path));
    FILE *file = fopen(path, "rb");
    assert(file != NULL);
    const size_t size = fread(artifact, 1, capacity, file);
    assert(!ferror(file) && feof(file));
    assert(fclose(file) == 0);
    return size;
}

/* Loads, prepares, steps, and reassembles one immutable shadow snapshot. */
static void test_complete_lifecycle(void)
{
    uint8_t artifact[FLOW_EXECUTABLE_MAX_ARTIFACT_BYTES];
    const size_t artifact_size = get_artifact(artifact, sizeof(artifact));
    uint8_t digest[FLOW_DEBUG_DIGEST_BYTES];
    flow_sha256(artifact, artifact_size, digest);
    flow_debug_t debug;
    assert(flow_debug_init(&debug, &TARGET, get_input, NULL));
    uint64_t session_id = 0;
    assert(flow_debug_begin(&debug, TEST_OWNER, false, (uint32_t)artifact_size, digest, 10, &session_id) == FLOW_DEBUG_OK);
    for (size_t offset = 0; offset < artifact_size; offset += FLOW_DEBUG_CHUNK_LIMIT)
    {
        const size_t remaining = artifact_size - offset;
        const size_t size      = remaining < FLOW_DEBUG_CHUNK_LIMIT ? remaining : FLOW_DEBUG_CHUNK_LIMIT;
        assert(flow_debug_write(&debug, TEST_OWNER, session_id, (uint32_t)offset, &artifact[offset], size, 11) == FLOW_DEBUG_OK);
    }
    assert(flow_debug_prepare(&debug, TEST_OWNER, session_id, 12) == FLOW_DEBUG_OK);
    INPUTS[0].value = true;
    INPUTS[1].value = true;
    assert(flow_debug_step(&debug, TEST_OWNER, session_id, 13) == FLOW_DEBUG_OK);
    flow_debug_snapshot_header_t header;
    assert(flow_debug_get_snapshot_header(&debug, TEST_OWNER, session_id, 1, 14, &header) == FLOW_DEBUG_OK);
    assert(header.total_length > 0 && header.chunk_count > 0);
    uint8_t assembled[FLOW_DEBUG_SNAPSHOT_CAPACITY];
    size_t assembled_size = 0;
    for (uint16_t index = 0; index < header.chunk_count; index++)
    {
        uint32_t offset = 0;
        size_t size     = 0;
        assert(flow_debug_read_snapshot_chunk(&debug, TEST_OWNER, session_id, 1, index, 15, &assembled[assembled_size],
                                              sizeof(assembled) - assembled_size, &offset, &size) == FLOW_DEBUG_OK);
        assert(offset == assembled_size);
        assembled_size += size;
    }
    uint8_t assembled_digest[FLOW_DEBUG_DIGEST_BYTES];
    flow_sha256(assembled, assembled_size, assembled_digest);
    assert(assembled_size == header.total_length);
    assert(memcmp(assembled_digest, header.digest, sizeof(header.digest)) == 0);
    assert(flow_debug_stop(&debug, TEST_OWNER, session_id) == FLOW_DEBUG_OK);
    assert(debug.state == FLOW_DEBUG_EMPTY && debug.artifact_length == 0);
}

/* Verifies ownership hiding, conflicting overlaps, and lease cleanup. */
static void test_safety_and_expiry(void)
{
    uint8_t digest[FLOW_DEBUG_DIGEST_BYTES] = {0};
    flow_debug_t debug;
    assert(flow_debug_init(&debug, &TARGET, get_input, NULL));
    uint64_t session_id = 0;
    assert(flow_debug_begin(&debug, TEST_OWNER, false, 2, digest, 100, &session_id) == FLOW_DEBUG_OK);
    const uint8_t first[]    = {1, 2};
    const uint8_t conflict[] = {1, 3};
    assert(flow_debug_write(&debug, TEST_OWNER, session_id, 0, first, sizeof(first), 101) == FLOW_DEBUG_OK);
    assert(flow_debug_write(&debug, TEST_OWNER, session_id, 0, conflict, sizeof(conflict), 102) == FLOW_DEBUG_INVALID_ARGUMENT);
    flow_debug_status_t status;
    assert(flow_debug_get_status(&debug, TEST_OTHER_OWNER, session_id, 103, &status) == FLOW_DEBUG_FORBIDDEN);
    flow_debug_process(&debug, 101 + FLOW_DEBUG_LEASE_MS);
    assert(debug.state == FLOW_DEBUG_EMPTY && debug.session_id == 0);
}

/* Runs all volatile debug-session contract checks. */
int main(void)
{
    test_complete_lifecycle();
    test_safety_and_expiry();
    puts("flow debug tests passed");
    return 0;
}
