#include "algorithm/kahn_sort.h"

/*
 * Purpose: Implement deterministic, allocation-free Kahn topological sorting
 * over bounded graphs whose nodes are represented by integer indices.
 */

#include <string.h>

/*
 * What: Produces a deterministic topological order for an indexed directed graph.
 * Why: A shared implementation keeps bounded scheduling and cycle detection independently testable.
 * How: Counts incoming edges, repeatedly selects the highest-priority ready node, and removes its outgoing edges.
 */
kahn_sort_result_t kahn_sort(uint16_t node_count, const kahn_edge_t *edges, size_t edge_count,
                             kahn_is_before_fn is_before, const void *context, uint16_t *order,
                             kahn_sort_workspace_t workspace, uint16_t *cycle_node)
{
    if (node_count == 0U)
    {
        return edge_count == 0U ? KAHN_SORT_OK : KAHN_SORT_INVALID_ARGUMENT;
    }

    if ((edge_count > 0U && edges == NULL) || is_before == NULL || order == NULL || workspace.degrees == NULL ||
        workspace.selected == NULL)
    {
        return KAHN_SORT_INVALID_ARGUMENT;
    }

    memset(workspace.degrees, 0, (size_t)node_count * sizeof(workspace.degrees[0]));
    memset(workspace.selected, 0, (size_t)node_count * sizeof(workspace.selected[0]));

    /* Validate endpoints while accumulating degrees so malformed graphs cannot index outside caller storage. */
    for (size_t edge = 0; edge < edge_count; edge++)
    {
        if (edges[edge].source >= node_count || edges[edge].target >= node_count)
        {
            return KAHN_SORT_INVALID_ARGUMENT;
        }

        if (workspace.degrees[edges[edge].target] == UINT16_MAX)
        {
            return KAHN_SORT_INVALID_ARGUMENT;
        }

        workspace.degrees[edges[edge].target]++;
    }

    for (uint16_t position = 0; position < node_count; position++)
    {
        uint16_t candidate = node_count;

        /* Caller-defined priority makes the result stable without coupling the algorithm to identifier representation. */
        for (uint16_t node = 0; node < node_count; node++)
        {
            if (!workspace.selected[node] && workspace.degrees[node] == 0U &&
                (candidate == node_count || is_before(node, candidate, context)))
            {
                candidate = node;
            }
        }

        if (candidate == node_count)
        {
            for (uint16_t node = 0; node < node_count; node++)
            {
                if (!workspace.selected[node])
                {
                    if (cycle_node != NULL)
                    {
                        *cycle_node = node;
                    }

                    return KAHN_SORT_CYCLE;
                }
            }
        }

        workspace.selected[candidate] = true;
        order[position]               = candidate;

        for (size_t edge = 0; edge < edge_count; edge++)
        {
            if (edges[edge].source == candidate)
            {
                workspace.degrees[edges[edge].target]--;
            }
        }
    }

    return KAHN_SORT_OK;
}
