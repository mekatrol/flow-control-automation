#include <assert.h>
#include <stdio.h>
#include <string.h>

#include "controller/points.h"

static uint16_t written_outputs;
static const char TEST_SUCCESS_MESSAGE[] = "Controller point arbitration tests passed";

/* Captures effective output writes for deterministic arbitration tests. */
static bool write_outputs(uint16_t outputs)
{
    written_outputs = outputs;
    return true;
}

/* Builds one valid source-owned command for an output and priority. */
static controller_point_command_t get_command(uint8_t output, uint8_t priority, bool value, const char *source)
{
    controller_point_command_t command = {.output        = output,
                                          .command_class = 1,
                                          .priority      = priority,
                                          .value         = value,
                                          .issued_at_ms  = 10,
                                          .expires_at_ms = INT64_MIN};
    (void)snprintf(command.source_id, sizeof(command.source_id), "%s", source);
    (void)snprintf(command.correlation_id, sizeof(command.correlation_id), "correlation-%u", priority);
    return command;
}

/* Verifies priority wins independently of arrival order and relinquish removes only its source. */
static void test_arbitration_and_relinquish(void)
{
    controller_points_t points;
    assert(controller_points_init(&points, write_outputs));
    controller_points_observe(&points, 0);
    controller_point_command_t low_priority  = get_command(0, 8, false, "low");
    controller_point_command_t high_priority = get_command(0, 2, true, "high");
    assert(controller_points_command(&points, &low_priority, 10) == CONTROLLER_POINT_OK);
    assert(controller_points_command(&points, &high_priority, 10) == CONTROLLER_POINT_OK);
    assert(written_outputs == 1);
    assert(controller_points_relinquish(&points, 0, "high", 11) == CONTROLLER_POINT_OK);
    assert(written_outputs == 0);
    assert(controller_points_relinquish(&points, 0, "other", 12) == CONTROLLER_POINT_NOT_FOUND);
}

/* Verifies expiry restores the baseline and bounded subscriptions report coalescing gaps. */
static void test_expiry_and_subscription(void)
{
    controller_points_t points;
    assert(controller_points_init(&points, write_outputs));
    controller_points_observe(&points, 0);
    controller_point_command_t command = get_command(1, 1, true, "timer");
    command.expires_at_ms              = 20;
    assert(controller_points_command(&points, &command, 10) == CONTROLLER_POINT_OK);
    assert(written_outputs == 2);
    controller_points_process(&points, 20);
    assert(written_outputs == 0);
    assert(controller_points_subscribe(&points, 7, UINT16_C(0xffff)) == CONTROLLER_POINT_OK);
    controller_points_observe(&points, 1);
    controller_points_observe(&points, 3);
    uint16_t changed  = 0;
    uint16_t values   = 0;
    uint32_t sequence = 0;
    bool has_gap      = false;
    assert(controller_points_get_event(&points, 7, &changed, &values, &sequence, &has_gap) == CONTROLLER_POINT_OK);
    assert(changed == 3 && values == 3 && sequence == 2 && has_gap);
}

/* Runs bounded arbitration, expiry, relinquish, and subscription tests. */
int main(void)
{
    test_arbitration_and_relinquish();
    test_expiry_and_subscription();
    puts(TEST_SUCCESS_MESSAGE);
    return 0;
}
