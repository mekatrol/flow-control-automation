#ifndef CONTROLLER_FLOW_VM_H
#define CONTROLLER_FLOW_VM_H

/*
 * Purpose: Define the version-1 portable host ABI for loading and executing
 * scheduled Flow IL v1 through an explicit PLC Scan Cycle.
 *
 * Why this contract exists: Server, emulator, host tests, and firmware need one
 * bounded implementation of opcode, state, debug-frame, and atomic-commit
 * semantics without exposing graph compilation or platform I/O to the VM.
 *
 * How callers use it: Prepare caller-owned storage from canonical IL, initialize
 * state, begin a scan with a coherent input frame, step or run instructions,
 * commit staged results, and copy bounded commands/snapshots.
 */

#include <stdbool.h>
#include <stddef.h>
#include <stdint.h>

enum
{
    FLOW_VM_ABI_VERSION      = 1,
    FLOW_VM_MAX_ARTIFACT     = 16384,
    FLOW_VM_MAX_INSTRUCTIONS = 256,
    FLOW_VM_MAX_SLOTS        = 256,
    FLOW_VM_MAX_POINTS       = 64,
    FLOW_VM_MAX_STATES       = 128,
    FLOW_VM_MAX_RETAINED_STATE_BYTES = FLOW_VM_MAX_STATES * 9,
    FLOW_VM_MAX_OUTPUTS      = 64,
    FLOW_VM_MAX_CONSTANTS    = 256,
    FLOW_VM_MAX_ID_BYTES     = 63,
    FLOW_VM_PATH_BYTES       = 95,
    FLOW_VM_CAPABILITY_BOOLEAN_SLOTS    = 1U << 0,
    FLOW_VM_CAPABILITY_POINT_READS      = 1U << 1,
    FLOW_VM_CAPABILITY_POINT_OUTPUTS    = 1U << 2,
    FLOW_VM_CAPABILITY_ONE_TICK_STATE   = 1U << 3,
    FLOW_VM_CAPABILITY_DEBUG_MAPS       = 1U << 4,
    FLOW_VM_CAPABILITY_EXPANDED_BOOLEAN = 1U << 5,
    FLOW_VM_CAPABILITY_NUMERIC           = 1U << 6,
    FLOW_VM_CAPABILITY_COMPARISON        = 1U << 7,
    FLOW_VM_CAPABILITY_LEVEL_SHIFTER     = 1U << 8,
    FLOW_VM_CAPABILITY_QUALITY           = 1U << 9,
    FLOW_VM_CAPABILITY_TIMER             = 1U << 10,
    FLOW_VM_CAPABILITY_EVENT             = 1U << 11,
    FLOW_VM_CAPABILITIES_ALL             = (1U << 12) - 1U,
};

typedef enum
{
    FLOW_VM_OK                      = 0,
    FLOW_VM_MALFORMED               = 1,
    FLOW_VM_UNSUPPORTED_VERSION     = 2,
    FLOW_VM_LENGTH_MISMATCH         = 3,
    FLOW_VM_NON_CANONICAL_ORDER     = 4,
    FLOW_VM_UNKNOWN_SECTION         = 5,
    FLOW_VM_LIMIT_EXCEEDED          = 6,
    FLOW_VM_INVALID_IDENTIFIER      = 7,
    FLOW_VM_INVALID_CONSTANT        = 8,
    FLOW_VM_INVALID_BINDING         = 9,
    FLOW_VM_INVALID_SLOT            = 10,
    FLOW_VM_UNKNOWN_OPCODE          = 11,
    FLOW_VM_INVALID_OPERAND         = 12,
    FLOW_VM_INVALID_COMMIT_PLAN     = 13,
    FLOW_VM_UNSUPPORTED_REQUIREMENT = 14,
    FLOW_VM_SNAPSHOT_TOO_LARGE      = 15,
    FLOW_VM_WRONG_STATE             = 16,
    FLOW_VM_INPUT_REJECTED          = 17,
    FLOW_VM_CAPACITY_EXCEEDED       = 18,
} flow_vm_result_code_t;

typedef enum
{
    FLOW_VM_EMPTY,
    FLOW_VM_PREPARED,
    FLOW_VM_INITIALIZED,
    FLOW_VM_EXECUTING,
} flow_vm_lifecycle_t;

