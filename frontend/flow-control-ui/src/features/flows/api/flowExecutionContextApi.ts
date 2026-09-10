import { waitForFetch } from '@/api/waitForFetch';
import type {
  DebugRuntimeSnapshot,
  FlowDebugBreakpoint,
  FlowDebugInspection
} from './flowDebugApi';
import type { EmulatorInputChange, EmulatorSnapshot } from './flowEmulatorApi';

export type FlowExecutionMode = 'simulator' | 'debugger';
export type FlowExecutionLifecycle =
  | 'preparing'
  | 'ready'
  | 'running'
  | 'paused'
  | 'stepping'
  | 'stale'
  | 'faulted'
  | 'stopped';

export interface FlowExecutionCapabilities {
  canRun: boolean;
  canPause: boolean;
  canStop: boolean;
  canRestart: boolean;
  canStepTick: boolean;
  canStepNode: boolean;
  canStepInstruction: boolean;
  canUseBreakpoints: boolean;
  canRunTo: boolean;
  canEditInputs: boolean;
  canAdvanceVirtualTime: boolean;
  canInjectFaults: boolean;
  canResetIo: boolean;
  canEnableLiveOutputs: boolean;
  locksFlowEditing: boolean;
}

export interface FlowExecutionContext {
  id: string;
  flowId: string;
  revision: number;
  mode: FlowExecutionMode;
  lifecycle: FlowExecutionLifecycle;
  capabilities: FlowExecutionCapabilities;
  breakpoints: FlowDebugBreakpoint[];
  snapshot?: DebugRuntimeSnapshot;
  inspection?: FlowDebugInspection;
  io?: EmulatorSnapshot & { liveOutputEnabled?: boolean; liveOutputPointIds?: string[] };
  presentation: { modeLabel: string; hostLabel: string; isSimulated: boolean; usesPhysicalIo: boolean };
  diagnostic?: { code: string; message: string };
  leaseRemainingMilliseconds: number;
}

export interface CreateExecutionContext {
  mode: FlowExecutionMode;
  expectedRevision: number;
  targetId: string;
  replaceExisting?: boolean;
  breakpoints?: FlowDebugBreakpoint[];
}

export class FlowExecutionContextApiError extends Error {
  constructor(message: string, readonly status: number, readonly code = 'request_failed') {
    super(message);
  }
}

const parse = (value: unknown): FlowExecutionContext => {
  if (!value || typeof value !== 'object' || Array.isArray(value)) throw new Error('Execution context is malformed.');
  const item = value as Record<string, unknown>;
  const modes = ['simulator', 'debugger'];
  const states = ['preparing', 'ready', 'running', 'paused', 'stepping', 'stale', 'faulted', 'stopped'];
  if (
    typeof item.id !== 'string' || typeof item.flowId !== 'string' ||
    typeof item.revision !== 'number' || !modes.includes(String(item.mode)) ||
    !states.includes(String(item.lifecycle)) || !item.capabilities ||
    typeof item.capabilities !== 'object' || !item.presentation ||
    typeof item.presentation !== 'object' || !Array.isArray(item.breakpoints)
  ) throw new Error('Execution context is malformed.');
  const capabilities = item.capabilities as Record<string, unknown>;
  for (const name of [
    'canRun','canPause','canStop','canRestart','canStepTick','canStepNode',
    'canStepInstruction','canUseBreakpoints','canRunTo','canEditInputs',
    'canAdvanceVirtualTime','canInjectFaults','canResetIo','canEnableLiveOutputs','locksFlowEditing'
  ]) if (typeof capabilities[name] !== 'boolean') throw new Error('Execution context capabilities are malformed.');
  return value as FlowExecutionContext;
};

const request = async (path: string, init?: RequestInit): Promise<FlowExecutionContext> => {
  const response = await waitForFetch(path, init);
  if (!response.ok) {
    let message = `Execution operation failed (${response.status}).`;
    let code = 'request_failed';
    try {
      const body = (await response.json()) as Record<string, unknown>;
      if (typeof body.message === 'string') message = body.message;
      if (typeof body.code === 'string') code = body.code;
    } catch { /* retain stable fallback */ }
    throw new FlowExecutionContextApiError(message, response.status, code);
  }
  return parse(await response.json());
};
const json = (method: string, body?: unknown): RequestInit => ({
  method,
  headers: body === undefined ? undefined : { 'Content-Type': 'application/json' },
  body: body === undefined ? undefined : JSON.stringify(body)
});
const base = (id: string): string => `/api/execution-contexts/${encodeURIComponent(id)}`;

export const flowExecutionContextApi = {
  create: (flowId: string, value: CreateExecutionContext) => request(
    `/api/flows/${encodeURIComponent(flowId)}/execution-contexts`,
    json('POST', { flowId, ...value, replaceExisting: value.replaceExisting ?? true, breakpoints: value.breakpoints ?? [] })
  ),
  get: (id: string) => request(base(id)),
  stop: (id: string, keepalive = false) => request(`${base(id)}/stop`, { ...json('POST'), keepalive }),
  run: (id: string, intervalMilliseconds = 100) => request(`${base(id)}/run`, json('POST', { intervalMilliseconds })),
  pause: (id: string) => request(`${base(id)}/pause`, json('POST')),
  restart: (id: string) => request(`${base(id)}/restart`, json('POST')),
  stepTick: (id: string) => request(`${base(id)}/step-tick`, json('POST')),
  stepNode: (id: string) => request(`${base(id)}/step-node`, json('POST')),
  stepInstruction: (id: string) => request(`${base(id)}/step-instruction`, json('POST')),
  runTo: (id: string, boundary: FlowDebugBreakpoint) => request(`${base(id)}/run-to`, json('POST', boundary)),
  replaceBreakpoints: (id: string, values: FlowDebugBreakpoint[]) => request(`${base(id)}/breakpoints`, json('PUT', values)),
  applyInputs: (id: string, inputs: EmulatorInputChange[]) => request(`${base(id)}/inputs`, json('PUT', { inputs })),
  advance: (id: string, milliseconds: number) => request(`${base(id)}/advance`, json('POST', { milliseconds })),
  injectFault: (id: string, fault: string | null) => request(`${base(id)}/fault`, json('PUT', { fault })),
  resetIo: (id: string, powerCycle: boolean) => request(`${base(id)}/reset-io`, json('POST', { powerCycle })),
  resetInputs: (id: string) => request(`${base(id)}/reset-inputs`, json('POST')),
  enableLiveOutput: (id: string, pointIds: string[]) => request(`${base(id)}/live-output`, json('POST', { pointIds })),
  keepAlive: (id: string) => request(`${base(id)}/keepalive`, json('POST'))
};
