#include "flow/debug.h"
#include "flow/executable.h"
#include "flow/runtime.h"

#include <assert.h>
#include <stdio.h>

/* What: Reports the schema-1 fixed-capacity resource baseline. Why: Flow IL v2 work needs a reproducible comparison point for
 * artifact, prepare, tick, and snapshot storage. How: Verifies contract capacities and prints host-ABI structure sizes. */
int main(void)
{
    assert(FLOW_EXECUTABLE_MAX_ARTIFACT_BYTES == 16384);
    assert(FLOW_EXECUTABLE_MAX_NODES == 128);
    assert(FLOW_EXECUTABLE_MAX_PORTS == 384);
    assert(FLOW_EXECUTABLE_MAX_CONNECTIONS == 384);
    assert(FLOW_EXECUTABLE_MAX_POINTS == 64);
    assert(FLOW_EXECUTABLE_MAX_OUTPUTS == 64);
    assert(FLOW_DEBUG_SNAPSHOT_CAPACITY == 16384);

    /* These labels form a stable, machine-readable report while sizeof values remain explicitly ABI-specific. */
    printf("artifact_capacity_bytes=%u\n", (unsigned int)FLOW_EXECUTABLE_MAX_ARTIFACT_BYTES);
    printf("prepare_state_bytes=%zu\n", sizeof(flow_executable_t));
    printf("tick_runtime_bytes=%zu\n", sizeof(flow_runtime_t));
    printf("tick_snapshot_bytes=%zu\n", sizeof(flow_tick_snapshot_t));
    printf("debug_snapshot_capacity_bytes=%u\n", (unsigned int)FLOW_DEBUG_SNAPSHOT_CAPACITY);
    printf("debug_session_bytes=%zu\n", sizeof(flow_debug_t));

    return 0;
}