typedef struct
{
    flow_vm_result_code_t code;
    char path[FLOW_VM_PATH_BYTES + 1];
} flow_vm_result_t;

typedef struct
{
    uint64_t capabilities;
    uint32_t artifact_bytes;
    uint32_t working_bytes;
    uint32_t snapshot_bytes;
    uint32_t instruction_count;
    uint32_t slot_count;
    uint32_t point_count;
    uint32_t state_count;
} flow_vm_requirements_t;

typedef struct
{
    uint32_t abi_version;
    uint64_t capabilities;
    uint32_t maximum_artifact_bytes;
    uint32_t maximum_work_per_scan;
    uint32_t maximum_snapshot_bytes;
} flow_vm_target_t;

typedef struct
{
    char point_id[FLOW_VM_MAX_ID_BYTES + 1];
    bool value;
    uint8_t quality;
    uint8_t type;
    uint8_t binding_kind;
    double number;
} flow_vm_input_sample_t;

typedef struct
{
    const flow_vm_input_sample_t *samples;
    size_t sample_count;
    uint64_t sampled_at_ms;
    bool is_coherent;
} flow_vm_input_frame_t;

typedef struct
{
    char point_id[FLOW_VM_MAX_ID_BYTES + 1];
    bool value;
    uint8_t quality;
    uint8_t type;
    uint8_t binding_kind;
    double number;
} flow_vm_command_t;

typedef struct
{
    char flow_id[FLOW_VM_MAX_ID_BYTES + 1];
    uint32_t flow_revision;
    uint64_t scan_number;
    uint64_t sampled_at_ms;
    uint16_t slot_count;
    uint16_t output_count;
    bool slots[FLOW_VM_MAX_SLOTS];
    flow_vm_command_t outputs[FLOW_VM_MAX_OUTPUTS];
    uint8_t slot_types[FLOW_VM_MAX_SLOTS];
    uint8_t slot_qualities[FLOW_VM_MAX_SLOTS];
    double numeric_slots[FLOW_VM_MAX_SLOTS];
} flow_vm_snapshot_t;

typedef struct
{
    uint8_t opcode;
    uint16_t result;
    uint16_t operand0;
    uint16_t operand1;
    uint16_t auxiliary;
} flow_vm_instruction_t;

typedef struct
{
    char id[FLOW_VM_MAX_ID_BYTES + 1];
    uint8_t direction;
    uint8_t type;
    uint8_t binding_kind;
} flow_vm_point_t;

typedef struct
{
    uint16_t instruction_index;
    uint8_t opcode;
    bool is_at_commit;
} flow_vm_execution_view_t;

typedef struct
{
    flow_vm_execution_view_t execution;
    uint16_t slot_count;
    uint16_t state_count;
    uint16_t output_count;
    bool slots[FLOW_VM_MAX_SLOTS];
    bool current_state[FLOW_VM_MAX_STATES];
    bool staged_state[FLOW_VM_MAX_STATES];
    double current_numeric_state[FLOW_VM_MAX_STATES];
    double staged_numeric_state[FLOW_VM_MAX_STATES];
    bool staged_state_valid[FLOW_VM_MAX_STATES];
    flow_vm_command_t outputs[FLOW_VM_MAX_OUTPUTS];
    uint8_t slot_types[FLOW_VM_MAX_SLOTS];
    uint8_t slot_qualities[FLOW_VM_MAX_SLOTS];
    double numeric_slots[FLOW_VM_MAX_SLOTS];
} flow_vm_debug_frame_t;

