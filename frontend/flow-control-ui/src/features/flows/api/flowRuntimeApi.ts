import { FlowApiError } from './flowApi';

export type FlowRuntimeState = 'stopped' | 'running' | 'error';
export type NodeRuntimeState = 'idle' | 'running' | 'stopped' | 'error';

export interface NodeRuntimeSnapshot {
  state: NodeRuntimeState;
  // String values are retained for locally synthesized debugger snapshots; HTTP parsing only accepts the backend's boolean value.
  value?: boolean | string;
  typedValue?: RuntimeTypedValue;
  updatedAt: string;
}

export interface RuntimeTypedValue {
  dataType: 'any' | 'boolean' | 'number' | 'string' | 'event';
  boolean: boolean;
  number: number;
  quality: 'good' | 'bad' | 'uncertain' | 'unavailable';
}

export interface FlowRuntimeSnapshot {
  flowId: string;
  state: FlowRuntimeState;
  updatedAt: string;
  nodes: Record<string, NodeRuntimeSnapshot>;
}

export interface ConnectorRuntimeValue {
  value: string;
  quality: string;
  units?: string;
  state: 'committed' | 'paused-frame';
}

const isRecord = (value: unknown): value is Record<string, unknown> =>
  typeof value === 'object' && value !== null && !Array.isArray(value);
const isDate = (value: unknown): value is string =>
  typeof value === 'string' && !Number.isNaN(Date.parse(value));

export const parseFlowRuntimeSnapshot = (payload: unknown): FlowRuntimeSnapshot => {
  if (!isRecord(payload)) throw new TypeError('Runtime snapshot must be an object.');
  if (typeof payload.flowId !== 'string' || !payload.flowId)
    throw new TypeError('Runtime snapshot requires a flow ID.');
  if (!['stopped', 'running', 'error'].includes(String(payload.state)))
    throw new TypeError('Runtime snapshot has an invalid flow state.');
  if (!isDate(payload.updatedAt)) throw new TypeError('Runtime snapshot has an invalid timestamp.');
  if (!isRecord(payload.nodes)) throw new TypeError('Runtime snapshot requires node states.');

  const nodes: Record<string, NodeRuntimeSnapshot> = {};
  for (const [nodeId, candidate] of Object.entries(payload.nodes)) {
    if (!isRecord(candidate)) throw new TypeError(`Runtime node ${nodeId} must be an object.`);
    if (!['idle', 'running', 'stopped', 'error'].includes(String(candidate.state)))
      throw new TypeError(`Runtime node ${nodeId} has an invalid state.`);
    if (!isDate(candidate.updatedAt))
      throw new TypeError(`Runtime node ${nodeId} has an invalid timestamp.`);
    if (candidate.value !== undefined && candidate.value !== null && typeof candidate.value !== 'boolean')
      throw new TypeError(`Runtime node ${nodeId} has an invalid value.`);
    if (candidate.typedValue !== undefined && candidate.typedValue !== null) {
      if (!isRecord(candidate.typedValue))
        throw new TypeError(`Runtime node ${nodeId} has an invalid typed value.`);
      const typed = candidate.typedValue;
      if (
        !['any', 'boolean', 'number', 'string', 'event'].includes(String(typed.dataType)) ||
        typeof typed.boolean !== 'boolean' ||
        typeof typed.number !== 'number' ||
        !Number.isFinite(typed.number) ||
        !['good', 'bad', 'uncertain', 'unavailable'].includes(String(typed.quality))
      )
        throw new TypeError(`Runtime node ${nodeId} has an invalid typed value.`);
    }
    nodes[nodeId] = {
      state: candidate.state as NodeRuntimeState,
      ...(typeof candidate.value === 'boolean' ? { value: candidate.value } : {}),
      ...(isRecord(candidate.typedValue)
        ? { typedValue: candidate.typedValue as unknown as RuntimeTypedValue }
        : {}),
      updatedAt: candidate.updatedAt
    };
  }
  return {
    flowId: payload.flowId,
    state: payload.state as FlowRuntimeState,
    updatedAt: payload.updatedAt,
    nodes
  };
};

const requestRuntime = async (url: string, init: RequestInit): Promise<FlowRuntimeSnapshot> => {
  try {
    const response = await waitForFetch(url, init);
    if (!response.ok) {
      let message = `Runtime request failed with status ${response.status}.`;
      try {
        const body = (await response.json()) as { message?: unknown };
        if (typeof body.message === 'string' && body.message.trim()) message = body.message;
      } catch {
        // The response status is the fallback when the error body is not JSON.
      }
      throw new FlowApiError('http', message, response.status);
    }
    let payload: unknown;
    try {
      payload = await response.json();
    } catch {
      throw new FlowApiError('validation', 'The server returned malformed runtime JSON.');
    }
    try {
      return parseFlowRuntimeSnapshot(payload);
    } catch (error) {
      throw new FlowApiError(
        'validation',
        `The server returned invalid runtime state: ${error instanceof Error ? error.message : 'unknown error'}`
      );
    }
  } catch (error) {
    if (error instanceof FlowApiError) throw error;
    if (error instanceof DOMException && error.name === 'AbortError')
      throw new FlowApiError('cancelled', 'The runtime request was cancelled.');
    throw new FlowApiError('network', 'Unable to reach the runtime service.');
  }
};

export interface FlowRuntimeApiClient {
  deployFlow(flowId: string, signal?: AbortSignal): Promise<FlowRuntimeSnapshot>;
  getRuntime(flowId: string, signal?: AbortSignal): Promise<FlowRuntimeSnapshot>;
}

export const flowRuntimeApi: FlowRuntimeApiClient = {
  deployFlow: (flowId, signal) =>
    requestRuntime(`/api/flows/${encodeURIComponent(flowId)}/deploy`, { method: 'POST', signal }),
  getRuntime: (flowId, signal) =>
    requestRuntime(`/api/flows/${encodeURIComponent(flowId)}/runtime`, { method: 'GET', signal })
};
import { waitForFetch } from '@/api/waitForFetch';
