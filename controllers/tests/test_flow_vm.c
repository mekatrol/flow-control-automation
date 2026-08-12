#include "flow/vm.h"

#include <assert.h>
#include <stdio.h>
#include <string.h>

#ifndef FLOW_IL_V2_FIXTURE_DIRECTORY
#error "FLOW_IL_V2_FIXTURE_DIRECTORY must identify the shared fixture directory"
#endif

static const flow_vm_target_t TARGET = {.abi_version            = FLOW_VM_ABI_VERSION,
                                        .capabilities           = 0x1f,
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

/* Runs v2 loader, PLC scan, state feedback, debug-frame, abort, and transactional rejection tests. */
int main(void)
{
    assert(flow_vm_get_abi_version() == FLOW_VM_ABI_VERSION);
    test_boolean_scans();
    test_memory_scans();
    test_step_and_abort();
    test_loader_rejections_are_transactional();

    return 0;
}
