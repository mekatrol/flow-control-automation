import { computed, ref } from 'vue';
import { defineStore } from 'pinia';

import { FlowApiError } from '@/features/flows/api/flowApi';
import type { ExecutableFlowSource } from '@/features/flows/api/flowDebugApi';
import {
  flowSimulatorApi,
  type SimulatorLifecycle,
  type SimulatorSession
} from '@/features/flows/api/flowSimulatorApi';
import type { EmulatorInputChange } from '@/features/flows/api/flowEmulatorApi';

export const useFlowSimulatorStore = defineStore('flow-simulator', () => {
  const lifecycle = ref<SimulatorLifecycle>('idle');
  const session = ref<SimulatorSession>();
  const error = ref<string>();
  const requestGeneration = ref(0);
  let controller: AbortController | undefined;
  let pollTimer: ReturnType<typeof window.setInterval> | undefined;

  const busy = computed(() => lifecycle.value === 'compiling');
  const stopPolling = (): void => {
    if (pollTimer !== undefined) window.clearInterval(pollTimer);
    pollTimer = undefined;
  };
  const begin = (): { generation: number; signal: AbortSignal } => {
    controller?.abort();
    controller = new AbortController();
    requestGeneration.value += 1;
    return { generation: requestGeneration.value, signal: controller.signal };
  };
  const current = (generation: number): boolean => generation === requestGeneration.value;
  const failure = (value: unknown): void => {
    if (value instanceof FlowApiError && value.kind === 'cancelled') return;
    lifecycle.value = 'faulted';
    error.value = value instanceof Error ? value.message : 'Simulator operation failed.';
  };
  const apply = (value: SimulatorSession): void => {
    session.value = value;
    lifecycle.value = value.lifecycleState;
    error.value = undefined;
  };

  const start = async (source: ExecutableFlowSource): Promise<void> => {
    const request = begin();
    stopPolling();
    lifecycle.value = 'compiling';
    error.value = undefined;
    try {
      const result = await flowSimulatorApi.start(source, request.signal);
      if (!current(request.generation)) return;
      if (result.flowId !== source.id || result.sourceRevision !== source.revision)
        throw new Error('The simulator returned a session for another draft revision.');
      apply(result);
    } catch (value) {
      if (current(request.generation)) failure(value);
    }
  };

  const operate = async (
    operation: (
      flowId: string,
      sessionId: string,
      signal?: AbortSignal
    ) => Promise<SimulatorSession>
  ): Promise<void> => {
    const active = session.value;
    if (!active || lifecycle.value === 'stale') return;
    const request = begin();
    try {
      const result = await operation(active.flowId, active.sessionId, request.signal);
      if (current(request.generation)) apply(result);
    } catch (value) {
      if (current(request.generation)) failure(value);
    }
  };

  const stepTick = (): Promise<void> => operate(flowSimulatorApi.stepTick);
  const applyInputsAndStep = (inputs: EmulatorInputChange[]): Promise<void> =>
    operate((flowId, sessionId, signal) =>
      flowSimulatorApi.applyInputsAndStep(flowId, sessionId, inputs, signal)
    );
  const advance = (milliseconds: number): Promise<void> =>
    operate((flowId, sessionId, signal) =>
      flowSimulatorApi.advance(flowId, sessionId, milliseconds, signal)
    );
  const fault = (value: string | null): Promise<void> =>
    operate((flowId, sessionId, signal) =>
      flowSimulatorApi.fault(flowId, sessionId, value, signal)
    );
  const resetIo = (powerCycle: boolean): Promise<void> =>
    operate((flowId, sessionId, signal) =>
      flowSimulatorApi.resetIo(flowId, sessionId, powerCycle, signal)
    );
  const resetInputs = (): Promise<void> => operate(flowSimulatorApi.resetInputs);
  const stepNode = (): Promise<void> => operate(flowSimulatorApi.stepNode);
  const stepInstruction = (): Promise<void> => operate(flowSimulatorApi.stepInstruction);
  const restart = (): Promise<void> => operate(flowSimulatorApi.restart);
  const pause = async (): Promise<void> => {
    stopPolling();
    await operate(flowSimulatorApi.pause);
  };
  const run = async (): Promise<void> => {
    await operate(flowSimulatorApi.run);
    if (lifecycle.value !== 'running' || !session.value) return;
    stopPolling();
    pollTimer = window.setInterval(() => {
      const active = session.value;
      if (!active || lifecycle.value !== 'running') return;
      void operate(flowSimulatorApi.get);
    }, 250);
  };
  const markStale = (): void => {
    if (!session.value || ['idle', 'stopped', 'faulted'].includes(lifecycle.value)) return;
    stopPolling();
    controller?.abort();
    requestGeneration.value += 1;
    lifecycle.value = 'stale';
  };
  const stop = async (keepalive = false): Promise<void> => {
    stopPolling();
    controller?.abort();
    requestGeneration.value += 1;
    const active = session.value;
    session.value = undefined;
    lifecycle.value = 'stopped';
    error.value = undefined;
    if (!active) return;
    try {
      await flowSimulatorApi.stop(active.flowId, active.sessionId, keepalive);
    } catch (value) {
      if (!keepalive) failure(value);
    }
  };
  const reset = (): void => {
    stopPolling();
    controller?.abort();
    requestGeneration.value += 1;
    session.value = undefined;
    lifecycle.value = 'idle';
    error.value = undefined;
  };

  return {
    lifecycle,
    session,
    error,
    busy,
    start,
    stepTick,
    applyInputsAndStep,
    advance,
    fault,
    resetIo,
    resetInputs,
    stepNode,
    stepInstruction,
    run,
    pause,
    restart,
    markStale,
    stop,
    reset
  };
});
