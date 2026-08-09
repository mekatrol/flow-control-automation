#include "algorithm/kahn_sort.h"

/*
 * Purpose: Verify the reusable Kahn sorter independently of flow artifact
 * decoding and controller runtime behavior.
 */

#include <assert.h>
#include <stddef.h>
#include <stdint.h>
#include <string.h>

enum
{
    TEST_MAX_NODES = 6,
};

/* Returns whether the left node has higher priority according to caller-supplied ranks. */
static bool is_rank_before(uint16_t left, uint16_t right, const void *context)
{
    const uint16_t *ranks = context;

    return ranks[left] < ranks[right];
}

/* Runs one sort with bounded test workspace and returns its result. */
static kahn_sort_result_t get_order(uint16_t node_count, const kahn_edge_t *edges, size_t edge_count,
                                    const uint16_t ranks[TEST_MAX_NODES], uint16_t order[TEST_MAX_NODES],
                                    uint16_t *cycle_node)
{
    uint16_t degrees[TEST_MAX_NODES];
    bool selected[TEST_MAX_NODES];

    return kahn_sort(node_count, edges, edge_count, is_rank_before, ranks, order,
                     (kahn_sort_workspace_t){.degrees = degrees, .selected = selected}, cycle_node);
}

/* Checks that an empty graph succeeds without requiring output or workspace storage. */
static void test_empty_graph(void)
{
    const kahn_sort_workspace_t workspace = {0};
    assert(kahn_sort(0U, NULL, 0U, NULL, NULL, NULL, workspace, NULL) == KAHN_SORT_OK);
}

/* Checks that independent ready nodes and branches use deterministic caller priority. */
static void test_deterministic_branch_order(void)
{
    const kahn_edge_t edges[] = {{.source = 1U, .target = 3U}, {.source = 2U, .target = 3U}};
    const uint16_t ranks[TEST_MAX_NODES] = {3U, 1U, 2U, 4U};
    uint16_t order[TEST_MAX_NODES];
    const uint16_t expected[] = {1U, 2U, 0U, 3U};
    assert(get_order(4U, edges, sizeof(edges) / sizeof(edges[0]), ranks, order, NULL) == KAHN_SORT_OK);
    assert(memcmp(order, expected, sizeof(expected)) == 0);
}

/* Checks that reordering identical graph edges cannot change the deterministic schedule. */
static void test_edge_order_independence(void)
{
    const kahn_edge_t forward[] = {{.source = 0U, .target = 2U}, {.source = 1U, .target = 2U}};
    const kahn_edge_t reverse[] = {{.source = 1U, .target = 2U}, {.source = 0U, .target = 2U}};
    const uint16_t ranks[TEST_MAX_NODES] = {2U, 1U, 3U};
    uint16_t first[TEST_MAX_NODES];
    uint16_t second[TEST_MAX_NODES];
    assert(get_order(3U, forward, sizeof(forward) / sizeof(forward[0]), ranks, first, NULL) == KAHN_SORT_OK);
    assert(get_order(3U, reverse, sizeof(reverse) / sizeof(reverse[0]), ranks, second, NULL) == KAHN_SORT_OK);
    assert(memcmp(first, second, 3U * sizeof(first[0])) == 0);
}

/* Checks that a directed cycle is rejected and identifies its first unselected node. */
static void test_cycle_detection(void)
{
    const kahn_edge_t edges[] = {{.source = 0U, .target = 1U}, {.source = 1U, .target = 0U}};
    const uint16_t ranks[TEST_MAX_NODES] = {1U, 2U};
    uint16_t order[TEST_MAX_NODES];
    uint16_t cycle_node = TEST_MAX_NODES;
    assert(get_order(2U, edges, sizeof(edges) / sizeof(edges[0]), ranks, order, &cycle_node) == KAHN_SORT_CYCLE);
    assert(cycle_node == 0U);
}

/* Checks that malformed endpoints are rejected before they can access workspace out of bounds. */
static void test_invalid_endpoint(void)
{
    const kahn_edge_t edge = {.source = 0U, .target = 2U};
    const uint16_t ranks[TEST_MAX_NODES] = {1U, 2U};
    uint16_t order[TEST_MAX_NODES];
    assert(get_order(2U, &edge, 1U, ranks, order, NULL) == KAHN_SORT_INVALID_ARGUMENT);
}

/* Runs deterministic ordering, cycle, and validation tests for the reusable sorter. */
int main(void)
{
    test_empty_graph();
    test_deterministic_branch_order();
    test_edge_order_independence();
    test_cycle_detection();
    test_invalid_endpoint();

    return 0;
}
