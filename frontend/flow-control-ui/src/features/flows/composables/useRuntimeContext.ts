/* eslint-disable @typescript-eslint/explicit-function-return-type */
import { computed, ref, watch, type ComputedRef } from 'vue';
import {
  flowExecutionContextApi,
  type CreateExecutionContext,
  type FlowExecutionContext
} from '@/features/flows/api/flowExecutionContextApi';
import type { FlowDebugBreakpoint } from '@/features/flows/api/flowDebugApi';
import type { EmulatorInputChange } from '@/features/flows/api/flowEmulatorApi';
import { createExecutionNodeRuntime } from '@/features/flows/executionNodeRuntime';
import type { FlowDefinition } from '@/features/flows/types';

export const useRuntimeContext = (options: {
  flowId: ComputedRef<string>;
  flow: ComputedRef<FlowDefinition | undefined>;
  revision: ComputedRef<number>;
  deployedRuntime?: ComputedRef<
    import('@/features/flows/api/flowRuntimeApi').FlowRuntimeSnapshot | undefined
  >;
}) => {
  const context = ref<FlowExecutionContext>();
  const error = ref<string>();
  let polling: ReturnType<typeof window.setInterval> | undefined;
  const failure = (value: unknown): string =>
    value instanceof Error ? value.message : 'Execution operation failed.';
  const apply = (value: FlowExecutionContext): void => {
    context.value = value;
    error.value = value.diagnostic?.message;
  };
  const operate = async (
    operation: (id: string) => Promise<FlowExecutionContext>
  ): Promise<void> => {
    if (!context.value?.id) return;
    try {
      apply(await operation(context.value.id));
    } catch (value) {
      error.value = failure(value);
    }
  };
  const stopPolling = (): void => {
    if (polling !== undefined) window.clearInterval(polling);
    polling = undefined;
  };
  const create = async (request: CreateExecutionContext): Promise<void> => {
    stopPolling();
    error.value = undefined;
    try {
      apply(await flowExecutionContextApi.create(options.flowId.value, request));
    } catch (value) {
      error.value = failure(value);
    }
  };
  const run = async (): Promise<void> => {
    await operate((id) => flowExecutionContextApi.run(id));
    if (context.value?.lifecycle !== 'running') return;
    stopPolling();
    polling = window.setInterval(() => void operate(flowExecutionContextApi.get), 250);
  };
  const stop = async (keepalive = false): Promise<void> => {
    stopPolling();
    if (!context.value) return;
    try {
      apply(await flowExecutionContextApi.stop(context.value.id, keepalive));
    } catch (value) {
      if (!keepalive) error.value = failure(value);
    }
  };
  const replaceBreakpoints = (values: FlowDebugBreakpoint[]): Promise<void> =>
    operate((id) => flowExecutionContextApi.replaceBreakpoints(id, values));
  const nodeRuntime = computed(() => {
    const flow = options.flow.value;
    const value = context.value;
    if (!flow || !value || value.lifecycle === 'stale') return options.deployedRuntime?.value;
    return createExecutionNodeRuntime({
      flow,
      flowId: value.flowId,
      snapshot: value.snapshot,
      inspection: value.inspection,
      io: value.io,
      state: value.lifecycle === 'faulted' ? 'error' : 'running'
    });
  });
  watch(options.revision, (revision) => {
    if (context.value && revision !== context.value.revision)
      context.value = { ...context.value, lifecycle: 'stale' };
  });
  return {
    context,
    contextId: computed(() => context.value?.id),
    lifecycle: computed(() => context.value?.lifecycle ?? 'stopped'),
    capabilities: computed(() => context.value?.capabilities),
    presentation: computed(() => context.value?.presentation),
    runtime: computed(() => context.value?.snapshot),
    inspection: computed(() => context.value?.inspection),
    breakpoints: computed(() => context.value?.breakpoints ?? []),
    io: computed(() => context.value?.io),
    error,
    canvasRuntime: nodeRuntime,
    create,
    refresh: () => operate(flowExecutionContextApi.get),
    run,
    pause: () => operate(flowExecutionContextApi.pause),
    stop,
    restart: () => operate(flowExecutionContextApi.restart),
    stepTick: () => operate(flowExecutionContextApi.stepTick),
    stepNode: () => operate(flowExecutionContextApi.stepNode),
    stepInstruction: () => operate(flowExecutionContextApi.stepInstruction),
    runTo: (value: FlowDebugBreakpoint) =>
      operate((id) => flowExecutionContextApi.runTo(id, value)),
    replaceBreakpoints,
    applyInputs: (values: EmulatorInputChange[]) =>
      operate((id) => flowExecutionContextApi.applyInputs(id, values)),
    advance: (milliseconds: number) =>
      operate((id) => flowExecutionContextApi.advance(id, milliseconds)),
    injectFault: (fault: string | null) =>
      operate((id) => flowExecutionContextApi.injectFault(id, fault)),
    resetIo: (powerCycle: boolean) =>
      operate((id) => flowExecutionContextApi.resetIo(id, powerCycle)),
    resetInputs: () => operate(flowExecutionContextApi.resetInputs),
    enableLiveOutput: (pointIds: string[]) =>
      operate((id) => flowExecutionContextApi.enableLiveOutput(id, pointIds)),
    setBreakpoint: (nodeId: string, position: 'before' | 'after' | null) =>
      replaceBreakpoints([
        ...(context.value?.breakpoints ?? []).filter((item) => item.nodeId !== nodeId),
        ...(position ? [{ nodeId, position }] : [])
      ]),
    runToNode: (nodeId: string) =>
      operate((id) => flowExecutionContextApi.runTo(id, { nodeId, position: 'before' })),
    applyInputsAndStep: async (values: EmulatorInputChange[]) => {
      await operate((id) => flowExecutionContextApi.applyInputs(id, values));
      await operate(flowExecutionContextApi.stepTick);
    }
  };
};