typedef struct
{
    flow_vm_lifecycle_t lifecycle;
    char flow_id[FLOW_VM_MAX_ID_BYTES + 1];
    uint32_t flow_revision;
    uint16_t instruction_count;
    uint16_t slot_count;
    uint16_t point_count;
    uint16_t state_count;
    uint16_t state_slot_base;
    uint16_t output_count;
    uint16_t instruction_pointer;
    uint8_t quality_policy;
    uint64_t scan_number;
    uint64_t sampled_at_ms;
    uint16_t constant_count;
    bool constants[FLOW_VM_MAX_CONSTANTS];
    double numeric_constants[FLOW_VM_MAX_CONSTANTS];
    uint8_t constant_types[FLOW_VM_MAX_CONSTANTS];
    bool initial_state[FLOW_VM_MAX_STATES];
    bool current_state[FLOW_VM_MAX_STATES];
    bool staged_state[FLOW_VM_MAX_STATES];
    double initial_numeric_state[FLOW_VM_MAX_STATES];
    double current_numeric_state[FLOW_VM_MAX_STATES];
    double staged_numeric_state[FLOW_VM_MAX_STATES];
    uint8_t state_types[FLOW_VM_MAX_STATES];
    uint8_t state_kinds[FLOW_VM_MAX_STATES];
    uint64_t timer_durations_ms[FLOW_VM_MAX_STATES];
    uint64_t timer_started_at_ms[FLOW_VM_MAX_STATES];
    uint64_t staged_timer_started_at_ms[FLOW_VM_MAX_STATES];
    bool working_slots[FLOW_VM_MAX_SLOTS];
    double numeric_slots[FLOW_VM_MAX_SLOTS];
    uint8_t slot_types[FLOW_VM_MAX_SLOTS];
    uint8_t slot_qualities[FLOW_VM_MAX_SLOTS];
    bool staged_state_valid[FLOW_VM_MAX_STATES];
    flow_vm_point_t points[FLOW_VM_MAX_POINTS];
    flow_vm_instruction_t instructions[FLOW_VM_MAX_INSTRUCTIONS];
    flow_vm_input_sample_t captured_inputs[FLOW_VM_MAX_POINTS];
    size_t captured_input_count;
    flow_vm_command_t staged_outputs[FLOW_VM_MAX_OUTPUTS];
    flow_vm_snapshot_t snapshot;
} flow_vm_t;

/* Returns the exact native ABI version implemented by this library. */
uint32_t flow_vm_get_abi_version(void);

/* Returns the opaque instance-storage size required by this exact ABI. */
size_t flow_vm_get_instance_size(void);

/* Validates Flow IL metadata and reports bounded storage/work requirements. */
flow_vm_result_t flow_vm_get_requirements(const uint8_t *artifact, size_t artifact_size, flow_vm_requirements_t *requirements);

/* Validates and prepares one VM in caller-owned storage without activating state. */
flow_vm_result_t flow_vm_prepare(const uint8_t *artifact, size_t artifact_size, const flow_vm_target_t *target, flow_vm_t *vm);

/* Initializes prepared state from artifact defaults; retained state is reserved for a later profile. */
flow_vm_result_t flow_vm_initialize(flow_vm_t *vm, const uint8_t *retained_state, size_t retained_state_size);

/* Performs Read Inputs and opens a private resumable Execute Logic frame. */
flow_vm_result_t flow_vm_begin_tick(flow_vm_t *vm, const flow_vm_input_frame_t *input);

/* Executes one scheduled instruction without committing staged state or commands. */
flow_vm_result_t flow_vm_step_instruction(flow_vm_t *vm, flow_vm_execution_view_t *view);

/* Copies the bounded private execution frame without publishing any staged value. */
flow_vm_result_t flow_vm_get_debug_frame(const flow_vm_t *vm, flow_vm_debug_frame_t *frame);

/* Runs remaining instructions, then performs the atomic Write Outputs phase. */
flow_vm_result_t flow_vm_commit_tick(flow_vm_t *vm, flow_vm_command_t *commands, size_t command_capacity, size_t *command_count,
                                     flow_vm_snapshot_t *snapshot);

/* Discards one uncommitted scan frame and restores initialized lifecycle. */
flow_vm_result_t flow_vm_abort_tick(flow_vm_t *vm);

/* Restores artifact-defined state and clears completed scan history. */
flow_vm_result_t flow_vm_reset(flow_vm_t *vm);

/* Exports committed Boolean state as canonical bytes for a retained-state host adapter. */
flow_vm_result_t flow_vm_export_retained_state(const flow_vm_t *vm, uint8_t *output, size_t capacity, size_t *written);

/* Copies the last completed immutable scan snapshot. */
flow_vm_result_t flow_vm_get_snapshot(const flow_vm_t *vm, flow_vm_snapshot_t *snapshot);

/* Idempotently clears all VM-owned contents from caller storage. */
flow_vm_result_t flow_vm_clear(flow_vm_t *vm);

#endif
