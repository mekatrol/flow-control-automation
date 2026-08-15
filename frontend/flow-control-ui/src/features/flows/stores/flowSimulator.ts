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
import {
  flowScenarioApi,
  type FlowScenario,
  type FlowScenarioRunResult
} from '@/features/flows/api/flowScenarioApi';

export const useFlowSimulatorStore = defineStore('flow-simulator', () => {
  const lifecycle = ref<SimulatorLifecycle>('idle');
  const session = ref<SimulatorSession>();
  const error = ref<string>();
  const requestGeneration = ref(0);
  const recording = ref(false);
  const recordedSteps = ref<FlowScenario['steps']>([]);
  const scenarios = ref<FlowScenario[]>([]);
  const scenarioResult = ref<FlowScenarioRunResult>();
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

  const record = (
    action: FlowScenario['steps'][number]['action'],
    inputs: EmulatorInputChange[] = [],
    powerCycle = false,
    atMilliseconds = session.value?.io?.virtualTimeMilliseconds ?? 0
  ): void => {
    if (!recording.value) return;
    recordedSteps.value.push({
      atMilliseconds,
      action,
      inputs: structuredClone(inputs),
      powerCycle
    });
  };
  const stepTick = (): Promise<void> => {
    record('step');
    return operate(flowSimulatorApi.stepTick);
  };
  const applyInputsAndStep = (inputs: EmulatorInputChange[]): Promise<void> => {
    record('step', inputs);
    return operate((flowId, sessionId, signal) =>
      flowSimulatorApi.applyInputsAndStep(flowId, sessionId, inputs, signal)
    );
  };
  const advance = (milliseconds: number): Promise<void> => {
    record('advance', [], false, (session.value?.io?.virtualTimeMilliseconds ?? 0) + milliseconds);
    return operate((flowId, sessionId, signal) =>
      flowSimulatorApi.advance(flowId, sessionId, milliseconds, signal)
    );
  };
  const fault = (value: string | null): Promise<void> =>
    operate((flowId, sessionId, signal) =>
      flowSimulatorApi.fault(flowId, sessionId, value, signal)
    );
  const resetIo = (powerCycle: boolean): Promise<void> => {
    record('reset', [], powerCycle);
    return operate((flowId, sessionId, signal) =>
      flowSimulatorApi.resetIo(flowId, sessionId, powerCycle, signal)
    );
  };
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
  const startRecording = (): void => {
    recordedSteps.value = [];
    recording.value = true;
    scenarioResult.value = undefined;
  };
  const stopRecording = (): void => {
    recording.value = false;
  };
  const loadScenarios = async (flowId: string): Promise<void> => {
    try {
      scenarios.value = await flowScenarioApi.list(flowId);
    } catch (value) {
      failure(value);
    }
  };
  const saveRecording = async (name: string): Promise<void> => {
    const active = session.value;
    if (!active || recordedSteps.value.length === 0) return;
    const scenario: FlowScenario = {
      schemaVersion: 1,
      id: crypto.randomUUID(),
      name: name.trim(),
      flowId: active.flowId,
      flowRevision: active.sourceRevision,
      steps: structuredClone(recordedSteps.value),
      expectations:
        active.io?.outputHistory
          .filter((sample) => sample.isInterface && sample.scanNumber === active.io?.scanNumber)
          .map((sample) => ({
            scan: sample.scanNumber,
            outputId: sample.outputId,
            operator: 'equals' as const,
            expectedValue: structuredClone(sample.effectiveValue)
          })) ?? []
    };
    try {
      scenarios.value.push(await flowScenarioApi.save(scenario));
      recording.value = false;
    } catch (value) {
      failure(value);
    }
  };
  const replay = async (scenario: FlowScenario, source: ExecutableFlowSource): Promise<void> => {
    scenarioResult.value = undefined;
    try {
      scenarioResult.value = await flowScenarioApi.run(scenario, source);
    } catch (value) {
      failure(value);
    }
  };
  const runAll = async (source: ExecutableFlowSource): Promise<void> => {
    for (const scenario of scenarios.value) {
      await replay(scenario, source);
      if (scenarioResult.value?.passed === false) return;
    }
  };
  const importScenario = async (scenario: FlowScenario): Promise<void> => {
    try {
      const saved = await flowScenarioApi.save(scenario);
      const index = scenarios.value.findIndex((item) => item.id === saved.id);
      if (index < 0) scenarios.value.push(saved);
      else scenarios.value[index] = saved;
    } catch (value) {
      failure(value);
    }
  };
  const deleteScenario = async (scenario: FlowScenario): Promise<void> => {
    try {
      await flowScenarioApi.remove(scenario.flowId, scenario.id);
      scenarios.value = scenarios.value.filter((item) => item.id !== scenario.id);
    } catch (value) {
      failure(value);
    }
  };

  return {
    lifecycle,
    session,
    error,
    busy,
    recording,
    recordedSteps,
    scenarios,
    scenarioResult,
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
    reset,
    startRecording,
    stopRecording,
    loadScenarios,
    saveRecording,
    replay,
    runAll,
    importScenario,
    deleteScenario
  };
});
