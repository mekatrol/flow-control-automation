#ifndef ALGORITHM_KAHN_SORT_H
#define ALGORITHM_KAHN_SORT_H

/*
 * Purpose: Define an allocation-free deterministic Kahn topological sorter
 * for bounded indexed graphs.
 *
 * Callers retain ownership of graph, output, and workspace storage. The module
 * deliberately has no knowledge of flow artifacts or domain-specific edge
 * semantics, allowing those policies to remain at each call site.
 */

#include <stdbool.h>
#include <stddef.h>
#include <stdint.h>

typedef struct
{
    uint16_t source;
    uint16_t target;
} kahn_edge_t;

typedef enum
{
    KAHN_SORT_OK,
    KAHN_SORT_CYCLE,
    KAHN_SORT_INVALID_ARGUMENT,
} kahn_sort_result_t;

typedef struct
{
    uint16_t *degrees;
    bool *selected;
} kahn_sort_workspace_t;

typedef bool (*kahn_is_before_fn)(uint16_t left, uint16_t right, const void *context);

/*
 * What: Produces a deterministic topological order for an indexed directed graph.
 * Why: Bounded callers need reusable cycle detection and scheduling without allocation or recursion.
 * How: Uses caller-owned Kahn workspace and resolves simultaneous ready nodes through is_before; all buffers must hold node_count
 * entries, edges must contain valid node indices, and cycle_node receives the first unselected node when a cycle is found.
 */
kahn_sort_result_t kahn_sort(uint16_t node_count, const kahn_edge_t *edges, size_t edge_count,
                             kahn_is_before_fn is_before, const void *context, uint16_t *order,
                             kahn_sort_workspace_t workspace, uint16_t *cycle_node);

#endif
