#include "flow/vm.h"

#include "flow/sha256.h"

#include <math.h>
#include <stdio.h>
#include <string.h>

enum
{
    ENVELOPE_BYTES           = 128,
    DIRECTORY_ENTRY_BYTES    = 48,
    SECTION_COUNT            = 8,
    SLOT_RECORD_BYTES        = 8,
    INSTRUCTION_RECORD_BYTES = 12,
    UNUSED_INDEX             = 0xffff,
    OPCODE_READ_POINT        = 1,
    OPCODE_CONSTANT          = 2,
    OPCODE_NOT               = 3,
    OPCODE_AND               = 4,
    OPCODE_OR                = 5,
    OPCODE_LOAD_STATE        = 6,
    OPCODE_PROPOSE_OUTPUT    = 7,
    OPCODE_STAGE_STATE       = 8,
    OPCODE_NAND              = 9,
    OPCODE_NOR               = 10,
    OPCODE_XOR               = 11,
    OPCODE_XNOR              = 12,
    OPCODE_NUMERIC_CONSTANT  = 13,
    OPCODE_ADD               = 14,
    OPCODE_COMPARE           = 15,
    OPCODE_LEVEL_SHIFTER     = 16,
    OPCODE_QUALITY_GOOD      = 17,
    OPCODE_ON_DELAY          = 18,
    OPCODE_RISING_EDGE       = 19,
    OPCODE_COMMIT            = 255,
};

typedef struct
{
    uint32_t offset;
    uint32_t length;
    uint32_t count;
} section_t;

typedef struct
{
    section_t sections[SECTION_COUNT];
    uint32_t artifact_length;
    uint32_t revision;
    uint32_t working_bytes;
    uint32_t snapshot_bytes;
    uint32_t maximum_work;
    uint64_t capabilities;
    uint8_t quality_policy;
    char flow_id[FLOW_VM_MAX_ID_BYTES + 1];
} metadata_t;

/* Reads one packed little-endian u16 without alignment assumptions. */
static uint16_t get_u16(const uint8_t *bytes)
{
    return (uint16_t)bytes[0] | (uint16_t)((uint16_t)bytes[1] << 8U);
}

/* Reads one packed little-endian u32 without alignment assumptions. */
static uint32_t get_u32(const uint8_t *bytes)
{
    return (uint32_t)bytes[0] | ((uint32_t)bytes[1] << 8U) | ((uint32_t)bytes[2] << 16U) | ((uint32_t)bytes[3] << 24U);
}

/* Reads one packed little-endian u64 without alignment assumptions. */
static uint64_t get_u64(const uint8_t *bytes)
{
    return (uint64_t)get_u32(bytes) | ((uint64_t)get_u32(&bytes[4]) << 32U);
}

/* Reads one canonical IEEE-754 binary64 value without alignment assumptions. */
static double get_f64(const uint8_t *bytes)
{
    const uint64_t bits = get_u64(bytes);
    double value;
    memcpy(&value, &bits, sizeof(value));

    return value;
}

/* Constructs a bounded stable result path for host correlation. */
static flow_vm_result_t get_result(flow_vm_result_code_t code, const char *path)
{
    flow_vm_result_t result = {.code = code};

    if (path != NULL)
    {
        snprintf(result.path, sizeof(result.path), "%s", path);
    }

    return result;
}

size_t flow_vm_get_instance_size(void)
{
    return sizeof(flow_vm_t);
}

