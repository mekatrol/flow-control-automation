#include "flow/executable.h"
#include "flow/runtime.h"

#include <assert.h>
#include <stdio.h>
#include <string.h>

#ifndef FLOW_CONTRACT_FIXTURE_DIRECTORY
#error "FLOW_CONTRACT_FIXTURE_DIRECTORY must identify the shared fixture directory"
#endif

enum
{
    TEST_POINT_READ             = 1,
    TEST_POINT_PROPOSED_WRITE   = 2,
    TEST_DIGITAL_TYPE           = 2,
    TEST_ALL_CAPABILITIES       = 0x1f,
    TEST_MAXIMUM_SNAPSHOT_BYTES = 16384,
};

static const flow_target_point_t TARGET_POINTS[] = {
    {.id = "input-01", .direction = TEST_POINT_READ, .value_type = TEST_DIGITAL_TYPE},
    {.id = "input-08", .direction = TEST_POINT_READ, .value_type = TEST_DIGITAL_TYPE},
    {.id = "output-01", .direction = TEST_POINT_PROPOSED_WRITE, .value_type = TEST_DIGITAL_TYPE}};
static const flow_target_t TARGET = {.points                 = TARGET_POINTS,
                                     .point_count            = sizeof(TARGET_POINTS) / sizeof(TARGET_POINTS[0]),
                                     .supported_capabilities = TEST_ALL_CAPABILITIES,
                                     .maximum_snapshot_bytes = TEST_MAXIMUM_SNAPSHOT_BYTES};

/* Loads one bounded artifact fixture and returns its exact byte count. */
static size_t get_fixture(const char *relative_path, uint8_t *bytes, size_t capacity)
{
    char path[512];
    const int path_length = snprintf(path, sizeof(path), "%s/%s", FLOW_CONTRACT_FIXTURE_DIRECTORY, relative_path);
    assert(path_length > 0 && (size_t)path_length < sizeof(path));
    FILE *file = fopen(path, "rb");
    assert(file != NULL);
    const size_t size = fread(bytes, 1, capacity, file);
    assert(!ferror(file));
    assert(feof(file));
    assert(fclose(file) == 0);
    return size;
}

/* Prepares one named fixture against the frozen target template. */
static flow_result_t get_prepared_fixture(const char *fixture_id, flow_executable_t *flow)
{
    uint8_t bytes[FLOW_EXECUTABLE_MAX_ARTIFACT_BYTES];
    char path[128];
    const int length = snprintf(path, sizeof(path), "%s/artifact.bin", fixture_id);
    assert(length > 0 && (size_t)length < sizeof(path));
    const size_t size = get_fixture(path, bytes, sizeof(bytes));
    return flow_executable_prepare(bytes, size, &TARGET, flow);
}

/* Finds a node's value in the artifact-ordered immutable snapshot. */
static bool get_node_value(const flow_tick_snapshot_t *snapshot, const char *node_id)
{
    for (uint16_t index = 0; index < snapshot->node_count; index++)
    {
        if (strcmp(snapshot->nodes[index].node_id, node_id) == 0)
        {
            return snapshot->nodes[index].value;
        }
    }
    assert(false);
    return false;
}

/* Checks all valid and invalid golden artifacts return their frozen result and path. */
static void test_validation_fixtures(void)
{
    static const struct
    {
        const char *id;
        flow_reason_code_t code;
        const char *path;
    } CASES[] = {{"valid-two-button-and", FLOW_REASON_OK, ""},
                 {"valid-memory-feedback", FLOW_REASON_OK, ""},
                 {"valid-source-order-permutation", FLOW_REASON_OK, ""},
                 {"malformed-truncated", FLOW_REASON_LENGTH_MISMATCH, "/artifactLength"},
                 {"incompatible-types", FLOW_REASON_INCOMPATIBLE_TYPE, "/connections/0"},
                 {"missing-point", FLOW_REASON_MISSING_POINT, "/points/input-99"},
                 {"combinational-cycle", FLOW_REASON_COMBINATIONAL_CYCLE, "/nodes/not-a"},
                 {"noncanonical-node-order", FLOW_REASON_NON_CANONICAL_ORDER, "/nodes/1"}};

    for (size_t index = 0; index < sizeof(CASES) / sizeof(CASES[0]); index++)
    {
        flow_executable_t flow;
        const flow_result_t result = get_prepared_fixture(CASES[index].id, &flow);
        assert(result.code == CASES[index].code);
        assert(strcmp(result.path, CASES[index].path) == 0);
    }
}

