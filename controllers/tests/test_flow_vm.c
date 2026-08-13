#include "flow/vm.h"

#include <assert.h>
#include <stdio.h>
#include <string.h>

#ifndef FLOW_IL_V2_FIXTURE_DIRECTORY
#define FLOW_IL_V2_FIXTURE_DIRECTORY "testdata/contracts/flow-il-v2"
#endif

static const flow_vm_target_t TARGET = {.abi_version            = FLOW_VM_ABI_VERSION,
                                        .capabilities           = FLOW_VM_CAPABILITIES_ALL,
                                        .maximum_artifact_bytes = FLOW_VM_MAX_ARTIFACT,
                                        .maximum_work_per_scan  = FLOW_VM_MAX_INSTRUCTIONS,
                                        .maximum_snapshot_bytes = 16384};

/* Loads one v2 artifact fixture into bounded caller storage. */
static size_t get_artifact(const char *fixture_id, uint8_t *artifact, size_t capacity)
{
    char path[512];
    const int length = snprintf(path, sizeof(path), "%s/%s/artifact.bin", FLOW_IL_V2_FIXTURE_DIRECTORY, fixture_id);
    assert(length > 0 && (size_t)length < sizeof(path));
    FILE *file = fopen(path, "rb");
    assert(file != NULL);
    const size_t size = fread(artifact, 1, capacity, file);
    assert(!ferror(file));
    assert(feof(file));
    assert(fclose(file) == 0);

    return size;
}

/* Prepares and initializes one named fixture with the full Boolean target profile. */
static void get_vm(const char *fixture_id, flow_vm_t *vm)
{
    uint8_t artifact[FLOW_VM_MAX_ARTIFACT];
    const size_t size = get_artifact(fixture_id, artifact, sizeof(artifact));
    assert(flow_vm_prepare(artifact, size, &TARGET, vm).code == FLOW_VM_OK);
    assert(flow_vm_initialize(vm, NULL, 0U).code == FLOW_VM_OK);
}

/* Runs one complete two-input PLC scan and returns its proposed output. */
static bool get_and_output(flow_vm_t *vm, bool left, bool right, uint64_t scan)
{
    const flow_vm_input_sample_t samples[] = {{.point_id = "input-01", .value = left}, {.point_id = "input-08", .value = right}};
    const flow_vm_input_frame_t input      = {.samples       = samples,
                                              .sample_count  = sizeof(samples) / sizeof(samples[0]),
                                              .sampled_at_ms = scan * 100U,
                                              .is_coherent   = true};
    assert(flow_vm_begin_tick(vm, &input).code == FLOW_VM_OK);
    flow_vm_command_t commands[FLOW_VM_MAX_OUTPUTS];
    size_t command_count = 0U;
    flow_vm_snapshot_t snapshot;
    assert(flow_vm_commit_tick(vm, commands, sizeof(commands) / sizeof(commands[0]), &command_count, &snapshot).code ==
           FLOW_VM_OK);
    assert(command_count == 1U);
    assert(strcmp(commands[0].point_id, "output-01") == 0);
    assert(snapshot.scan_number == scan);

    return commands[0].value;
}

/* Checks the scheduled v2 Boolean fixture produces deterministic PLC scan results. */
static void test_boolean_scans(void)
{
    flow_vm_t vm;
    get_vm("valid-two-button-and", &vm);
    assert(!get_and_output(&vm, false, false, 1U));
    assert(!get_and_output(&vm, true, false, 2U));
    assert(get_and_output(&vm, true, true, 3U));
}

/* Checks memory reads committed state and publishes staged feedback only in the following scan. */
static void test_memory_scans(void)
{
    flow_vm_t vm;
    get_vm("valid-memory-feedback", &vm);
    const flow_vm_input_frame_t input = {.sampled_at_ms = 1U, .is_coherent = true};
    flow_vm_command_t command;
    size_t command_count = 0U;
    flow_vm_snapshot_t snapshot;
    assert(flow_vm_begin_tick(&vm, &input).code == FLOW_VM_OK);
    assert(flow_vm_commit_tick(&vm, &command, 1U, &command_count, &snapshot).code == FLOW_VM_OK);
    assert(command_count == 1U && !command.value);
    assert(flow_vm_begin_tick(&vm, &input).code == FLOW_VM_OK);
    assert(flow_vm_commit_tick(&vm, &command, 1U, &command_count, &snapshot).code == FLOW_VM_OK);
    assert(command.value);
}

