import { FlowApiError } from './flowApi';

export type DebugLifecycleState =
  | 'empty'
  | 'loading'
  | 'ready'
  | 'stepping'
  | 'paused'
  | 'fault'
  | 'stopped'
  | 'running';

export interface DebugTypedValue {
  type: string;
  value: boolean;
}

export interface DebugNodeSnapshot {
  nodeId: string;
  state: string;
  quality: string;
  typedValue?: DebugTypedValue;
}

export interface DebugProposedOutput {
  pointId: string;
  state: string;
  quality: string;
  proposedValue: boolean;
}

export interface DebugRuntimeSnapshot {
  debugSessionId: string;
  flowId: string;
  revision: number;
  lifecycleState: DebugLifecycleState;
  mode: string;
  tickNumber: number;
  sampledAtMs: number;
  completedAtMs: number;
  executionDurationUs: number;
  executionHighWaterUs?: number;
  missedDeadlineCount?: number;
  inputValidity: string[];
  nodes: DebugNodeSnapshot[];
  proposedOutputs: DebugProposedOutput[];
  overrunCount: number;
  evaluationFailureCount: number;
  arbitrationLossCount?: number;
  lastReasonCode: number;
  lastReason: string;
  lastReasonPath: string;
}

export interface FlowDebugSession {
  debugSessionId: string;
  flowId: string;
  revision: number;
  lifecycleState: DebugLifecycleState;
  mode: string;
  tickNumber: number;
  leaseRemainingMilliseconds: number;
  lastReasonCode: number;
  lastReason: string;
  lastReasonPath: string;
  snapshot?: DebugRuntimeSnapshot;
  affectedOutputPoints: string[];
  liveOutputEnabled: boolean;
  liveOutputPriority?: number;
  liveOutputHoldMilliseconds?: number;
}

export interface ExecutableFlowSource {
  schemaVersion: 1;
  id: string;
  revision: number;
  controllerTemplateId: string;
  controllerTemplateRevision: number;
  execution: { mode: 'manual'; intervalMs: number; inputQualityPolicy: 'require_good' };
  nodes: { id: string; kind: string; configuration: Record<string, unknown> }[];
  connections: {
    source: { nodeId: string; portId: string };
    target: { nodeId: string; portId: string };
  }[];
}

const isRecord = (value: unknown): value is Record<string, unknown> =>
  typeof value === 'object' && value !== null && !Array.isArray(value);
const text = (value: unknown, path: string): string => {
  if (typeof value !== 'string' || !value) throw new TypeError(`${path} must be a string.`);
  return value;
};
const number = (value: unknown, path: string): number => {
  if (typeof value !== 'number' || !Number.isSafeInteger(value) || value < 0)
    throw new TypeError(`${path} must be a non-negative integer.`);
  return value;
};
const string = (value: unknown, path: string): string => {
  if (typeof value !== 'string') throw new TypeError(`${path} must be a string.`);
  return value;
};
const lifecycle = (value: unknown): DebugLifecycleState => {
  const state = text(value, 'lifecycleState') as DebugLifecycleState;
  if (
    !['empty', 'loading', 'ready', 'stepping', 'paused', 'fault', 'stopped', 'running'].includes(
      state
    )
  )
    throw new TypeError('lifecycleState is invalid.');
  return state;
};

const parseNode = (value: unknown, index: number): DebugNodeSnapshot => {
  if (!isRecord(value)) throw new TypeError(`nodes[${index}] must be an object.`);
  let typedValue: DebugTypedValue | undefined;
  if (value.typedValue !== null && value.typedValue !== undefined) {
    if (!isRecord(value.typedValue) || typeof value.typedValue.value !== 'boolean')
      throw new TypeError(`nodes[${index}].typedValue is invalid.`);
    typedValue = {
      type: text(value.typedValue.type, `nodes[${index}].typedValue.type`),
      value: value.typedValue.value
    };
  }
  return {
    nodeId: text(value.nodeId, `nodes[${index}].nodeId`),
    state: text(value.state, `nodes[${index}].state`),
    quality: text(value.quality, `nodes[${index}].quality`),
    ...(typedValue ? { typedValue } : {})
  };
};

export const parseDebugSnapshot = (value: unknown): DebugRuntimeSnapshot => {
  if (!isRecord(value)) throw new TypeError('Debug snapshot must be an object.');
  if (
    !Array.isArray(value.nodes) ||
    !Array.isArray(value.proposedOutputs) ||
    !Array.isArray(value.inputValidity)
  )
    throw new TypeError('Debug snapshot collections are invalid.');
  return {
    debugSessionId: text(value.debugSessionId, 'debugSessionId'),
    flowId: text(value.flowId, 'flowId'),
    revision: number(value.revision, 'revision'),
    lifecycleState: lifecycle(value.lifecycleState),
    mode: text(value.mode, 'mode'),
    tickNumber: number(value.tickNumber, 'tickNumber'),
    sampledAtMs: number(value.sampledAtMs, 'sampledAtMs'),
    completedAtMs: number(value.completedAtMs, 'completedAtMs'),
    executionDurationUs: number(value.executionDurationUs, 'executionDurationUs'),
    executionHighWaterUs: number(
      value.executionHighWaterUs ?? value.executionDurationUs,
      'executionHighWaterUs'
    ),
    missedDeadlineCount: number(value.missedDeadlineCount ?? 0, 'missedDeadlineCount'),
    inputValidity: value.inputValidity.map((item, index) => text(item, `inputValidity[${index}]`)),
    nodes: value.nodes.map(parseNode),
    proposedOutputs: value.proposedOutputs.map((item, index) => {
      if (!isRecord(item) || typeof item.proposedValue !== 'boolean')
        throw new TypeError(`proposedOutputs[${index}] is invalid.`);
      return {
        pointId: text(item.pointId, `proposedOutputs[${index}].pointId`),
        state: text(item.state, `proposedOutputs[${index}].state`),
        quality: text(item.quality, `proposedOutputs[${index}].quality`),
        proposedValue: item.proposedValue
      };
    }),
    overrunCount: number(value.overrunCount, 'overrunCount'),
    evaluationFailureCount: number(value.evaluationFailureCount, 'evaluationFailureCount'),
    arbitrationLossCount: number(value.arbitrationLossCount ?? 0, 'arbitrationLossCount'),
    lastReasonCode: number(value.lastReasonCode, 'lastReasonCode'),
    lastReason: string(value.lastReason, 'lastReason'),
    lastReasonPath: string(value.lastReasonPath, 'lastReasonPath')
  };
};