/* Decodes and validates the canonical envelope, directory, and per-section integrity before any section is trusted. */
static flow_vm_result_t get_metadata(const uint8_t *artifact, size_t size, metadata_t *metadata)
{
    if (artifact == NULL || metadata == NULL || size < ENVELOPE_BYTES || size > FLOW_VM_MAX_ARTIFACT ||
        memcmp(artifact, "FIL2", 4) != 0)
    {
        return get_result(FLOW_VM_MALFORMED, "");
    }

    if (get_u16(&artifact[4]) != 2U || get_u16(&artifact[6]) != ENVELOPE_BYTES)
    {
        return get_result(FLOW_VM_UNSUPPORTED_VERSION, "/version");
    }

    if (get_u32(&artifact[8]) != size)
    {
        return get_result(FLOW_VM_LENGTH_MISMATCH, "/artifactLength");
    }

    if (get_u16(&artifact[24]) != FLOW_VM_ABI_VERSION || get_u16(&artifact[26]) != SECTION_COUNT ||
        get_u32(&artifact[116]) != ENVELOPE_BYTES || ENVELOPE_BYTES + SECTION_COUNT * DIRECTORY_ENTRY_BYTES > size)
    {
        return get_result(FLOW_VM_MALFORMED, "/sections");
    }

    memset(metadata, 0, sizeof(*metadata));
    uint32_t expected_offset = ENVELOPE_BYTES + SECTION_COUNT * DIRECTORY_ENTRY_BYTES;

    for (uint16_t index = 0; index < SECTION_COUNT; index++)
    {
        const uint8_t *entry = &artifact[ENVELOPE_BYTES + (size_t)index * DIRECTORY_ENTRY_BYTES];
        const uint16_t id    = get_u16(entry);

        if (id < 1U || id > SECTION_COUNT)
        {
            return get_result(FLOW_VM_UNKNOWN_SECTION, "/sections/0/id");
        }

        if (id != index + 1U)
        {
            return get_result(FLOW_VM_NON_CANONICAL_ORDER, "/sections/0/id");
        }

        section_t *section = &metadata->sections[index];
        section->offset    = get_u32(&entry[4]);
        section->length    = get_u32(&entry[8]);
        section->count     = get_u32(&entry[12]);

        const uint16_t expected_version = id == 6U ? 2U : 1U;

        if (get_u16(&entry[2]) != expected_version || section->offset != expected_offset || section->offset > size ||
            section->length > size - section->offset)
        {
            return get_result(FLOW_VM_MALFORMED, "/sections");
        }

        uint8_t digest[32];
        flow_sha256(&artifact[section->offset], section->length, digest);

        if (memcmp(digest, &entry[16], sizeof(digest)) != 0)
        {
            return get_result(FLOW_VM_MALFORMED, "/sections/digest");
        }

        expected_offset += section->length;
    }

    if (expected_offset != size)
    {
        return get_result(FLOW_VM_LENGTH_MISMATCH, "/artifactLength");
    }

    const uint8_t id_length = artifact[52];

    if (id_length == 0U || id_length > FLOW_VM_MAX_ID_BYTES)
    {
        return get_result(FLOW_VM_INVALID_IDENTIFIER, "/flowId");
    }

    memcpy(metadata->flow_id, &artifact[53], id_length);
    metadata->artifact_length = get_u32(&artifact[8]);
    metadata->revision        = get_u32(&artifact[16]);
    metadata->maximum_work    = get_u32(&artifact[32]);
    metadata->capabilities    = get_u64(&artifact[36]);
    metadata->quality_policy  = artifact[28];
    metadata->working_bytes   = get_u32(&artifact[44]);
    metadata->snapshot_bytes  = get_u32(&artifact[48]);

    if ((metadata->capabilities & ~((uint64_t)FLOW_VM_CAPABILITIES_ALL)) != 0U ||
        (metadata->quality_policy != 1U && metadata->quality_policy != 2U))
    {
        return get_result(FLOW_VM_UNSUPPORTED_REQUIREMENT, "/requiredCapabilities");
    }

    return get_result(FLOW_VM_OK, "");
}

/* Finds one captured good-quality Boolean point sample by stable binding ID. */
static const flow_vm_input_sample_t *get_input(const flow_vm_t *vm, const char *point_id)
{
    for (size_t index = 0; index < vm->captured_input_count; index++)
    {
        if (strcmp(vm->captured_inputs[index].point_id, point_id) == 0)
        {
            return &vm->captured_inputs[index];
        }
    }

    return NULL;
}

/* Returns the native ABI version without consulting mutable runtime state. */
uint32_t flow_vm_get_abi_version(void)
{
    return FLOW_VM_ABI_VERSION;
}

/* Validates metadata and reports exact declared resource counts for host admission. */
flow_vm_result_t flow_vm_get_requirements(const uint8_t *artifact, size_t artifact_size, flow_vm_requirements_t *requirements)
{
    metadata_t metadata;
    flow_vm_result_t result = get_metadata(artifact, artifact_size, &metadata);

    if (result.code != FLOW_VM_OK || requirements == NULL)
    {
        return requirements == NULL ? get_result(FLOW_VM_MALFORMED, "/requirements") : result;
    }

    *requirements = (flow_vm_requirements_t){.capabilities      = metadata.capabilities,
                                             .artifact_bytes    = metadata.artifact_length,
                                             .working_bytes     = metadata.working_bytes,
                                             .snapshot_bytes    = metadata.snapshot_bytes,
                                             .instruction_count = metadata.sections[3].count,
                                             .slot_count        = metadata.sections[2].count,
                                             .point_count       = metadata.sections[1].count};

    /* State slot kind is decoded during prepare; count it here without retaining untrusted pointers. */
    const section_t slots = metadata.sections[2];

    if (slots.length != slots.count * SLOT_RECORD_BYTES)
    {
        return get_result(FLOW_VM_INVALID_SLOT, "/slots");
    }

    for (uint32_t index = 0; index < slots.count; index++)
    {
        if (artifact[slots.offset + index * SLOT_RECORD_BYTES] >= 3U &&
            artifact[slots.offset + index * SLOT_RECORD_BYTES] <= 5U)
        {
            requirements->state_count++;
        }
    }

    return get_result(FLOW_VM_OK, "");
}