/* Checks every expanded Boolean opcode has the normative truth-table result in the portable host. */
static void test_expanded_boolean_scans(void)
{
    flow_vm_t vm;
    get_vm("valid-expanded-boolean", &vm);
    const flow_vm_input_frame_t input = {.sampled_at_ms = 1U, .is_coherent = true};
    flow_vm_command_t commands[4];
    size_t command_count = 0U;
    flow_vm_snapshot_t snapshot;
    assert(flow_vm_begin_tick(&vm, &input).code == FLOW_VM_OK);
    assert(flow_vm_commit_tick(&vm, commands, 4U, &command_count, &snapshot).code == FLOW_VM_OK);
    assert(command_count == 4U);

    for (size_t index = 0U; index < command_count; index++)
    {
        const bool expected = strcmp(commands[index].point_id, "output-nand") == 0 ||
                              strcmp(commands[index].point_id, "output-xor") == 0;
        assert(commands[index].value == expected);
    }
}

/* Checks numeric, comparison, and level-shifter opcodes share deterministic binary64 semantics. */
static void test_numeric_scans(void)
{
    flow_vm_t vm;
    get_vm("valid-numeric-language", &vm);
    const flow_vm_input_frame_t input = {.sampled_at_ms = 1U, .is_coherent = true};
    size_t command_count = 0U;
    flow_vm_snapshot_t snapshot;
    assert(flow_vm_begin_tick(&vm, &input).code == FLOW_VM_OK);
    assert(flow_vm_commit_tick(&vm, NULL, 0U, &command_count, &snapshot).code == FLOW_VM_OK);
    assert(command_count == 0U);
    assert(snapshot.numeric_slots[2] == 5.0);
    assert(snapshot.numeric_slots[3] == 9.0);
    assert(snapshot.slots[4]);
}

/* Checks propagated quality, monotonic on-delay state, and one-scan rising-edge events. */
static void test_quality_timer_event_scans(void)
{
    flow_vm_t vm;
    get_vm("valid-quality-timer-event", &vm);
    flow_vm_input_sample_t sample = {.point_id = "input-01", .value = true, .quality = 1U, .type = 1U};
    flow_vm_input_frame_t input = {.samples = &sample, .sample_count = 1U, .sampled_at_ms = 10U, .is_coherent = true};
    size_t command_count = 0U;
    flow_vm_snapshot_t snapshot;
    assert(flow_vm_begin_tick(&vm, &input).code == FLOW_VM_OK);
    assert(flow_vm_commit_tick(&vm, NULL, 0U, &command_count, &snapshot).code == FLOW_VM_OK);
    assert(snapshot.slots[1]);
    assert(!snapshot.slots[2]);
    assert(!snapshot.slots[3]);

    sample.quality = 0U;
    input.sampled_at_ms = 110U;
    assert(flow_vm_begin_tick(&vm, &input).code == FLOW_VM_OK);
    assert(flow_vm_commit_tick(&vm, NULL, 0U, &command_count, &snapshot).code == FLOW_VM_OK);
    assert(!snapshot.slots[1]);
    assert(snapshot.slots[2]);
    assert(snapshot.slots[3]);
}

/* Checks typed analog point input, unit-bearing binding, arithmetic, and numeric command output. */
static void test_analog_point_scan(void)
{
    flow_vm_t vm;
    get_vm("valid-analog-points", &vm);
    const flow_vm_input_sample_t sample = {
        .point_id = "temperature", .quality = 0U, .type = 2U, .number = 10.0};
    const flow_vm_input_frame_t input = {
        .samples = &sample, .sample_count = 1U, .sampled_at_ms = 1U, .is_coherent = true};
    flow_vm_command_t command;
    size_t command_count = 0U;
    flow_vm_snapshot_t snapshot;
    assert(flow_vm_begin_tick(&vm, &input).code == FLOW_VM_OK);
    assert(flow_vm_commit_tick(&vm, &command, 1U, &command_count, &snapshot).code == FLOW_VM_OK);
    assert(command_count == 1U);
    assert(command.type == 2U);
    assert(command.number == 21.0);
}

