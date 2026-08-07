#include <assert.h>
#include <stdint.h>
#include <stdio.h>
#include <string.h>

#ifndef FLOW_CONTRACT_FIXTURE_DIRECTORY
#error "FLOW_CONTRACT_FIXTURE_DIRECTORY must identify the shared fixture directory"
#endif

enum
{
    ARTIFACT_ENVELOPE_SIZE        = 192,
    ARTIFACT_MAGIC_SIZE           = 4,
    ARTIFACT_FLOW_ID_LENGTH_INDEX = 48,
    ARTIFACT_FLOW_ID_INDEX        = 49,
    ARTIFACT_LENGTH_INDEX         = 12,
    ARTIFACT_SCHEMA_INDEX         = 4,
    ARTIFACT_BODY_SCHEMA_INDEX    = 6,
    ARTIFACT_MAXIMUM_SIZE         = 8192,
};

static const uint8_t ARTIFACT_MAGIC[ARTIFACT_MAGIC_SIZE] = {'F', 'C', 'E', 'X'};

/* Reads a little-endian u16 from a fixture without relying on host alignment or byte order. */
static uint16_t get_u16(const uint8_t *bytes)
{
    return (uint16_t)bytes[0] | (uint16_t)((uint16_t)bytes[1] << 8U);
}

/* Reads a little-endian u32 from a fixture without relying on host alignment or byte order. */
static uint32_t get_u32(const uint8_t *bytes)
{
    return (uint32_t)bytes[0] | ((uint32_t)bytes[1] << 8U) | ((uint32_t)bytes[2] << 16U) |
           ((uint32_t)bytes[3] << 24U);
}

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

/* Checks that shared binary fixtures expose stable v1 envelope fields to portable C. */
static void test_valid_fixture_envelope(void)
{
    uint8_t bytes[ARTIFACT_MAXIMUM_SIZE];
    const size_t size = get_fixture("valid-two-button-and/artifact.bin", bytes, sizeof(bytes));
    assert(size > ARTIFACT_ENVELOPE_SIZE);
    assert(memcmp(bytes, ARTIFACT_MAGIC, sizeof(ARTIFACT_MAGIC)) == 0);
    assert(get_u16(&bytes[ARTIFACT_SCHEMA_INDEX]) == 1U);
    assert(get_u16(&bytes[ARTIFACT_BODY_SCHEMA_INDEX]) == 1U);
    assert(get_u32(&bytes[ARTIFACT_LENGTH_INDEX]) == size);
    const char expected_flow_id[] = "two-button-and";
    assert(bytes[ARTIFACT_FLOW_ID_LENGTH_INDEX] == sizeof(expected_flow_id) - 1U);
    assert(memcmp(&bytes[ARTIFACT_FLOW_ID_INDEX], expected_flow_id, sizeof(expected_flow_id) - 1U) == 0);
}

/* Checks the malformed fixture freezes a length mismatch rather than ambiguous trailing data. */
static void test_truncated_fixture(void)
{
    uint8_t bytes[ARTIFACT_MAXIMUM_SIZE];
    const size_t size = get_fixture("malformed-truncated/artifact.bin", bytes, sizeof(bytes));
    assert(size > ARTIFACT_ENVELOPE_SIZE);
    assert(get_u32(&bytes[ARTIFACT_LENGTH_INDEX]) == size + 1U);
}

/* Runs the portable fixture-layout checks before decoder implementation exists. */
int main(void)
{
    test_valid_fixture_envelope();
    test_truncated_fixture();
    return 0;
}