/* Prepares bounded constants, points, slots, and scheduled instructions after full target admission checks. */
flow_vm_result_t flow_vm_prepare(const uint8_t *artifact, size_t artifact_size, const flow_vm_target_t *target, flow_vm_t *vm)
{
    flow_vm_requirements_t requirements;
    flow_vm_result_t result = flow_vm_get_requirements(artifact, artifact_size, &requirements);

    if (result.code != FLOW_VM_OK || target == NULL || vm == NULL)
    {
        return result.code != FLOW_VM_OK ? result : get_result(FLOW_VM_MALFORMED, "/target");
    }

    if (target->abi_version != FLOW_VM_ABI_VERSION || requirements.artifact_bytes > target->maximum_artifact_bytes ||
        requirements.instruction_count > target->maximum_work_per_scan ||
        requirements.snapshot_bytes > target->maximum_snapshot_bytes ||
        (requirements.capabilities & ~target->capabilities) != 0U || requirements.instruction_count > FLOW_VM_MAX_INSTRUCTIONS ||
        requirements.slot_count > FLOW_VM_MAX_SLOTS || requirements.point_count > FLOW_VM_MAX_POINTS ||
        requirements.state_count > FLOW_VM_MAX_STATES)
    {
        return get_result(FLOW_VM_LIMIT_EXCEEDED, "/requirements");
    }

    metadata_t metadata;
    result = get_metadata(artifact, artifact_size, &metadata);
    memset(vm, 0, sizeof(*vm));
    snprintf(vm->flow_id, sizeof(vm->flow_id), "%s", metadata.flow_id);
    vm->flow_revision     = metadata.revision;
    vm->slot_count        = (uint16_t)requirements.slot_count;
    vm->instruction_count = (uint16_t)requirements.instruction_count;
    vm->point_count       = (uint16_t)requirements.point_count;
    vm->state_count       = (uint16_t)requirements.state_count;
    vm->quality_policy    = metadata.quality_policy;
    vm->state_slot_base   = vm->slot_count - vm->state_count;

    const section_t constants = metadata.sections[0];

    if (constants.count > FLOW_VM_MAX_CONSTANTS)
    {
        return get_result(FLOW_VM_INVALID_CONSTANT, "/constants");
    }

    size_t constant_offset = constants.offset;

    for (uint32_t index = 0; index < constants.count; index++)
    {
        if (constant_offset + 4U > constants.offset + constants.length)
        {
            return get_result(FLOW_VM_INVALID_CONSTANT, "/constants");
        }

        const uint8_t *record = &artifact[constant_offset];

        if (record[0] == 1U && record[1] <= 1U && get_u16(&record[2]) == 0U)
        {
            vm->constants[index]      = record[1] != 0U;
            vm->constant_types[index] = 1U;
            constant_offset += 4U;
        }
        else if (record[0] == 2U && record[1] == 0U && get_u16(&record[2]) == 0U &&
                 constant_offset + 12U <= constants.offset + constants.length)
        {
            const double number = get_f64(&record[4]);

            if (!isfinite(number))
            {
                return get_result(FLOW_VM_INVALID_CONSTANT, "/constants");
            }

            vm->numeric_constants[index] = number;
            vm->constant_types[index]     = 2U;
            constant_offset += 12U;
        }
        else
        {
            return get_result(FLOW_VM_INVALID_CONSTANT, "/constants");
        }
    }

    if (constant_offset != constants.offset + constants.length)
    {
        return get_result(FLOW_VM_INVALID_CONSTANT, "/constants");
    }

    vm->constant_count = (uint16_t)constants.count;

    const section_t points = metadata.sections[1];
    size_t point_offset    = points.offset;

    for (uint32_t index = 0; index < points.count; index++)
    {
        if (point_offset + 5U > points.offset + points.length)
        {
            return get_result(FLOW_VM_INVALID_BINDING, "/points");
        }

        const uint8_t id_length = artifact[point_offset + 4U];

        if (id_length == 0U || id_length > FLOW_VM_MAX_ID_BYTES || point_offset + 5U + id_length > points.offset + points.length)
        {
            return get_result(FLOW_VM_INVALID_BINDING, "/points");
        }

        vm->points[index].direction = artifact[point_offset];
        vm->points[index].type      = artifact[point_offset + 1U];

        if (vm->points[index].type != 1U && vm->points[index].type != 2U)
        {
            return get_result(FLOW_VM_INVALID_BINDING, "/points");
        }

        memcpy(vm->points[index].id, &artifact[point_offset + 5U], id_length);
        point_offset += 5U + id_length;
    }

    if (point_offset != points.offset + points.length)
    {
        return get_result(FLOW_VM_INVALID_BINDING, "/points");
    }

    const section_t slots = metadata.sections[2];
    uint16_t state_index  = 0U;

    for (uint32_t index = 0; index < slots.count; index++)
    {
        const uint8_t *record = &artifact[slots.offset + index * SLOT_RECORD_BYTES];

        if (get_u16(&record[4]) != index || (record[1] != 1U && record[1] != 2U))
        {
            return get_result(FLOW_VM_INVALID_SLOT, "/slots");
        }

        vm->slot_types[index] = record[1];

        if (record[0] == 3U || record[0] == 4U || record[0] == 5U)
        {
            const uint16_t constant = get_u16(&record[6]);

            if (constant >= constants.count || state_index >= vm->state_count ||
                (record[0] != 4U && vm->constant_types[constant] != record[1]))
            {
                return get_result(FLOW_VM_INVALID_SLOT, "/slots");
            }

            vm->state_kinds[state_index] = record[0];

            if (record[0] == 4U)
            {
                if (vm->constant_types[constant] != 2U || vm->numeric_constants[constant] < 0.0)
                {
                    return get_result(FLOW_VM_INVALID_SLOT, "/slots");
                }

                vm->timer_durations_ms[state_index] = (uint64_t)vm->numeric_constants[constant];
            }
            else
            {
                vm->initial_state[state_index] = vm->constants[constant];
            }

            state_index++;
        }
    }

    const section_t instructions = metadata.sections[3];

    if (instructions.length != instructions.count * INSTRUCTION_RECORD_BYTES)
    {
        return get_result(FLOW_VM_MALFORMED, "/instructions");
    }

    for (uint32_t index = 0; index < instructions.count; index++)
    {
        const uint8_t *record              = &artifact[instructions.offset + index * INSTRUCTION_RECORD_BYTES];
        flow_vm_instruction_t *instruction = &vm->instructions[index];
        instruction->opcode                = record[0];
        instruction->result                = get_u16(&record[2]);
        instruction->operand0              = get_u16(&record[4]);
        instruction->operand1              = get_u16(&record[6]);
        instruction->auxiliary             = get_u16(&record[8]);

        if (record[1] != 0U || get_u16(&record[10]) != 0U || instruction->opcode == 0U ||
            (instruction->opcode > OPCODE_RISING_EDGE && instruction->opcode != OPCODE_COMMIT))
        {
            return get_result(FLOW_VM_UNKNOWN_OPCODE, "/instructions");
        }

        if ((instruction->result != UNUSED_INDEX && instruction->result >= vm->slot_count) ||
            (instruction->operand0 != UNUSED_INDEX && instruction->operand0 >= vm->slot_count) ||
            (instruction->operand1 != UNUSED_INDEX && instruction->opcode != OPCODE_LEVEL_SHIFTER &&
             instruction->operand1 >= vm->slot_count))
        {
            return get_result(FLOW_VM_INVALID_OPERAND, "/instructions/0/resultSlot");
        }

        if (instruction->opcode == OPCODE_PROPOSE_OUTPUT)
        {
            vm->output_count++;
        }
    }

    if (vm->instruction_count == 0U || vm->instructions[vm->instruction_count - 1U].opcode != OPCODE_COMMIT ||
        vm->output_count > FLOW_VM_MAX_OUTPUTS)
    {
        return get_result(FLOW_VM_INVALID_COMMIT_PLAN, "/instructions");
    }

    vm->lifecycle = FLOW_VM_PREPARED;

    return get_result(FLOW_VM_OK, "");
}

