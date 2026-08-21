#include "flow/virtual_points.h"

#include <assert.h>
#include <stdio.h>
#include <string.h>

static const char TEST_INSTANCE[] = "controller-east";
static const char TEST_SUCCESS_MESSAGE[] = "Flow virtual-point tests passed";

/* Creates a clean protocol-v1 instance-global store for each independent test. */
static flow_virtual_point_store_t get_store(void)
{
    flow_virtual_point_store_t store;
    assert(flow_virtual_points_init(&store, TEST_INSTANCE, 1U));

    return store;
}

/* Creates one retained analog writer contract with a deterministic default. */
static flow_virtual_point_declaration_t get_temperature(bool is_writer)
{
    flow_virtual_point_declaration_t declaration = {.type = FLOW_VIRTUAL_POINT_ANALOG,
                                                     .persistence = FLOW_VIRTUAL_POINT_RETAINED,
                                                     .is_writer = is_writer,
                                                     .has_default = true,
                                                     .analog_default = 20.0};
    snprintf(declaration.key, sizeof(declaration.key), "%s", "temperature");

    return declaration;
}

/* Proves readers share committed next-scan state while writer conflicts and wrong instance identity are rejected. */
static void test_shared_value_and_ownership(void)
{
    flow_virtual_point_store_t store = get_store();
    const flow_virtual_point_declaration_t writer = get_temperature(true);
    const flow_virtual_point_declaration_t reader = get_temperature(false);
    assert(flow_virtual_points_activate(&store, TEST_INSTANCE, "writer-a", &writer, 1U) == FLOW_VIRTUAL_POINT_OK);
    assert(flow_virtual_points_activate(&store, TEST_INSTANCE, "reader-b", &reader, 1U) == FLOW_VIRTUAL_POINT_OK);
    assert(flow_virtual_points_activate(&store, TEST_INSTANCE, "writer-c", &writer, 1U) ==
           FLOW_VIRTUAL_POINT_WRITER_CONFLICT);
    const flow_virtual_point_command_t command = {
        .key = "temperature", .type = FLOW_VIRTUAL_POINT_ANALOG, .analog_value = 21.5};
    assert(flow_virtual_points_commit(&store, "controller-west", "writer-a", &command, 1U, 10U) ==
           FLOW_VIRTUAL_POINT_INSTANCE_MISMATCH);
    assert(flow_virtual_points_commit(&store, TEST_INSTANCE, "writer-a", &command, 1U, 10U) == FLOW_VIRTUAL_POINT_OK);
    const char *keys[] = {"temperature"};
    flow_virtual_point_snapshot_t snapshot;
    assert(flow_virtual_points_snapshot(&store, keys, 1U, &snapshot) == FLOW_VIRTUAL_POINT_OK);
    assert(snapshot.is_initialized && snapshot.analog_value == 21.5 && snapshot.version == 1U);
    assert(flow_virtual_points_deactivate(&store, TEST_INSTANCE, "writer-a") == FLOW_VIRTUAL_POINT_OK);
    assert(flow_virtual_points_activate(&store, TEST_INSTANCE, "writer-c", &writer, 1U) == FLOW_VIRTUAL_POINT_OK);
}

/* Proves a failed multi-point commit changes neither point and a valid batch advances one transaction generation. */
static void test_atomic_batch(void)
{
    flow_virtual_point_store_t store = get_store();
    flow_virtual_point_declaration_t declarations[2] = {get_temperature(true),
                                                        {.key = "enabled",
                                                         .type = FLOW_VIRTUAL_POINT_DIGITAL,
                                                         .persistence = FLOW_VIRTUAL_POINT_VOLATILE,
                                                         .is_writer = true}};
    assert(flow_virtual_points_activate(&store, TEST_INSTANCE, "writer", declarations, 2U) == FLOW_VIRTUAL_POINT_OK);
    flow_virtual_point_command_t commands[2] = {{.key = "temperature",
                                                 .type = FLOW_VIRTUAL_POINT_ANALOG,
                                                 .analog_value = 22.0},
                                                {.key = "enabled",
                                                 .type = FLOW_VIRTUAL_POINT_ANALOG,
                                                 .analog_value = 1.0}};
    assert(flow_virtual_points_commit(&store, TEST_INSTANCE, "writer", commands, 2U, 20U) ==
           FLOW_VIRTUAL_POINT_CONTRACT_CONFLICT);
    const char *keys[] = {"temperature", "enabled"};
    flow_virtual_point_snapshot_t snapshots[2];
    assert(flow_virtual_points_snapshot(&store, keys, 2U, snapshots) == FLOW_VIRTUAL_POINT_OK);
    assert(snapshots[0].analog_value == 20.0 && snapshots[0].version == 0U && !snapshots[1].is_initialized);
    commands[1].type = FLOW_VIRTUAL_POINT_DIGITAL;
    commands[1].digital_value = true;
    assert(flow_virtual_points_commit(&store, TEST_INSTANCE, "writer", commands, 2U, 20U) == FLOW_VIRTUAL_POINT_OK);
    assert(store.generation == 1U);
}

/* Proves retained state round-trips by key and type while volatile state is excluded. */
static void test_typed_retained_round_trip(void)
{
    flow_virtual_point_store_t source = get_store();
    const flow_virtual_point_declaration_t declaration = get_temperature(true);
    assert(flow_virtual_points_activate(&source, TEST_INSTANCE, "writer", &declaration, 1U) == FLOW_VIRTUAL_POINT_OK);
    const flow_virtual_point_command_t command = {
        .key = "temperature", .type = FLOW_VIRTUAL_POINT_ANALOG, .analog_value = 19.25};
    assert(flow_virtual_points_commit(&source, TEST_INSTANCE, "writer", &command, 1U, 50U) == FLOW_VIRTUAL_POINT_OK);
    uint8_t image[4096];
    size_t image_size = 0;
    assert(flow_virtual_points_export_retained(&source, image, sizeof(image), &image_size) == FLOW_VIRTUAL_POINT_OK);
    flow_virtual_point_store_t restored = get_store();
    assert(flow_virtual_points_activate(&restored, TEST_INSTANCE, "writer", &declaration, 1U) == FLOW_VIRTUAL_POINT_OK);
    assert(flow_virtual_points_restore_retained(&restored, image, image_size) == FLOW_VIRTUAL_POINT_OK);
    const char *keys[] = {"temperature"};
    flow_virtual_point_snapshot_t snapshot;
    assert(flow_virtual_points_snapshot(&restored, keys, 1U, &snapshot) == FLOW_VIRTUAL_POINT_OK);
    assert(snapshot.analog_value == 19.25 && snapshot.timestamp_ms == 50U && snapshot.version == 1U);
    image[FLOW_VIRTUAL_POINT_ID_CAPACITY + 8U] = FLOW_VIRTUAL_POINT_DIGITAL;
    assert(flow_virtual_points_restore_retained(&restored, image, image_size) ==
           FLOW_VIRTUAL_POINT_RETAINED_INCOMPATIBLE);
}

/* Runs instance isolation, transaction, ownership, and retained-image coverage. */
int main(void)
{
    test_shared_value_and_ownership();
    test_atomic_batch();
    test_typed_retained_round_trip();
    puts(TEST_SUCCESS_MESSAGE);

    return 0;
}
