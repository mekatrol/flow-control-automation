#pragma once

#include <stdbool.h>
#include <stddef.h>
#include <stdint.h>

/* Arbitration and subscription bounds keep point-control state deterministic. */
enum
{
    CONTROLLER_POINT_OUTPUT_COUNT          = 16,
    CONTROLLER_POINT_COMMAND_CAPACITY      = 64,
    CONTROLLER_POINT_SOURCE_ID_CAPACITY    = 33,
    CONTROLLER_POINT_CORRELATION_CAPACITY  = 33,
    CONTROLLER_POINT_SUBSCRIPTION_CAPACITY = 4,
};

typedef enum
{
    CONTROLLER_POINT_OK,
    CONTROLLER_POINT_INVALID_ARGUMENT,
    CONTROLLER_POINT_NOT_FOUND,
    CONTROLLER_POINT_QUEUE_FULL,
    CONTROLLER_POINT_NOT_READY,
    CONTROLLER_POINT_FAILED,
} controller_point_result_t;

typedef struct
{
    bool is_used;
    uint8_t output;
    char source_id[CONTROLLER_POINT_SOURCE_ID_CAPACITY];
    char correlation_id[CONTROLLER_POINT_CORRELATION_CAPACITY];
    uint8_t command_class;
    uint8_t priority;
    bool value;
    int64_t issued_at_ms;
    int64_t expires_at_ms;
} controller_point_command_t;

typedef struct
{
    bool is_used;
    uint16_t peer;
    uint16_t point_mask;
    uint16_t pending_mask;
    uint32_t sequence;
    bool has_gap;
} controller_point_subscription_t;

typedef struct
{
    uint32_t accepted_command_count;
    uint32_t relinquished_command_count;
    uint32_t expired_command_count;
    uint32_t command_rejection_count;
    uint32_t subscription_drop_count;
} controller_point_health_t;

typedef struct
{
    controller_point_command_t commands[CONTROLLER_POINT_COMMAND_CAPACITY];
    controller_point_subscription_t subscriptions[CONTROLLER_POINT_SUBSCRIPTION_CAPACITY];
    bool (*write_outputs)(uint16_t outputs);
    uint16_t commanded_outputs;
    uint16_t base_outputs;
    bool are_outputs_valid;
    uint16_t observed_points;
    controller_point_health_t health;
} controller_points_t;

/* Initializes bounded arbitration and subscription state over one output writer. */
bool controller_points_init(controller_points_t *points, bool (*write_outputs)(uint16_t outputs));

/* Submits or replaces one source-owned command and reapplies priority arbitration. */
controller_point_result_t controller_points_command(controller_points_t *points, const controller_point_command_t *command,
                                                    int64_t now_ms);

/* Relinquishes only one source's command for one output and reapplies arbitration. */
controller_point_result_t controller_points_relinquish(controller_points_t *points, uint8_t output, const char *source_id,
                                                       int64_t now_ms);

/* Reports whether one source currently wins arbitration for an output without changing point state. */
bool controller_points_is_source_effective(const controller_points_t *points, uint8_t output, const char *source_id);

/* Expires bounded commands and reapplies outputs when effective values change. */
void controller_points_process(controller_points_t *points, int64_t now_ms);

/* Creates or replaces one peer's bounded output-change subscription. */
controller_point_result_t controller_points_subscribe(controller_points_t *points, uint16_t peer, uint16_t point_mask);

/* Records observed logical output changes for subscribed peers. */
void controller_points_observe(controller_points_t *points, uint16_t outputs);

/* Gets and clears one peer's pending change event with explicit gap state. */
controller_point_result_t controller_points_get_event(controller_points_t *points, uint16_t peer, uint16_t *changed_mask,
                                                      uint16_t *values, uint32_t *sequence, bool *has_gap);

/* Gets arbitration, expiry, rejection, and subscription-drop counters. */
controller_point_health_t controller_points_get_health(const controller_points_t *points);
