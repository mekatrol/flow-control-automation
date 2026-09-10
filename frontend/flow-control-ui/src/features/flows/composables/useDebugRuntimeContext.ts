/* eslint-disable @typescript-eslint/explicit-function-return-type */
import { computed, ref, type ComputedRef } from 'vue';

import { DataDirectionType, DataQualityType, DataType } from '@/types/serverTypes';
import {
  flowDebugApi,
  type DebugRuntimeSnapshot,
  type ExecutableFlowSource,
  type FlowDebugBreakpoint,
  type FlowDebugCapabilities,
  type FlowDebugInspection
} from '@/features/flows/api/flowDebugApi';
import {
  flowEmulatorApi,
  type EmulatorInputChange,
  type EmulatorSnapshot
} from '@/features/flows/api/flowEmulatorApi';
import type { FlowDebugTarget } from '@/features/flows/debugTargets';
import { FlowDebugTargetKind } from '@/features/flows/debugTargets';
import { createExecutionNodeRuntime } from '@/features/flows/executionNodeRuntime';
import type { FlowDefinition } from '@/features/flows/types';

export type DesignerDebugLifecycle =
  | 'idle'
  | 'loading'
  | 'ready'
  | 'stepping'
  | 'running'
  | 'paused'
  | 'fault'
  | 'stopped';

