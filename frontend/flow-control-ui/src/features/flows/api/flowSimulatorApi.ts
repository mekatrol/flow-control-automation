import { FlowApiError } from './flowApi';
import { waitForFetch } from '@/api/waitForFetch';
import type {
  DebugRuntimeSnapshot,
  ExecutableFlowSource,
  FlowDebugBreakpoint,
  FlowDebugCapabilities,
  FlowDebugInspection
} from './flowDebugApi';
import type { EmulatorInputChange, EmulatorSnapshot } from './flowEmulatorApi';

export type SimulatorLifecycle =
  | 'idle'
  | 'compiling'
  | 'ready'
  | 'running'
  | 'paused'
  | 'faulted'
  | 'stopped'
  | 'stale';

export interface SimulatorSession {
  sessionId: string;
  flowId: string;
  sourceRevision: number;
  sourceDigest: string;
  lifecycleState: Exclude<SimulatorLifecycle, 'idle' | 'compiling' | 'stale'>;
  capabilities: FlowDebugCapabilities;
  snapshot?: DebugRuntimeSnapshot;
  io?: EmulatorSnapshot;
  inspection?: FlowDebugInspection;
  breakpoints: FlowDebugBreakpoint[];
  leaseRemainingMilliseconds: number;
}

interface CompilerDiagnostic {
  message: string;
  path?: string;
}

const compilerDiagnostics = (value: unknown): CompilerDiagnostic[] => {
  if (!Array.isArray(value)) return [];
  return value.flatMap((item) => {
    if (typeof item !== 'object' || item === null || !('message' in item)) return [];
    if (typeof item.message !== 'string' || !item.message.trim()) return [];
    const path = 'path' in item && typeof item.path === 'string' ? item.path.trim() : '';
    return [{ message: item.message.trim(), ...(path ? { path } : {}) }];
  });
};

const errorMessage = (body: unknown, status: number): string => {
  if (typeof body !== 'object' || body === null)
    return `Simulator request failed with status ${status}.`;
  const payload = body as { message?: unknown; diagnostics?: unknown };
  const summary =
    typeof payload.message === 'string' && payload.message.trim()
      ? payload.message.trim()
      : `Simulator request failed with status ${status}.`;
  const details = compilerDiagnostics(payload.diagnostics).map((diagnostic) =>
    diagnostic.path ? `${diagnostic.message} (${diagnostic.path})` : diagnostic.message
  );
  return details.length > 0 ? `${summary} ${details.join(' ')}` : summary;
};

const parse = (value: unknown): SimulatorSession => {
  if (typeof value !== 'object' || value === null)
    throw new TypeError('Simulator session is invalid.');
  const item = value as Partial<SimulatorSession>;
  if (
    typeof item.sessionId !== 'string' ||
    typeof item.flowId !== 'string' ||
    typeof item.sourceRevision !== 'number' ||
    typeof item.sourceDigest !== 'string' ||
    typeof item.lifecycleState !== 'string' ||
    typeof item.leaseRemainingMilliseconds !== 'number' ||
    typeof item.capabilities !== 'object' ||
    item.capabilities === null
  )
    throw new TypeError('Simulator session fields are invalid.');
  return {
    ...item,
    breakpoints: Array.isArray(item.breakpoints) ? item.breakpoints : []
  } as SimulatorSession;
};

const request = async (
  url: string,
  init: RequestInit,
  signal?: AbortSignal,
  trackWait = true
): Promise<SimulatorSession> => {
  try {
    const response = await waitForFetch(url, { ...init, signal }, { trackWait });
    if (!response.ok) {
      const body: unknown = await response.json().catch(() => undefined);
      throw new FlowApiError('http', errorMessage(body, response.status), response.status);
    }
    return parse(await response.json());
  } catch (error) {
    if (error instanceof FlowApiError || error instanceof TypeError) throw error;
    if (error instanceof DOMException && error.name === 'AbortError')
      throw new FlowApiError('cancelled', 'The simulator request was cancelled.');
    throw new FlowApiError('network', 'Unable to reach the simulator.');
  }
};

const base = (flowId: string): string =>
  `/api/flows/${encodeURIComponent(flowId)}/simulator-sessions`;
const sessionUrl = (flowId: string, sessionId: string): string =>
  `${base(flowId)}/${encodeURIComponent(sessionId)}`;

export const flowSimulatorApi = {
  start: (source: ExecutableFlowSource, signal?: AbortSignal) =>
    request(
      base(source.id),
      {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ source, replaceExisting: true })
      },
      signal
    ),
  get: (flowId: string, sessionId: string, signal?: AbortSignal) =>
    request(sessionUrl(flowId, sessionId), { method: 'GET' }, signal, false),
  stepTick: (flowId: string, sessionId: string, signal?: AbortSignal) =>
    request(`${sessionUrl(flowId, sessionId)}/step`, { method: 'POST' }, signal),
  applyInputsAndStep: (
    flowId: string,
    sessionId: string,
    inputs: EmulatorInputChange[],
    signal?: AbortSignal
  ) =>
    request(
      `${sessionUrl(flowId, sessionId)}/apply-and-step`,
      {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ inputs })
      },
      signal
    ),
  advance: (flowId: string, sessionId: string, milliseconds: number, signal?: AbortSignal) =>
    request(
      `${sessionUrl(flowId, sessionId)}/advance`,
      {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ milliseconds, scan: true })
      },
      signal
    ),
  fault: (flowId: string, sessionId: string, fault: string | null, signal?: AbortSignal) =>
    request(
      `${sessionUrl(flowId, sessionId)}/fault`,
      {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ fault })
      },
      signal
    ),
  resetIo: (flowId: string, sessionId: string, powerCycle: boolean, signal?: AbortSignal) =>
    request(
      `${sessionUrl(flowId, sessionId)}/reset-io`,
      {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ powerCycle })
      },
      signal
    ),
  resetInputs: (flowId: string, sessionId: string, signal?: AbortSignal) =>
    request(`${sessionUrl(flowId, sessionId)}/reset-inputs`, { method: 'POST' }, signal),
  stepNode: (flowId: string, sessionId: string, signal?: AbortSignal) =>
    request(`${sessionUrl(flowId, sessionId)}/step-node`, { method: 'POST' }, signal),
  stepInstruction: (flowId: string, sessionId: string, signal?: AbortSignal) =>
    request(`${sessionUrl(flowId, sessionId)}/step-instruction`, { method: 'POST' }, signal),
  restart: (flowId: string, sessionId: string, signal?: AbortSignal) =>
    request(`${sessionUrl(flowId, sessionId)}/restart`, { method: 'POST' }, signal),
  run: (flowId: string, sessionId: string, signal?: AbortSignal) =>
    request(
      `${sessionUrl(flowId, sessionId)}/run`,
      {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ intervalMilliseconds: 500 })
      },
      signal
    ),
  pause: (flowId: string, sessionId: string, signal?: AbortSignal) =>
    request(`${sessionUrl(flowId, sessionId)}/pause`, { method: 'POST' }, signal),
  stop: async (flowId: string, sessionId: string, keepalive = false): Promise<void> => {
    const response = await waitForFetch(sessionUrl(flowId, sessionId), {
      method: 'DELETE',
      keepalive
    });
    if (!response.ok && response.status !== 404)
      throw new FlowApiError(
        'http',
        `Stop simulation failed with status ${response.status}.`,
        response.status
      );
  }
};
