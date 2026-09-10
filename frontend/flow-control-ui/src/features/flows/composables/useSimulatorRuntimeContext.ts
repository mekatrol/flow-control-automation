/* eslint-disable @typescript-eslint/explicit-function-return-type */
import { computed, type ComputedRef } from 'vue';

import type { ExecutableFlowSource } from '@/features/flows/api/flowDebugApi';
import type { EmulatorInputChange } from '@/features/flows/api/flowEmulatorApi';
import { createExecutionNodeRuntime } from '@/features/flows/executionNodeRuntime';
import { useFlowSimulatorStore } from '@/features/flows/stores/flowSimulator';
import type { FlowDefinition } from '@/features/flows/types';

export const useSimulatorRuntimeContext = (
  flow: ComputedRef<FlowDefinition | undefined>,
  source: () => ExecutableFlowSource | undefined
) => {
  const simulator = useFlowSimulatorStore();
  const nodeRuntime = computed(() => {
    const session = simulator.session;
    const currentFlow = flow.value;
    if (!session || !currentFlow || simulator.lifecycle === 'stale') return undefined;
    return createExecutionNodeRuntime({
      flow: currentFlow,
      flowId: session.flowId,
      snapshot: session.snapshot,
      inspection: session.inspection,
      io: session.io,
      state: session.lifecycleState === 'faulted' ? 'error' : 'running'
    });
  });

  const start = async (): Promise<void> => {
    const executable = source();
    if (!executable) {
      simulator.reportFailure(new Error('The flow or execution target is unavailable.'));
      return;
    }
    try {
      await simulator.start(executable);
      if (simulator.lifecycle === 'ready') await simulator.run();
    } catch (error) {
      simulator.reportFailure(error);
    }
  };

  const applyInputs = async (inputs: EmulatorInputChange[]): Promise<void> => {
    const wasRunning = simulator.lifecycle === 'running';
    if (wasRunning) await simulator.pause();
    await simulator.applyInputsAndStep(inputs);
    if (wasRunning && simulator.lifecycle === 'paused') await simulator.run();
  };

  return { simulator, nodeRuntime, start, applyInputs };
};
