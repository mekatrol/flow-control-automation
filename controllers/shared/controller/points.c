#include "controller/points.h"

#include <string.h>

/* Tests one bounded source/correlation string for non-empty termination. */
static bool is_string_valid(const char *value, size_t capacity)
{
    for (size_t index = 0; index < capacity; index++)
    {
        if (value[index] == '\0')
        {
            return index > 0;
        }
    }
    return false;
}

/* Gets the effective bitmap using the numerically lowest active priority per output. */
static uint16_t get_effective_outputs(const controller_points_t *points)
{
    uint16_t outputs = points->base_outputs;
    for (uint8_t output = 0; output < CONTROLLER_POINT_OUTPUT_COUNT; output++)
    {
        const controller_point_command_t *winner = NULL;
        for (size_t index = 0; index < CONTROLLER_POINT_COMMAND_CAPACITY; index++)
        {
            const controller_point_command_t *candidate = &points->commands[index];
            if (candidate->is_used && candidate->output == output && (winner == NULL || candidate->priority < winner->priority))
            {
                winner = candidate;
            }
        }
        if (winner != NULL)
        {
            const uint16_t mask = (uint16_t)(1U << output);
            outputs             = winner->value ? (uint16_t)(outputs | mask) : (uint16_t)(outputs & ~mask);
        }
    }
    return outputs;
}

/* Applies a changed effective bitmap once and preserves the old state on write failure. */
static controller_point_result_t apply_outputs(controller_points_t *points)
{
    const uint16_t outputs = get_effective_outputs(points);
    if (points->are_outputs_valid && outputs == points->commanded_outputs)
    {
        return CONTROLLER_POINT_OK;
    }
    if (!points->write_outputs(outputs))
    {
        return CONTROLLER_POINT_FAILED;
    }
    points->commanded_outputs = outputs;
    points->are_outputs_valid = true;
    return CONTROLLER_POINT_OK;
}

/* Initializes bounded arbitration and subscription state over one output writer. */
bool controller_points_init(controller_points_t *points, bool (*write_outputs)(uint16_t outputs))
{
    if (points == NULL || write_outputs == NULL)
    {
        return false;
    }
    *points               = (controller_points_t){0};
    points->write_outputs = write_outputs;
    return true;
}

/* Submits or replaces one source-owned command and reapplies priority arbitration. */
controller_point_result_t controller_points_command(controller_points_t *points, const controller_point_command_t *command,
                                                    int64_t now_ms)
{
    if (points == NULL || command == NULL || command->output >= CONTROLLER_POINT_OUTPUT_COUNT || command->priority == 0 ||
        command->priority > 16 || !is_string_valid(command->source_id, sizeof(command->source_id)) ||
        !is_string_valid(command->correlation_id, sizeof(command->correlation_id)) || command->issued_at_ms > now_ms ||
        (command->expires_at_ms != INT64_MIN && command->expires_at_ms <= now_ms))
    {
        if (points != NULL)
        {
            points->health.command_rejection_count++;
        }
        return CONTROLLER_POINT_INVALID_ARGUMENT;
    }
    controller_point_command_t *slot = NULL;
    for (size_t index = 0; index < CONTROLLER_POINT_COMMAND_CAPACITY; index++)
    {
        controller_point_command_t *candidate = &points->commands[index];
        if (candidate->is_used && candidate->output == command->output && candidate->priority == command->priority &&
            strcmp(candidate->source_id, command->source_id) == 0)
        {
            slot = candidate;
            break;
        }
        if (!candidate->is_used && slot == NULL)
        {
            slot = candidate;
        }
    }
    if (slot == NULL)
    {
        points->health.command_rejection_count++;
        return CONTROLLER_POINT_QUEUE_FULL;
    }
    const controller_point_command_t previous = *slot;
    *slot                                     = *command;
    slot->is_used                             = true;
    const controller_point_result_t result    = apply_outputs(points);
    if (result != CONTROLLER_POINT_OK)
    {
        *slot = previous;
        points->health.command_rejection_count++;
        return result;
    }
    points->health.accepted_command_count++;
    return CONTROLLER_POINT_OK;
}

/* Relinquishes only one source's command for one output and reapplies arbitration. */
controller_point_result_t controller_points_relinquish(controller_points_t *points, uint8_t output, const char *source_id,
                                                       int64_t now_ms)
{
    if (points == NULL || output >= CONTROLLER_POINT_OUTPUT_COUNT || source_id == NULL || now_ms == INT64_MIN)
    {
        return CONTROLLER_POINT_INVALID_ARGUMENT;
    }
    bool is_removed = false;
    for (size_t index = 0; index < CONTROLLER_POINT_COMMAND_CAPACITY; index++)
    {
        controller_point_command_t *command = &points->commands[index];
        if (command->is_used && command->output == output && strcmp(command->source_id, source_id) == 0)
        {
            *command   = (controller_point_command_t){0};
            is_removed = true;
        }
    }
    if (!is_removed)
    {
        return CONTROLLER_POINT_NOT_FOUND;
    }
    const controller_point_result_t result = apply_outputs(points);
    if (result == CONTROLLER_POINT_OK)
    {
        points->health.relinquished_command_count++;
    }
    return result;
}