export const useDebugRuntimeContext = (options: {
  flowId: ComputedRef<string>;
  flow: ComputedRef<FlowDefinition | undefined>;
  revision: ComputedRef<number>;
  target: ComputedRef<FlowDebugTarget | undefined>;
  source: () => ExecutableFlowSource | undefined;
}) => {
  const lifecycle = ref<DesignerDebugLifecycle>('idle');
  const sessionId = ref<string>();
  const snapshot = ref<DebugRuntimeSnapshot>();
  const loadedRevision = ref<number>();
  const error = ref<string>();
  const affectedOutputPoints = ref<string[]>([]);
  const liveOutputEnabled = ref(false);
  const liveOutputPriority = ref<number>();
  const liveOutputHoldMilliseconds = ref<number>();
  const capabilities = ref<FlowDebugCapabilities>();
  const inspection = ref<FlowDebugInspection>();
  const executionOrder = ref<string[]>([]);
  const breakpoints = ref<FlowDebugBreakpoint[]>([]);
  const emulatorSnapshot = ref<EmulatorSnapshot>();
  let controller: AbortController | undefined;
  let pollTimer: ReturnType<typeof window.setInterval> | undefined;

  const stale = computed(() =>
    Boolean(snapshot.value && loadedRevision.value !== options.revision.value)
  );
  const active = computed(() => Boolean(sessionId.value));
  const host = computed<'server' | 'emulator' | 'controller'>(() => {
    const kind = options.target.value?.kind;
    return kind === FlowDebugTargetKind.Emulator || kind === FlowDebugTargetKind.Controller
      ? kind
      : FlowDebugTargetKind.Server;
  });
  const showEmulator = computed(() => options.target.value?.kind === FlowDebugTargetKind.Emulator);
  const failure = (value: unknown): string =>
    value instanceof Error ? value.message : 'Debug operation failed.';
  const stopPolling = (): void => {
    if (pollTimer !== undefined) window.clearInterval(pollTimer);
    pollTimer = undefined;
  };
  const applySession = (session: Awaited<ReturnType<typeof flowDebugApi.stepNode>>): void => {
    lifecycle.value = session.lifecycleState === 'empty' ? 'stopped' : session.lifecycleState;
    snapshot.value = session.snapshot;
    capabilities.value = session.capabilities;
    inspection.value = session.inspection;
    executionOrder.value = session.executionOrder ?? [];
  };
  const operate = async (
    operation: (
      flowId: string,
      id: string
    ) => Promise<Awaited<ReturnType<typeof flowDebugApi.stepNode>>>
  ): Promise<void> => {
    if (!sessionId.value) return;
    try {
      applySession(await operation(options.flowId.value, sessionId.value));
    } catch (value) {
      error.value = failure(value);
    }
  };

  const load = async (): Promise<void> => {
    controller?.abort();
    controller = new AbortController();
    lifecycle.value = 'loading';
    error.value = undefined;
    snapshot.value = undefined;
    try {
      const executable = options.source();
      if (!executable) throw new Error('The flow is not available.');
      if (options.target.value?.kind === FlowDebugTargetKind.Emulator && !emulatorSnapshot.value)
        emulatorSnapshot.value = await flowEmulatorApi.create(executable);
      const session = await flowDebugApi.load(
        executable,
        host.value,
        emulatorSnapshot.value?.emulatorId,
        controller.signal
      );
      if (session.flowId !== options.flowId.value || session.revision !== executable.revision)
        throw new Error('Loaded debug session does not match this flow revision.');
      sessionId.value = session.debugSessionId;
      loadedRevision.value = session.revision;
      snapshot.value = session.snapshot;
      affectedOutputPoints.value = session.affectedOutputPoints;
      liveOutputEnabled.value = session.liveOutputEnabled;
      liveOutputPriority.value = session.liveOutputPriority;
      liveOutputHoldMilliseconds.value = session.liveOutputHoldMilliseconds;
      capabilities.value = session.capabilities;
      inspection.value = session.inspection;
      lifecycle.value = 'ready';
    } catch (value) {
      lifecycle.value = 'fault';
      error.value = failure(value);
    }
  };
  const stepTick = async (): Promise<void> => {
    const id = sessionId.value;
    if (!id || stale.value) return;
    lifecycle.value = 'stepping';
    error.value = undefined;
    try {
      const next = await flowDebugApi.step(options.flowId.value, id);
      if (
        next.flowId !== options.flowId.value ||
        next.revision !== loadedRevision.value ||
        next.debugSessionId !== id
      )
        throw new Error('The debug service returned a stale or mismatched snapshot.');
      snapshot.value = next;
      lifecycle.value = 'ready';
    } catch (value) {
      lifecycle.value = 'fault';
      error.value = failure(value);
    }
  };
  const run = async (): Promise<void> => {
    const id = sessionId.value;
    if (!id) return;
    try {
      applySession(await flowDebugApi.run(options.flowId.value, id));
      if (lifecycle.value !== 'running') {
        lifecycle.value = 'fault';
        return;
      }
      stopPolling();
      pollTimer = window.setInterval(async () => {
        if (lifecycle.value !== 'running') return;
        try {
          const current = await flowDebugApi.inspect(options.flowId.value, id);
          applySession(current);
          if (current.lifecycleState !== 'running') stopPolling();
        } catch (value) {
          lifecycle.value = 'fault';
          error.value = failure(value);
          stopPolling();
        }
      }, 250);
    } catch (value) {
      lifecycle.value = 'fault';
      error.value = failure(value);
    }
  };
  const pause = async (): Promise<void> => {
    stopPolling();
    await operate(flowDebugApi.pause);
  };
  const stop = async (keepalive = false): Promise<void> => {
    stopPolling();
    controller?.abort();
    const id = sessionId.value;
    sessionId.value = undefined;
    affectedOutputPoints.value = [];
    liveOutputEnabled.value = false;
    liveOutputPriority.value = undefined;
    liveOutputHoldMilliseconds.value = undefined;
    capabilities.value = undefined;
    inspection.value = undefined;
    breakpoints.value = [];
    lifecycle.value = 'stopped';
    if (!id) return;
    try {
      await flowDebugApi.stop(options.flowId.value, id, keepalive);
    } catch (value) {
      if (!keepalive) error.value = failure(value);
    }
  };
  const runToNode = async (nodeId: string): Promise<void> =>
    operate((flowId, id) => flowDebugApi.runTo(flowId, id, { nodeId, position: 'before' }));
  const runToBreakpoint = async (): Promise<void> => {
    const breakpoint = breakpoints.value[0];
    if (!breakpoint) {
      error.value = 'Add a breakpoint by double-clicking a node first.';
      return;
    }
    await operate((flowId, id) => flowDebugApi.runTo(flowId, id, breakpoint));
  };
  const setBreakpoint = async (
    nodeId: string,
    position: 'before' | 'after' | null
  ): Promise<void> => {
    if (!sessionId.value || !capabilities.value?.maximumBreakpoints) return;
    const retained = breakpoints.value.filter((item) => item.nodeId !== nodeId);
    try {
      breakpoints.value = (
        await flowDebugApi.replaceBreakpoints(
          options.flowId.value,
          sessionId.value,
          position ? [...retained, { nodeId, position }] : retained
        )
      ).breakpoints;
    } catch (value) {
      error.value = failure(value);
    }
  };
  const enableLiveOutput = async (ids: string[]): Promise<void> => {
    if (!sessionId.value || stale.value) return;
    try {
      const session = await flowDebugApi.enableLiveOutput(
        options.flowId.value,
        sessionId.value,
        ids
      );
      liveOutputEnabled.value = session.liveOutputEnabled;
      liveOutputPriority.value = session.liveOutputPriority;
      liveOutputHoldMilliseconds.value = session.liveOutputHoldMilliseconds;
    } catch (value) {
      error.value = failure(value);
    }
  };
  const nodeRuntime = computed(() => {
    const currentFlow = options.flow.value;
    if (stale.value || !sessionId.value || !currentFlow) return undefined;
    const running = lifecycle.value === 'running' || lifecycle.value === 'stepping';
    return createExecutionNodeRuntime({
      flow: currentFlow,
      flowId: snapshot.value?.flowId ?? currentFlow.id,
      snapshot: snapshot.value,
      inspection: inspection.value,
      state: lifecycle.value === 'fault' ? 'error' : running ? 'running' : 'stopped'
    });
  });
  const connectorValues = computed(() => {
    const currentFlow = options.flow.value;
    if (!snapshot.value || !currentFlow || stale.value) return undefined;
    const values: Record<
      string,
      Record<string, import('@/features/flows/api/flowRuntimeApi').ConnectorRuntimeValue>
    > = {};
    const add = (
      nodeId: string,
      typed: NonNullable<DebugRuntimeSnapshot['nodes'][number]['typedValue']>,
      state: 'committed' | 'paused-frame'
    ) => {
      const node = currentFlow.nodes.find((item) => item.id === nodeId);
      if (!node) return;
      const text = typed.type === DataType.Number ? String(typed.number) : String(typed.value);
      values[nodeId] ??= {};
      for (const connector of node.connectors.filter(
        (item) => item.direction === DataDirectionType.Output
      ))
        values[nodeId]![connector.id] = {
          value: text,
          quality: typed.quality ?? DataQualityType.Good,
          state
        };
    };
    for (const item of snapshot.value.nodes)
      if (item.typedValue) add(item.nodeId, item.typedValue, 'committed');
    for (const [nodeId, typed] of Object.entries(inspection.value?.nodeValues ?? {}))
      add(nodeId, typed, 'paused-frame');
    for (const connection of currentFlow.connections) {
      const source = values[connection.start.nodeId]?.[connection.start.connectorId];
      if (source) (values[connection.end.nodeId] ??= {})[connection.end.connectorId] = source;
    }
    return values;
  });

  return {
    lifecycle,
    snapshot,
    stale,
    error,
    active,
    host,
    capabilities,
    inspection,
    executionOrder,
    breakpoints,
    affectedOutputPoints,
    liveOutputEnabled,
    liveOutputPriority,
    liveOutputHoldMilliseconds,
    emulatorSnapshot,
    showEmulator,
    nodeRuntime,
    connectorValues,
    load,
    stepTick,
    stepNode: () => operate(flowDebugApi.stepNode),
    stepInstruction: () => operate(flowDebugApi.stepInstruction),
    restart: () => operate(flowDebugApi.restart),
    run,
    runToNode,
    runToBreakpoint,
    pause,
    stop,
    setBreakpoint,
    enableLiveOutput,
    applyEmulatorInputs: async (inputs: EmulatorInputChange[]): Promise<void> => {
      if (emulatorSnapshot.value)
        emulatorSnapshot.value = await flowEmulatorApi.applyInputsAndStep(
          emulatorSnapshot.value.emulatorId,
          inputs
        );
    },
    advanceEmulator: async (ms: number) => {
      if (emulatorSnapshot.value)
        emulatorSnapshot.value = await flowEmulatorApi.advance(
          emulatorSnapshot.value.emulatorId,
          ms
        );
    },
    setEmulatorFault: async (fault: string | null) => {
      if (emulatorSnapshot.value)
        emulatorSnapshot.value = await flowEmulatorApi.fault(
          emulatorSnapshot.value.emulatorId,
          fault
        );
    },
    resetEmulator: async (powerCycle: boolean) => {
      if (emulatorSnapshot.value)
        emulatorSnapshot.value = await flowEmulatorApi.reset(
          emulatorSnapshot.value.emulatorId,
          powerCycle
        );
    },
    resetEmulatorInputs: async () => {
      if (emulatorSnapshot.value)
        emulatorSnapshot.value = await flowEmulatorApi.resetInputs(
          emulatorSnapshot.value.emulatorId
        );
    }
  };
};
