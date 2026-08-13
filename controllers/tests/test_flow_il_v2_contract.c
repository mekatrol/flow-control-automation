#include "flow/sha256.h"

#include <assert.h>
#include <stdint.h>
#include <stdio.h>
#include <string.h>

#ifndef FLOW_IL_V2_FIXTURE_DIRECTORY
#error "FLOW_IL_V2_FIXTURE_DIRECTORY must identify the shared fixture directory"
#endif

enum
{
    TEST_ARTIFACT_CAPACITY       = 16384,
    TEST_ENVELOPE_LENGTH         = 128,
    TEST_DIRECTORY_ENTRY_LENGTH  = 48,
    TEST_SECTION_COUNT           = 8,
    TEST_INSTRUCTION_SECTION     = 4,
    TEST_SLOT_SECTION            = 3,
    TEST_INSTRUCTION_RECORD_SIZE = 12,
    TEST_UNUSED_INDEX            = 0xffff,
};

typedef enum
{
    TEST_FLOW_IL_OK,
    TEST_FLOW_IL_LENGTH_MISMATCH,
    TEST_FLOW_IL_UNKNOWN_SECTION,
    TEST_FLOW_IL_NON_CANONICAL_ORDER,
    TEST_FLOW_IL_INVALID_OPERAND,
    TEST_FLOW_IL_MALFORMED,
} test_flow_il_result_t;

typedef struct
{
    char flow_id[64];
    uint32_t flow_revision;
    uint32_t artifact_length;
    uint16_t section_count;
    uint32_t instruction_count;
    uint32_t slot_count;
} test_flow_il_metadata_t;

/* What: Reads a little-endian u16. Why: Fixture decoding must not depend on host alignment. How: Combines individual bytes. */
static uint16_t get_u16(const uint8_t *bytes)
{
    return (uint16_t)bytes[0] | (uint16_t)((uint16_t)bytes[1] << 8U);
}

/* What: Reads a little-endian u32. Why: Fixture decoding must be portable. How: Combines four bounded bytes. */
static uint32_t get_u32(const uint8_t *bytes)
{
    return (uint32_t)bytes[0] | ((uint32_t)bytes[1] << 8U) | ((uint32_t)bytes[2] << 16U) | ((uint32_t)bytes[3] << 24U);
}

/* What: Loads one bounded fixture artifact. Why: The C decoder and .NET decoder consume identical bytes. How: Reads the named
 * binary file into caller-owned storage and returns its exact length. */
static size_t get_fixture(const char *fixture_id, uint8_t *bytes, size_t capacity)
{
    char path[512];
    const int path_length = snprintf(path, sizeof(path), "%s/%s/artifact.bin", FLOW_IL_V2_FIXTURE_DIRECTORY, fixture_id);
    assert(path_length > 0 && (size_t)path_length < sizeof(path));
    FILE *file = fopen(path, "rb");
    assert(file != NULL);
    const size_t size = fread(bytes, 1, capacity, file);
    assert(!ferror(file));
    assert(feof(file));
    assert(fclose(file) == 0);

    return size;
}

/* What: Independently decodes bounded v2 metadata. Why: Phase 1 requires C and .NET agreement before VM implementation. How:
 * Validates the envelope, canonical directory, section ranges, and instruction slot operands without constructing runtime state.
 */