/* Initializes current state from canonical constants and rejects unsupported retained-state input. */
flow_vm_result_t flow_vm_initialize(flow_vm_t *vm, const uint8_t *retained_state, size_t retained_state_size)
{
    if (vm == NULL || vm->lifecycle != FLOW_VM_PREPARED || retained_state != NULL || retained_state_size != 0U)
    {
        return get_result(FLOW_VM_WRONG_STATE, "/lifecycle");
    }

    memcpy(vm->current_state, vm->initial_state, sizeof(vm->current_state));
    vm->lifecycle = FLOW_VM_INITIALIZED;

    return get_result(FLOW_VM_OK, "");
}

/* Captures one coherent input image and opens private Execute Logic storage. */
flow_vm_result_t flow_vm_begin_tick(flow_vm_t *vm, const flow_vm_input_frame_t *input)
{
    if (vm == NULL || input == NULL || vm->lifecycle != FLOW_VM_INITIALIZED)
    {
        return get_result(FLOW_VM_WRONG_STATE, "/lifecycle");
    }

    if (!input->is_coherent || input->sample_count > FLOW_VM_MAX_POINTS || (input->sample_count > 0U && input->samples == NULL))
    {
        return get_result(FLOW_VM_INPUT_REJECTED, "/inputs");
    }

    for (size_t index = 0; index < input->sample_count; index++)
    {
        if (vm->quality_policy == 1U && input->samples[index].quality != 0U)
        {
            return get_result(FLOW_VM_INPUT_REJECTED, "/inputs");
        }
    }

    memset(vm->working_slots, 0, sizeof(vm->working_slots));
    memset(vm->numeric_slots, 0, sizeof(vm->numeric_slots));
    memset(vm->slot_qualities, 0, sizeof(vm->slot_qualities));
    memset(vm->staged_state_valid, 0, sizeof(vm->staged_state_valid));
    memset(vm->staged_outputs, 0, sizeof(vm->staged_outputs));
    memcpy(vm->staged_state, vm->current_state, sizeof(vm->staged_state));
    memcpy(vm->staged_timer_started_at_ms, vm->timer_started_at_ms, sizeof(vm->staged_timer_started_at_ms));
    memcpy(vm->captured_inputs, input->samples, input->sample_count * sizeof(input->samples[0]));
    vm->captured_input_count = input->sample_count;
    vm->sampled_at_ms        = input->sampled_at_ms;
    vm->instruction_pointer  = 0U;
    vm->lifecycle            = FLOW_VM_EXECUTING;

    return get_result(FLOW_VM_OK, "");
}