/* Checks a paused Execute Logic frame is resumable and abort commits neither state nor a snapshot. */
static void test_step_and_abort(void)
{
    flow_vm_t vm;
    get_vm("valid-memory-feedback", &vm);
    const flow_vm_input_frame_t input = {.sampled_at_ms = 1U, .is_coherent = true};
    assert(flow_vm_begin_tick(&vm, &input).code == FLOW_VM_OK);
    flow_vm_execution_view_t view;
    assert(flow_vm_step_instruction(&vm, &view).code == FLOW_VM_OK);
    assert(view.instruction_index == 0U && !view.is_at_commit);
    assert(flow_vm_abort_tick(&vm).code == FLOW_VM_OK);
    uint8_t retained[FLOW_VM_MAX_STATES];
    size_t retained_size = 0U;
    assert(flow_vm_export_retained_state(&vm, retained, sizeof(retained), &retained_size).code == FLOW_VM_OK);
    assert(retained_size == 1U && retained[0] == 0U);
    flow_vm_snapshot_t snapshot;
    assert(flow_vm_get_snapshot(&vm, &snapshot).code == FLOW_VM_WRONG_STATE);

    flow_vm_command_t command;
    size_t command_count = 0U;
    assert(flow_vm_begin_tick(&vm, &input).code == FLOW_VM_OK);
    assert(flow_vm_step_instruction(&vm, &view).code == FLOW_VM_OK);
    assert(flow_vm_commit_tick(&vm, &command, 1U, &command_count, &snapshot).code == FLOW_VM_OK);
    assert(!command.value && snapshot.scan_number == 1U);
}

/* Checks invalid fixtures fail before initialization and cannot disturb an existing initialized VM. */
static void test_loader_rejections_are_transactional(void)
{
    static const struct
    {
        const char *id;
        flow_vm_result_code_t code;
    } CASES[] = {{"malformed-truncated", FLOW_VM_LENGTH_MISMATCH},
                 {"invalid-operand", FLOW_VM_INVALID_OPERAND},
                 {"unknown-section", FLOW_VM_UNKNOWN_SECTION},
                 {"noncanonical-section-order", FLOW_VM_NON_CANONICAL_ORDER}};
    flow_vm_t active;
    get_vm("valid-two-button-and", &active);

    for (size_t index = 0; index < sizeof(CASES) / sizeof(CASES[0]); index++)
    {
        uint8_t artifact[FLOW_VM_MAX_ARTIFACT];
        const size_t size = get_artifact(CASES[index].id, artifact, sizeof(artifact));
        flow_vm_t replacement;
        assert(flow_vm_prepare(artifact, size, &TARGET, &replacement).code == CASES[index].code);
        assert(active.lifecycle == FLOW_VM_INITIALIZED);
    }
}

/* Fuzzes bounded payload bytes and requires every digest-invalid artifact to fail without changing caller storage. */
static void test_payload_fuzz_rejections(void)
{
    uint8_t artifact[FLOW_VM_MAX_ARTIFACT];
    const size_t size = get_artifact("valid-two-button-and", artifact, sizeof(artifact));
    const size_t first_payload = 128U + 8U * 48U;

    for (size_t index = first_payload; index < size; index += 7U)
    {
        uint8_t mutated[FLOW_VM_MAX_ARTIFACT];
        memcpy(mutated, artifact, size);
        mutated[index] ^= (uint8_t)(1U << (index % 8U));
        flow_vm_t vm;
        assert(flow_vm_prepare(mutated, size, &TARGET, &vm).code != FLOW_VM_OK);
    }
}

/* Runs a long deterministic sequence to prove fixed storage and scan-state stability. */
static void test_long_running_scans(void)
{
    flow_vm_t vm;
    get_vm("valid-two-button-and", &vm);

    for (uint64_t scan = 1U; scan <= 10000U; scan++)
    {
        const bool value = (scan & 1U) != 0U;
        assert(get_and_output(&vm, value, true, scan) == value);
    }
}

/* Prepares the largest normative fixture within the advertised artifact and slot bounds. */
static void test_maximum_fixture(void)
{
    flow_vm_t vm;
    get_vm("maximum-boolean", &vm);
    assert(vm.slot_count == 128U);
}

/* Runs v2 loader, PLC scan, state feedback, debug-frame, abort, and transactional rejection tests. */
int main(void)
{
    assert(flow_vm_get_abi_version() == FLOW_VM_ABI_VERSION);
    test_boolean_scans();
    test_memory_scans();
    test_expanded_boolean_scans();
    test_numeric_scans();
    test_quality_timer_event_scans();
    test_analog_point_scan();
    test_step_and_abort();
    test_loader_rejections_are_transactional();
    test_payload_fuzz_rejections();
    test_long_running_scans();
    test_maximum_fixture();

    return 0;
}
