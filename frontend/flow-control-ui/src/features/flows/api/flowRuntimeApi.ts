import { FlowApiError } from './flowApi';

export type FlowRuntimeState = 'stopped' | 'running' | 'error';
export type NodeRuntimeState = 'idle' | 'running' | 'stopped' | 'error';

export interface NodeRuntimeSnapshot {
  state: NodeRuntimeState;
  value?: string;
  updatedAt: string;
}

export interface FlowRuntimeSnapshot {
  flowId: string;
  state: FlowRuntimeState;
  updatedAt: string;
  nodes: Record<string, NodeRuntimeSnapshot>;
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
    if (candidate.value !== undefined && typeof candidate.value !== 'string')
      throw new TypeError(`Runtime node ${nodeId} has an invalid value.`);
    nodes[nodeId] = {
      state: candidate.state as NodeRuntimeState,
      ...(candidate.value === undefined ? {} : { value: candidate.value }),
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
    const response = await fetch(url, init);
    if (!response.ok)
      throw new FlowApiError(
        'http',
        `Runtime request failed with status ${response.status}.`,
        response.status
      );
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