/* Executes exactly one instruction into the private scan frame and never publishes committed state. */
flow_vm_result_t flow_vm_step_instruction(flow_vm_t *vm, flow_vm_execution_view_t *view)
{
    if (vm == NULL || vm->lifecycle != FLOW_VM_EXECUTING || vm->instruction_pointer >= vm->instruction_count)
    {
        return get_result(FLOW_VM_WRONG_STATE, "/lifecycle");
    }

    const flow_vm_instruction_t *instruction = &vm->instructions[vm->instruction_pointer];

    if (view != NULL)
    {
        *view = (flow_vm_execution_view_t){.instruction_index = vm->instruction_pointer,
                                           .opcode            = instruction->opcode,
                                           .is_at_commit      = instruction->opcode == OPCODE_COMMIT};
    }

    switch (instruction->opcode)
    {
        case OPCODE_READ_POINT: {
            if (instruction->auxiliary >= vm->point_count)
            {
                return get_result(FLOW_VM_INVALID_OPERAND, "/instructions/point");
            }

            const flow_vm_input_sample_t *sample = get_input(vm, vm->points[instruction->auxiliary].id);

            if (sample == NULL)
            {
                return get_result(FLOW_VM_INPUT_REJECTED, "/inputs");
            }

            vm->working_slots[instruction->result] = sample->value;
            vm->numeric_slots[instruction->result] = sample->number;
            vm->slot_qualities[instruction->result] = sample->quality;
            break;
        }

        case OPCODE_CONSTANT:

            if (instruction->auxiliary >= vm->constant_count || vm->constant_types[instruction->auxiliary] != 1U)
            {
                return get_result(FLOW_VM_INVALID_OPERAND, "/instructions/constant");
            }

            vm->working_slots[instruction->result] = vm->constants[instruction->auxiliary];
            break;
        case OPCODE_NOT:
            vm->working_slots[instruction->result] = !vm->working_slots[instruction->operand0];
            vm->slot_qualities[instruction->result] = vm->slot_qualities[instruction->operand0];
            break;
        case OPCODE_AND:
            vm->working_slots[instruction->result] =
                vm->working_slots[instruction->operand0] && vm->working_slots[instruction->operand1];
            vm->slot_qualities[instruction->result] =
                vm->slot_qualities[instruction->operand0] > vm->slot_qualities[instruction->operand1]
                    ? vm->slot_qualities[instruction->operand0]
                    : vm->slot_qualities[instruction->operand1];
            break;
        case OPCODE_OR:
            vm->working_slots[instruction->result] =
                vm->working_slots[instruction->operand0] || vm->working_slots[instruction->operand1];
            vm->slot_qualities[instruction->result] =
                vm->slot_qualities[instruction->operand0] > vm->slot_qualities[instruction->operand1]
                    ? vm->slot_qualities[instruction->operand0]
                    : vm->slot_qualities[instruction->operand1];
            break;
        case OPCODE_NAND:
            vm->working_slots[instruction->result] =
                !(vm->working_slots[instruction->operand0] && vm->working_slots[instruction->operand1]);
            vm->slot_qualities[instruction->result] =
                vm->slot_qualities[instruction->operand0] > vm->slot_qualities[instruction->operand1]
                    ? vm->slot_qualities[instruction->operand0]
                    : vm->slot_qualities[instruction->operand1];
            break;
        case OPCODE_NOR:
            vm->working_slots[instruction->result] =
                !(vm->working_slots[instruction->operand0] || vm->working_slots[instruction->operand1]);
            vm->slot_qualities[instruction->result] =
                vm->slot_qualities[instruction->operand0] > vm->slot_qualities[instruction->operand1]
                    ? vm->slot_qualities[instruction->operand0]
                    : vm->slot_qualities[instruction->operand1];
            break;
        case OPCODE_XOR:
            vm->working_slots[instruction->result] =
                vm->working_slots[instruction->operand0] != vm->working_slots[instruction->operand1];
            vm->slot_qualities[instruction->result] =
                vm->slot_qualities[instruction->operand0] > vm->slot_qualities[instruction->operand1]
                    ? vm->slot_qualities[instruction->operand0]
                    : vm->slot_qualities[instruction->operand1];
            break;
        case OPCODE_XNOR:
            vm->working_slots[instruction->result] =
                vm->working_slots[instruction->operand0] == vm->working_slots[instruction->operand1];
            vm->slot_qualities[instruction->result] =
                vm->slot_qualities[instruction->operand0] > vm->slot_qualities[instruction->operand1]
                    ? vm->slot_qualities[instruction->operand0]
                    : vm->slot_qualities[instruction->operand1];
            break;
        case OPCODE_NUMERIC_CONSTANT:

            if (instruction->auxiliary >= vm->constant_count || vm->constant_types[instruction->auxiliary] != 2U)
            {
                return get_result(FLOW_VM_INVALID_OPERAND, "/instructions/constant");
            }

            vm->numeric_slots[instruction->result] = vm->numeric_constants[instruction->auxiliary];
            break;
        case OPCODE_ADD: {
            const double value = vm->numeric_slots[instruction->operand0] + vm->numeric_slots[instruction->operand1];

            if (!isfinite(value))
            {
                return get_result(FLOW_VM_INPUT_REJECTED, "/arithmeticOverflow");
            }

            vm->numeric_slots[instruction->result] = value;
            vm->slot_qualities[instruction->result] =
                vm->slot_qualities[instruction->operand0] > vm->slot_qualities[instruction->operand1]
                    ? vm->slot_qualities[instruction->operand0]
                    : vm->slot_qualities[instruction->operand1];
            break;
        }
        case OPCODE_COMPARE: {
            const double left  = vm->numeric_slots[instruction->operand0];
            const double right = vm->numeric_slots[instruction->operand1];
            bool value;

            switch (instruction->auxiliary)
            {
                case 1U: value = left < right; break;
                case 2U: value = left <= right; break;
                case 3U: value = left == right; break;
                case 4U: value = left >= right; break;
                case 5U: value = left > right; break;
                case 6U: value = left != right; break;
                default: return get_result(FLOW_VM_INVALID_OPERAND, "/instructions/comparison");
            }

            vm->working_slots[instruction->result] = value;
            vm->slot_qualities[instruction->result] =
                vm->slot_qualities[instruction->operand0] > vm->slot_qualities[instruction->operand1]
                    ? vm->slot_qualities[instruction->operand0]
                    : vm->slot_qualities[instruction->operand1];
            break;
        }
        case OPCODE_LEVEL_SHIFTER: {
            if (instruction->operand1 >= vm->constant_count || instruction->auxiliary >= vm->constant_count ||
                vm->constant_types[instruction->operand1] != 2U || vm->constant_types[instruction->auxiliary] != 2U)
            {
                return get_result(FLOW_VM_INVALID_OPERAND, "/instructions/levelShifter");
            }

            const double value = vm->numeric_slots[instruction->operand0] * vm->numeric_constants[instruction->operand1] +
                                 vm->numeric_constants[instruction->auxiliary];

            if (!isfinite(value))
            {
                return get_result(FLOW_VM_INPUT_REJECTED, "/arithmeticOverflow");
            }

            vm->numeric_slots[instruction->result] = value;
            vm->slot_qualities[instruction->result] = vm->slot_qualities[instruction->operand0];
            break;
        }
        case OPCODE_QUALITY_GOOD:
            vm->working_slots[instruction->result] = vm->slot_qualities[instruction->operand0] == 0U;
            vm->slot_qualities[instruction->result] = 0U;
            break;
        case OPCODE_ON_DELAY: {
            if (instruction->auxiliary < vm->state_slot_base || instruction->auxiliary - vm->state_slot_base >= vm->state_count)
            {
                return get_result(FLOW_VM_INVALID_OPERAND, "/instructions/timer");
            }

            const uint16_t state = instruction->auxiliary - vm->state_slot_base;

            if (!vm->working_slots[instruction->operand0])
            {
                vm->working_slots[instruction->result] = false;
                vm->staged_timer_started_at_ms[state] = 0U;
            }
            else
            {
                if (vm->timer_started_at_ms[state] == 0U)
                {
                    vm->staged_timer_started_at_ms[state] = vm->sampled_at_ms == 0U ? 1U : vm->sampled_at_ms;
                }

                const uint64_t started = vm->timer_started_at_ms[state] == 0U
                                             ? vm->staged_timer_started_at_ms[state]
                                             : vm->timer_started_at_ms[state];
                vm->working_slots[instruction->result] =
                    vm->sampled_at_ms >= started && vm->sampled_at_ms - started >= vm->timer_durations_ms[state];
            }

            vm->staged_state_valid[state] = true;
            break;
        }
        case OPCODE_RISING_EDGE: {
            if (instruction->auxiliary < vm->state_slot_base || instruction->auxiliary - vm->state_slot_base >= vm->state_count)
            {
                return get_result(FLOW_VM_INVALID_OPERAND, "/instructions/event");
            }

            const uint16_t state = instruction->auxiliary - vm->state_slot_base;
            vm->working_slots[instruction->result] =
                vm->working_slots[instruction->operand0] && !vm->current_state[state];
            vm->staged_state[state] = vm->working_slots[instruction->operand0];
            vm->staged_state_valid[state] = true;
            break;
        }
        case OPCODE_LOAD_STATE:

            if (instruction->auxiliary < vm->state_slot_base || instruction->auxiliary - vm->state_slot_base >= vm->state_count)
            {
                return get_result(FLOW_VM_INVALID_OPERAND, "/instructions/state");
            }

            vm->working_slots[instruction->result] = vm->current_state[instruction->auxiliary - vm->state_slot_base];
            break;
        case OPCODE_PROPOSE_OUTPUT:

            if (instruction->auxiliary >= vm->point_count)
            {
                return get_result(FLOW_VM_INVALID_OPERAND, "/instructions/point");
            }

            vm->working_slots[instruction->result] = vm->working_slots[instruction->operand0];
            vm->numeric_slots[instruction->result] = vm->numeric_slots[instruction->operand0];
            vm->slot_qualities[instruction->result] = vm->slot_qualities[instruction->operand0];
            break;
        case OPCODE_STAGE_STATE:

            if (instruction->auxiliary < vm->state_slot_base || instruction->auxiliary - vm->state_slot_base >= vm->state_count)
            {
                return get_result(FLOW_VM_INVALID_OPERAND, "/instructions/state");
            }

            vm->staged_state[instruction->auxiliary - vm->state_slot_base]       = vm->working_slots[instruction->operand0];
            vm->staged_state_valid[instruction->auxiliary - vm->state_slot_base] = true;
            break;
        case OPCODE_COMMIT:
            break;
        default:

            return get_result(FLOW_VM_UNKNOWN_OPCODE, "/instructions");
    }

    vm->instruction_pointer++;

    return get_result(FLOW_VM_OK, "");
}