static test_flow_il_result_t get_metadata(const uint8_t *bytes, size_t size, test_flow_il_metadata_t *metadata)
{
    if (bytes == NULL || metadata == NULL || size < TEST_ENVELOPE_LENGTH || memcmp(bytes, "FIL2", 4) != 0 ||
        get_u16(&bytes[4]) != 2U || get_u16(&bytes[6]) != TEST_ENVELOPE_LENGTH)
    {
        return TEST_FLOW_IL_MALFORMED;
    }

    const uint32_t artifact_length = get_u32(&bytes[8]);

    if (artifact_length != size)
    {
        return TEST_FLOW_IL_LENGTH_MISMATCH;
    }

    const uint16_t section_count = get_u16(&bytes[26]);

    if (section_count != TEST_SECTION_COUNT || get_u32(&bytes[116]) != TEST_ENVELOPE_LENGTH ||
        TEST_ENVELOPE_LENGTH + (size_t)section_count * TEST_DIRECTORY_ENTRY_LENGTH > size)
    {
        return TEST_FLOW_IL_MALFORMED;
    }

    uint32_t expected_offset    = TEST_ENVELOPE_LENGTH + (uint32_t)section_count * TEST_DIRECTORY_ENTRY_LENGTH;
    uint32_t instruction_offset = 0U;
    uint32_t instruction_length = 0U;
    uint32_t instruction_count  = 0U;
    uint32_t slot_count         = 0U;

    for (uint16_t index = 0; index < section_count; index++)
    {
        const uint8_t *entry = &bytes[TEST_ENVELOPE_LENGTH + (size_t)index * TEST_DIRECTORY_ENTRY_LENGTH];
        const uint16_t id    = get_u16(entry);

        if (id < 1U || id > TEST_SECTION_COUNT)
        {
            return TEST_FLOW_IL_UNKNOWN_SECTION;
        }

        if (id != index + 1U)
        {
            return TEST_FLOW_IL_NON_CANONICAL_ORDER;
        }

        const uint32_t offset = get_u32(&entry[4]);
        const uint32_t length = get_u32(&entry[8]);
        const uint32_t count  = get_u32(&entry[12]);

        const uint16_t expected_version = id == 6U ? 2U : 1U;

        if (get_u16(&entry[2]) != expected_version || offset != expected_offset || length > size - offset)
        {
            return TEST_FLOW_IL_MALFORMED;
        }

        uint8_t digest[32];
        flow_sha256(&bytes[offset], length, digest);

        if (memcmp(digest, &entry[16], sizeof(digest)) != 0)
        {
            return TEST_FLOW_IL_MALFORMED;
        }

        expected_offset += length;

        if (id == TEST_SLOT_SECTION)
        {
            slot_count = count;
        }

        if (id == TEST_INSTRUCTION_SECTION)
        {
            instruction_offset = offset;
            instruction_length = length;
            instruction_count  = count;
        }
    }

    if (expected_offset != size || instruction_length != instruction_count * TEST_INSTRUCTION_RECORD_SIZE)
    {
        return TEST_FLOW_IL_MALFORMED;
    }

    for (uint32_t index = 0; index < instruction_count; index++)
    {
        const uint8_t *instruction = &bytes[instruction_offset + index * TEST_INSTRUCTION_RECORD_SIZE];
        const uint16_t result      = get_u16(&instruction[2]);
        const uint16_t operand0    = get_u16(&instruction[4]);
        const uint16_t operand1    = get_u16(&instruction[6]);

        if ((result != TEST_UNUSED_INDEX && result >= slot_count) || (operand0 != TEST_UNUSED_INDEX && operand0 >= slot_count) ||
            (operand1 != TEST_UNUSED_INDEX && operand1 >= slot_count))
        {
            return TEST_FLOW_IL_INVALID_OPERAND;
        }
    }

    const uint8_t flow_id_length = bytes[52];

    if (flow_id_length == 0U || flow_id_length >= sizeof(metadata->flow_id))
    {
        return TEST_FLOW_IL_MALFORMED;
    }

    memset(metadata, 0, sizeof(*metadata));
    memcpy(metadata->flow_id, &bytes[53], flow_id_length);
    metadata->flow_revision     = get_u32(&bytes[16]);
    metadata->artifact_length   = artifact_length;
    metadata->section_count     = section_count;
    metadata->instruction_count = instruction_count;
    metadata->slot_count        = slot_count;

    return TEST_FLOW_IL_OK;
}

/* What: Checks valid metadata and source-order determinism. Why: Independent consumers must agree on exact compiler output. How:
 * Decodes canonical fixtures and compares identity, counts, lengths, and bytes. */
static void test_valid_metadata(void)
{
    uint8_t canonical[TEST_ARTIFACT_CAPACITY];
    uint8_t permuted[TEST_ARTIFACT_CAPACITY];
    const size_t canonical_size = get_fixture("valid-two-button-and", canonical, sizeof(canonical));
    const size_t permuted_size  = get_fixture("valid-source-order-permutation", permuted, sizeof(permuted));
    test_flow_il_metadata_t metadata;
    assert(get_metadata(canonical, canonical_size, &metadata) == TEST_FLOW_IL_OK);
    assert(strcmp(metadata.flow_id, "two-button-and") == 0);
    assert(metadata.flow_revision == 7U);
    assert(metadata.artifact_length == canonical_size);
    assert(metadata.section_count == TEST_SECTION_COUNT);
    assert(metadata.instruction_count == 5U);
    assert(metadata.slot_count == 4U);
    assert(permuted_size == canonical_size);
    assert(memcmp(permuted, canonical, canonical_size) == 0);

    const size_t maximum_size = get_fixture("maximum-boolean", canonical, sizeof(canonical));
    assert(get_metadata(canonical, maximum_size, &metadata) == TEST_FLOW_IL_OK);
    assert(metadata.instruction_count == 129U);
    assert(metadata.slot_count == 128U);
}

/* What: Checks stable rejection categories for malformed fixtures. Why: C and .NET loaders must fail at the same trust boundary.
 * How: Decodes each deliberately mutated artifact and compares its expected category. */
static void test_invalid_metadata(void)
{
    static const struct
    {
        const char *id;
        test_flow_il_result_t result;
    } CASES[] = {{"malformed-truncated", TEST_FLOW_IL_LENGTH_MISMATCH},
                 {"invalid-operand", TEST_FLOW_IL_INVALID_OPERAND},
                 {"unknown-section", TEST_FLOW_IL_UNKNOWN_SECTION},
                 {"noncanonical-section-order", TEST_FLOW_IL_NON_CANONICAL_ORDER}};
    uint8_t artifact[TEST_ARTIFACT_CAPACITY];

    for (size_t index = 0; index < sizeof(CASES) / sizeof(CASES[0]); index++)
    {
        const size_t size = get_fixture(CASES[index].id, artifact, sizeof(artifact));
        test_flow_il_metadata_t metadata;
        assert(get_metadata(artifact, size, &metadata) == CASES[index].result);
    }
}

/* What: Runs the independent C metadata contract suite. Why: Phase 2 must begin from fixture agreement. How: Executes valid,
 * deterministic, maximum-count, and invalid cases. */
int main(void)
{
    test_valid_metadata();
    test_invalid_metadata();

    return 0;
}