/* Reports whether one source currently wins arbitration using the same stable slot-order tie break as output application. */
bool controller_points_is_source_effective(const controller_points_t *points, uint8_t output, const char *source_id)
{
    if (points == NULL || output >= CONTROLLER_POINT_OUTPUT_COUNT || source_id == NULL)
    {
        return false;
    }
    const controller_point_command_t *winner = NULL;
    for (size_t index = 0; index < CONTROLLER_POINT_COMMAND_CAPACITY; index++)
    {
        const controller_point_command_t *candidate = &points->commands[index];
        if (candidate->is_used && candidate->output == output &&
            (winner == NULL || candidate->priority < winner->priority))
        {
            winner = candidate;
        }
    }
    return winner != NULL && strcmp(winner->source_id, source_id) == 0;
}

/* Expires bounded commands and reapplies outputs when effective values change. */
void controller_points_process(controller_points_t *points, int64_t now_ms)
{
    if (points == NULL)
    {
        return;
    }
    bool has_expired = false;
    for (size_t index = 0; index < CONTROLLER_POINT_COMMAND_CAPACITY; index++)
    {
        controller_point_command_t *command = &points->commands[index];
        if (command->is_used && command->expires_at_ms != INT64_MIN && now_ms >= command->expires_at_ms)
        {
            *command = (controller_point_command_t){0};
            points->health.expired_command_count++;
            has_expired = true;
        }
    }
    if (has_expired && apply_outputs(points) != CONTROLLER_POINT_OK)
    {
        points->health.command_rejection_count++;
    }
}

/* Creates or replaces one peer's bounded output-change subscription. */
controller_point_result_t controller_points_subscribe(controller_points_t *points, uint16_t peer, uint16_t point_mask)
{
    if (points == NULL || point_mask == 0)
    {
        return CONTROLLER_POINT_INVALID_ARGUMENT;
    }
    controller_point_subscription_t *slot = NULL;
    for (size_t index = 0; index < CONTROLLER_POINT_SUBSCRIPTION_CAPACITY; index++)
    {
        if (points->subscriptions[index].is_used && points->subscriptions[index].peer == peer)
        {
            slot = &points->subscriptions[index];
            break;
        }
        if (!points->subscriptions[index].is_used && slot == NULL)
        {
            slot = &points->subscriptions[index];
        }
    }
    if (slot == NULL)
    {
        points->health.subscription_drop_count++;
        return CONTROLLER_POINT_QUEUE_FULL;
    }
    *slot = (controller_point_subscription_t){.is_used = true, .peer = peer, .point_mask = point_mask};
    return CONTROLLER_POINT_OK;
}

/* Records observed logical output changes for subscribed peers. */
void controller_points_observe(controller_points_t *points, uint16_t outputs)
{
    if (points == NULL)
    {
        return;
    }
    if (!points->are_outputs_valid)
    {
        points->commanded_outputs = outputs;
        points->base_outputs      = outputs;
        points->observed_points   = outputs;
        points->are_outputs_valid = true;
        return;
    }
    const uint16_t changed  = points->observed_points ^ outputs;
    points->observed_points = outputs;
    /* Direct physical changes become the relinquish baseline only where arbitration has no active command. */
    for (uint8_t output = 0; output < CONTROLLER_POINT_OUTPUT_COUNT; output++)
    {
        bool is_commanded = false;
        for (size_t command_index = 0; command_index < CONTROLLER_POINT_COMMAND_CAPACITY; command_index++)
        {
            is_commanded |= points->commands[command_index].is_used && points->commands[command_index].output == output;
        }
        if (!is_commanded)
        {
            const uint16_t mask = (uint16_t)(1U << output);
            points->base_outputs =
                (outputs & mask) != 0U ? (uint16_t)(points->base_outputs | mask) : (uint16_t)(points->base_outputs & ~mask);
        }
    }
    for (size_t index = 0; index < CONTROLLER_POINT_SUBSCRIPTION_CAPACITY; index++)
    {
        controller_point_subscription_t *subscription = &points->subscriptions[index];
        const uint16_t relevant                       = changed & subscription->point_mask;
        if (subscription->is_used && relevant != 0)
        {
            if (subscription->pending_mask != 0)
            {
                subscription->has_gap = true;
                points->health.subscription_drop_count++;
            }
            subscription->pending_mask |= relevant;
            subscription->sequence++;
        }
    }
}

/* Gets and clears one peer's pending change event with explicit gap state. */
controller_point_result_t controller_points_get_event(controller_points_t *points, uint16_t peer, uint16_t *changed_mask,
                                                      uint16_t *values, uint32_t *sequence, bool *has_gap)
{
    if (points == NULL || changed_mask == NULL || values == NULL || sequence == NULL || has_gap == NULL)
    {
        return CONTROLLER_POINT_INVALID_ARGUMENT;
    }
    for (size_t index = 0; index < CONTROLLER_POINT_SUBSCRIPTION_CAPACITY; index++)
    {
        controller_point_subscription_t *subscription = &points->subscriptions[index];
        if (subscription->is_used && subscription->peer == peer)
        {
            if (subscription->pending_mask == 0)
            {
                return CONTROLLER_POINT_NOT_READY;
            }
            *changed_mask              = subscription->pending_mask;
            *values                    = points->observed_points;
            *sequence                  = subscription->sequence;
            *has_gap                   = subscription->has_gap;
            subscription->pending_mask = 0;
            subscription->has_gap      = false;
            return CONTROLLER_POINT_OK;
        }
    }
    return CONTROLLER_POINT_NOT_FOUND;
}

/* Gets arbitration, expiry, rejection, and subscription-drop counters. */
controller_point_health_t controller_points_get_health(const controller_points_t *points)
{
    return points != NULL ? points->health : (controller_point_health_t){0};
}