/* Copies a paused execution frame so hosts can inspect it without weakening atomic commit. */
flow_vm_result_t flow_vm_get_debug_frame(const flow_vm_t *vm, flow_vm_debug_frame_t *frame)
{
    size_t index;

    if ((vm == NULL) || (frame == NULL))
    {
        return get_result(FLOW_VM_MALFORMED, "/debugFrame");
    }

    if (vm->lifecycle != FLOW_VM_EXECUTING)
    {
        return get_result(FLOW_VM_WRONG_STATE, "/lifecycle");
    }

    memset(frame, 0, sizeof(*frame));
    frame->execution.instruction_index = vm->instruction_pointer;
    frame->execution.is_at_commit      = vm->instruction_pointer == vm->instruction_count;
    frame->execution.opcode = frame->execution.is_at_commit ? UINT8_MAX : vm->instructions[vm->instruction_pointer].opcode;
    frame->slot_count       = vm->slot_count;
    frame->state_count      = vm->state_count;
    frame->output_count     = vm->output_count;
    memcpy(frame->slots, vm->working_slots, vm->slot_count * sizeof(frame->slots[0]));
    memcpy(frame->numeric_slots, vm->numeric_slots, vm->slot_count * sizeof(frame->numeric_slots[0]));
    memcpy(frame->slot_types, vm->slot_types, vm->slot_count * sizeof(frame->slot_types[0]));
    memcpy(frame->slot_qualities, vm->slot_qualities, vm->slot_count * sizeof(frame->slot_qualities[0]));
    memcpy(frame->current_state, vm->current_state, vm->state_count * sizeof(frame->current_state[0]));
    memcpy(frame->staged_state, vm->staged_state, vm->state_count * sizeof(frame->staged_state[0]));
    memcpy(frame->staged_state_valid, vm->staged_state_valid, vm->state_count * sizeof(frame->staged_state_valid[0]));

    /* Copy only proposed commands produced by instructions already executed in this frame. */
    for (index = 0; index < vm->output_count; ++index)
    {
        frame->outputs[index] = vm->staged_outputs[index];
    }

    return get_result(FLOW_VM_OK, "");
}

