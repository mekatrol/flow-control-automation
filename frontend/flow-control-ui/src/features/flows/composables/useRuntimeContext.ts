/* eslint-disable @typescript-eslint/explicit-function-return-type */
import { computed, watch, type ComputedRef } from 'vue';

import type { ExecutableFlowSource } from '@/features/flows/api/flowDebugApi';
import type { FlowDebugTarget } from '@/features/flows/debugTargets';
import type { FlowDefinition } from '@/features/flows/types';
import { WorkspaceMode } from '@/features/flows/types/flowDesigner';
import type { FlowRuntimeSnapshot } from '@/features/flows/api/flowRuntimeApi';
import { useDebugRuntimeContext } from './useDebugRuntimeContext';
import { useSimulatorRuntimeContext } from './useSimulatorRuntimeContext';

/**
 * Presents one execution context to the designer. Backend execution modes remain
 * implementation details of their respective sub-composables.
 */
export const useRuntimeContext = (options: {
  flowId: ComputedRef<string>;
  mode: ComputedRef<WorkspaceMode>;
  flow: ComputedRef<FlowDefinition | undefined>;
  revision: ComputedRef<number>;
  target: ComputedRef<FlowDebugTarget | undefined>;
  source: () => ExecutableFlowSource | undefined;
  simulatorSource: () => ExecutableFlowSource | undefined;
  deployedRuntime?: ComputedRef<FlowRuntimeSnapshot | undefined>;
}) => {
  const simulator = useSimulatorRuntimeContext(options.flow, options.simulatorSource);
  const debug = useDebugRuntimeContext(options);
  const simulatorMode = computed(() => options.mode.value === WorkspaceMode.Simulator);
  const debuggerMode = computed(() => options.mode.value === WorkspaceMode.Debugger);

  watch(options.revision, (revision, previous) => {
    if (previous !== undefined && revision !== previous) simulator.simulator.markStale();
  });

  const canvasRuntime = computed(() => {
    if (simulatorMode.value) return simulator.nodeRuntime.value ?? options.deployedRuntime?.value;
    if (debuggerMode.value) return debug.nodeRuntime.value ?? options.deployedRuntime?.value;
    return options.deployedRuntime?.value;
  });

  const stop = (keepalive = false): Promise<void[]> =>
    Promise.all([simulator.simulator.stop(keepalive), debug.stop(keepalive)]);

  return {
    simulator,
    debug,
    simulatorMode,
    debuggerMode,
    canvasRuntime,
    connectorValues: computed(() => (debuggerMode.value ? debug.connectorValues.value : undefined)),
    currentNodeId: computed(() =>
      debuggerMode.value ? debug.inspection.value?.nodeId : undefined
    ),
    breakpoints: computed(() => (debuggerMode.value ? debug.breakpoints.value : [])),
    executing: computed(() => debuggerMode.value && debug.active.value),
    io: computed(() => (simulatorMode.value ? simulator.simulator.session?.io : undefined)),
    showDefaultValues: computed(() => simulatorMode.value || debuggerMode.value),
    stop
  };
};