/* Checks the two-button fixture executes its three frozen truth-table frames. */
static void test_two_button_ticks(void)
{
    flow_executable_t flow;
    assert(get_prepared_fixture("valid-two-button-and", &flow).code == FLOW_REASON_OK);
    flow_runtime_t runtime;
    assert(flow_runtime_init(&runtime, &flow));
    flow_input_sample_t samples[] = {{.point_id = "input-01", .quality = FLOW_QUALITY_GOOD},
                                     {.point_id = "input-08", .quality = FLOW_QUALITY_GOOD}};
    const bool frames[][2]        = {{false, false}, {true, false}, {true, true}};

    for (size_t tick = 0; tick < sizeof(frames) / sizeof(frames[0]); tick++)
    {
        samples[0].value               = frames[tick][0];
        samples[1].value               = frames[tick][1];
        const flow_input_frame_t input = {.samples       = samples,
                                          .sample_count  = sizeof(samples) / sizeof(samples[0]),
                                          .sampled_at_ms = 1000U + tick * 100U,
                                          .is_coherent   = true};
        assert(flow_runtime_step(&runtime, &input).code == FLOW_REASON_OK);
        const flow_tick_snapshot_t *snapshot = get_flow_runtime_snapshot(&runtime);
        assert(snapshot != NULL && snapshot->tick_number == tick + 1U);
        assert(get_node_value(snapshot, "and-main") == (frames[tick][0] && frames[tick][1]));
        assert(snapshot->output_count == 1U);
        assert(snapshot->outputs[0].value == (frames[tick][0] && frames[tick][1]));
    }
}

/* Checks memory publishes its old value and commits feedback only for the following tick. */
static void test_memory_feedback(void)
{
    flow_executable_t flow;
    assert(get_prepared_fixture("valid-memory-feedback", &flow).code == FLOW_REASON_OK);
    flow_runtime_t runtime;
    assert(flow_runtime_init(&runtime, &flow));
    const flow_input_frame_t input = {.sampled_at_ms = 1U, .is_coherent = true};
    assert(flow_runtime_step(&runtime, &input).code == FLOW_REASON_OK);
    assert(!get_node_value(get_flow_runtime_snapshot(&runtime), "memory-1"));
    assert(!get_flow_runtime_snapshot(&runtime)->outputs[0].value);
    assert(flow_runtime_step(&runtime, &input).code == FLOW_REASON_OK);
    assert(get_node_value(get_flow_runtime_snapshot(&runtime), "memory-1"));
    assert(get_flow_runtime_snapshot(&runtime)->outputs[0].value);
}

/* Checks rejected input leaves the prior snapshot, tick number, and memory state unchanged. */
static void test_failed_tick_is_atomic(void)
{
    flow_executable_t flow;
    assert(get_prepared_fixture("valid-two-button-and", &flow).code == FLOW_REASON_OK);
    flow_runtime_t runtime;
    assert(flow_runtime_init(&runtime, &flow));
    flow_input_sample_t samples[] = {{.point_id = "input-01", .value = true, .quality = FLOW_QUALITY_GOOD},
                                     {.point_id = "input-08", .value = true, .quality = FLOW_QUALITY_GOOD}};
    flow_input_frame_t input      = {.samples = samples, .sample_count = 2U, .sampled_at_ms = 1U, .is_coherent = true};
    assert(flow_runtime_step(&runtime, &input).code == FLOW_REASON_OK);
    const flow_tick_snapshot_t before = *get_flow_runtime_snapshot(&runtime);
    samples[1].quality                = FLOW_QUALITY_BAD;
    assert(flow_runtime_step(&runtime, &input).code == FLOW_REASON_INPUT_QUALITY_REJECTED);
    assert(runtime.tick_number == 1U);
    assert(memcmp(&before, get_flow_runtime_snapshot(&runtime), sizeof(before)) == 0);
    assert(runtime.evaluation_failure_count == 1U);
}

/* Runs portable decoder, validator, schedule, evaluator, and atomicity fixture tests. */
int main(void)
{
    test_validation_fixtures();
    test_two_button_ticks();
    test_memory_feedback();
    test_failed_tick_is_atomic();
    return 0;
}