/* Completes Execute Logic and atomically publishes state, proposed commands, and one completed-scan snapshot. */
flow_vm_result_t flow_vm_commit_tick(flow_vm_t *vm, flow_vm_command_t *commands, size_t command_capacity, size_t *command_count,
                                     flow_vm_snapshot_t *snapshot)
{
    if (vm == NULL || command_count == NULL || snapshot == NULL || vm->lifecycle != FLOW_VM_EXECUTING ||
        command_capacity < vm->output_count || (vm->output_count > 0U && commands == NULL))
    {
        return get_result(FLOW_VM_CAPACITY_EXCEEDED, "/outputs");
    }

    while (vm->instruction_pointer < vm->instruction_count)
    {
        flow_vm_result_t result = flow_vm_step_instruction(vm, NULL);

        if (result.code != FLOW_VM_OK)
        {
            return result;
        }
    }

    size_t output_index = 0U;

    for (uint16_t index = 0; index < vm->instruction_count; index++)
    {
        const flow_vm_instruction_t *instruction = &vm->instructions[index];

        if (instruction->opcode == OPCODE_PROPOSE_OUTPUT)
        {
            flow_vm_command_t *command = &vm->staged_outputs[output_index++];
            snprintf(command->point_id, sizeof(command->point_id), "%s", vm->points[instruction->auxiliary].id);
            command->value = vm->working_slots[instruction->result];
            command->number = vm->numeric_slots[instruction->result];
            command->quality = vm->slot_qualities[instruction->result];
            command->type = vm->slot_types[instruction->result];
        }
    }

    /* This group is the sole Write Outputs publication boundary for one PLC scan. */
    memcpy(vm->current_state, vm->staged_state, sizeof(vm->current_state));
    memcpy(vm->timer_started_at_ms, vm->staged_timer_started_at_ms, sizeof(vm->timer_started_at_ms));
    vm->scan_number++;
    vm->snapshot = (flow_vm_snapshot_t){.flow_revision = vm->flow_revision,
                                        .scan_number   = vm->scan_number,
                                        .sampled_at_ms = vm->sampled_at_ms,
                                        .slot_count    = vm->slot_count,
                                        .output_count  = vm->output_count};
    snprintf(vm->snapshot.flow_id, sizeof(vm->snapshot.flow_id), "%s", vm->flow_id);
    memcpy(vm->snapshot.slots, vm->working_slots, sizeof(vm->snapshot.slots));
    memcpy(vm->snapshot.numeric_slots, vm->numeric_slots, sizeof(vm->snapshot.numeric_slots));
    memcpy(vm->snapshot.slot_types, vm->slot_types, sizeof(vm->snapshot.slot_types));
    memcpy(vm->snapshot.slot_qualities, vm->slot_qualities, sizeof(vm->snapshot.slot_qualities));
    memcpy(vm->snapshot.outputs, vm->staged_outputs, sizeof(vm->snapshot.outputs));
    memcpy(commands, vm->staged_outputs, vm->output_count * sizeof(commands[0]));

    *command_count = vm->output_count;
    *snapshot      = vm->snapshot;
    vm->lifecycle  = FLOW_VM_INITIALIZED;

    return get_result(FLOW_VM_OK, "");
}