const parseSession = (value: unknown): FlowDebugSession => {
  if (!isRecord(value)) throw new TypeError('Debug session must be an object.');
  const affectedOutputPoints = value.affectedOutputPoints ?? [];
  if (!Array.isArray(affectedOutputPoints))
    throw new TypeError('affectedOutputPoints must be an array.');
  return {
    debugSessionId: text(value.debugSessionId, 'debugSessionId'),
    flowId: text(value.flowId, 'flowId'),
    revision: number(value.revision, 'revision'),
    lifecycleState: lifecycle(value.lifecycleState),
    mode: text(value.mode, 'mode'),
    tickNumber: number(value.tickNumber, 'tickNumber'),
    leaseRemainingMilliseconds: number(
      value.leaseRemainingMilliseconds,
      'leaseRemainingMilliseconds'
    ),
    lastReasonCode: number(value.lastReasonCode, 'lastReasonCode'),
    lastReason: string(value.lastReason, 'lastReason'),
    lastReasonPath: string(value.lastReasonPath, 'lastReasonPath'),
    affectedOutputPoints: affectedOutputPoints.map((item, index) =>
      text(item, `affectedOutputPoints[${index}]`)
    ),
    liveOutputEnabled: value.liveOutputEnabled === true,
    ...(value.liveOutputPriority === null || value.liveOutputPriority === undefined
      ? {}
      : { liveOutputPriority: number(value.liveOutputPriority, 'liveOutputPriority') }),
    ...(value.liveOutputHoldMilliseconds === null || value.liveOutputHoldMilliseconds === undefined
      ? {}
      : {
          liveOutputHoldMilliseconds: number(
            value.liveOutputHoldMilliseconds,
            'liveOutputHoldMilliseconds'
          )
        }),
    ...(value.snapshot === null || value.snapshot === undefined
      ? {}
      : { snapshot: parseDebugSnapshot(value.snapshot) })
  };
};

const request = async <T>(
  url: string,
  init: RequestInit,
  parse: (value: unknown) => T
): Promise<T> => {
  try {
    const response = await fetch(url, init);
    if (!response.ok) {
      const body = (await response.json().catch(() => ({}))) as { message?: unknown };
      throw new FlowApiError(
        'http',
        typeof body.message === 'string'
          ? body.message
          : `Debug request failed with status ${response.status}.`,
        response.status
      );
    }
    return parse(await response.json());
  } catch (error) {
    if (error instanceof FlowApiError || error instanceof TypeError) throw error;
    if (error instanceof DOMException && error.name === 'AbortError')
      throw new FlowApiError('cancelled', 'The debug request was cancelled.');
    throw new FlowApiError('network', 'Unable to reach the debug service.');
  }
};

const base = (flowId: string): string => `/api/flows/${encodeURIComponent(flowId)}/debug-sessions`;
export const flowDebugApi = {
  load: (source: ExecutableFlowSource, signal?: AbortSignal) =>
    request(
      base(source.id),
      {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ source, replaceExisting: false }),
        signal
      },
      parseSession
    ),
  inspect: (flowId: string, sessionId: string, signal?: AbortSignal) =>
    request(
      `${base(flowId)}/${encodeURIComponent(sessionId)}`,
      { method: 'GET', signal },
      parseSession
    ),
  step: (flowId: string, sessionId: string, signal?: AbortSignal) =>
    request(
      `${base(flowId)}/${encodeURIComponent(sessionId)}/step`,
      { method: 'POST', signal },
      parseDebugSnapshot
    ),
  run: (flowId: string, sessionId: string, intervalMilliseconds = 500, signal?: AbortSignal) =>
    request(
      `${base(flowId)}/${encodeURIComponent(sessionId)}/run`,
      {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ intervalMilliseconds }),
        signal
      },
      parseSession
    ),
  pause: (flowId: string, sessionId: string, signal?: AbortSignal) =>
    request(
      `${base(flowId)}/${encodeURIComponent(sessionId)}/pause`,
      { method: 'POST', signal },
      parseSession
    ),
  enableLiveOutput: (
    flowId: string,
    sessionId: string,
    confirmedPointIds: string[],
    signal?: AbortSignal
  ) =>
    request(
      `${base(flowId)}/${encodeURIComponent(sessionId)}/live-output`,
      {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ confirmedPointIds }),
        signal
      },
      parseSession
    ),
  stop: async (flowId: string, sessionId: string, keepalive = false): Promise<void> => {
    const response = await fetch(`${base(flowId)}/${encodeURIComponent(sessionId)}/stop`, {
      method: 'POST',
      keepalive
    });
    if (!response.ok && response.status !== 404)
      throw new FlowApiError(
        'http',
        `Stop failed with status ${response.status}.`,
        response.status
      );
  }
};