/* Aborts Execute Logic and discards every staged value without changing committed state or snapshot. */
flow_vm_result_t flow_vm_abort_tick(flow_vm_t *vm)
{
    if (vm == NULL || vm->lifecycle != FLOW_VM_EXECUTING)
    {
        return get_result(FLOW_VM_WRONG_STATE, "/lifecycle");
    }

    memset(vm->working_slots, 0, sizeof(vm->working_slots));
    memset(vm->numeric_slots, 0, sizeof(vm->numeric_slots));
    memset(vm->staged_outputs, 0, sizeof(vm->staged_outputs));
    vm->captured_input_count = 0U;
    vm->instruction_pointer  = 0U;
    vm->lifecycle            = FLOW_VM_INITIALIZED;

    return get_result(FLOW_VM_OK, "");
}

/* Resets initialized state to artifact defaults and clears scan/snapshot history. */
flow_vm_result_t flow_vm_reset(flow_vm_t *vm)
{
    if (vm == NULL || vm->lifecycle != FLOW_VM_INITIALIZED)
    {
        return get_result(FLOW_VM_WRONG_STATE, "/lifecycle");
    }

    memcpy(vm->current_state, vm->initial_state, sizeof(vm->current_state));
    memset(&vm->snapshot, 0, sizeof(vm->snapshot));
    vm->scan_number = 0U;

    return get_result(FLOW_VM_OK, "");
}

/* Exports only committed state; an in-progress scan never leaks its staged next-state image. */
flow_vm_result_t flow_vm_export_retained_state(const flow_vm_t *vm, uint8_t *output, size_t capacity, size_t *written)
{
    if (vm == NULL || written == NULL || vm->lifecycle == FLOW_VM_EMPTY || capacity < vm->state_count ||
        (vm->state_count > 0U && output == NULL))
    {
        return get_result(FLOW_VM_CAPACITY_EXCEEDED, "/retainedState");
    }

    for (uint16_t index = 0; index < vm->state_count; index++)
    {
        output[index] = vm->current_state[index] ? 1U : 0U;
    }

    *written = vm->state_count;

    return get_result(FLOW_VM_OK, "");
}

/* Copies the last completed immutable scan snapshot without exposing mutable VM storage. */
flow_vm_result_t flow_vm_get_snapshot(const flow_vm_t *vm, flow_vm_snapshot_t *snapshot)
{
    if (vm == NULL || snapshot == NULL || vm->lifecycle == FLOW_VM_EMPTY || vm->scan_number == 0U)
    {
        return get_result(FLOW_VM_WRONG_STATE, "/snapshot");
    }

    *snapshot = vm->snapshot;

    return get_result(FLOW_VM_OK, "");
}

/* Idempotently clears one caller-owned VM instance and all volatile debug-frame contents. */
flow_vm_result_t flow_vm_clear(flow_vm_t *vm)
{
    if (vm == NULL)
    {
        return get_result(FLOW_VM_MALFORMED, "/instance");
    }

    memset(vm, 0, sizeof(*vm));

    return get_result(FLOW_VM_OK, "");
}
