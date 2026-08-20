import { FlowApiError } from './flowApi';
import { waitForFetch } from '@/api/waitForFetch';
import type { FlowInterface } from '@/features/flows/types';

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
  value?: boolean;
  number?: number;
  quality?: string;
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
  proposedValue?: boolean;
  proposedNumber?: number;
  typedValue?: DebugTypedValue;
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
  host: 'server' | 'emulator' | 'controller';
  capabilities: FlowDebugCapabilities;
  breakpoints: FlowDebugBreakpoint[];
  inspection?: FlowDebugInspection;
  executionOrder?: string[];
}

export interface FlowDebugCapabilities {
  stepTick: boolean;
  stepNode: boolean;
  stepInstruction: boolean;
  continue: boolean;
  pause: boolean;
  runTo: boolean;
  maximumBreakpoints: number;
  maximumInspectableSlots: number;
}

export interface FlowDebugBreakpoint {
  nodeId: string;
  position: 'before' | 'after';
  instructionDiscriminator?: number;
}

export interface FlowDebugInspection {
  instructionPointer: number;
  isAtCommit: boolean;
  nodeId?: string;
  slots: DebugTypedValue[];
  currentState: DebugTypedValue[];
  stagedNextState: (DebugTypedValue | null)[];
  proposedOutputs: { pointId: string; value: boolean }[];
  nodeValues?: Record<string, DebugTypedValue>;
}

export interface ExecutableFlowSource {
  schemaVersion: 1;
  id: string;
  revision: number;
  controllerTemplateId: string;
  controllerTemplateRevision: number;
  execution: {
    mode: 'manual';
    intervalMs: number;
    inputQualityPolicy: 'require_good' | 'propagate';
  };
  nodes: {
    id: string;
    kind: string;
    configuration: Record<string, unknown>;
    label: string;
    x: number;
    y: number;
    zOrder: number;
    groupId?: string;
  }[];
  connections: {
    source: { nodeId: string; portId: string };
    target: { nodeId: string; portId: string };
  }[];
  interface: FlowInterface;
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
const parseTypedValue = (value: unknown, path: string): DebugTypedValue => {
  if (!isRecord(value)) throw new TypeError(`${path} is invalid.`);
  const dataType = text(value.dataType, `${path}.dataType`);
  if (value.value !== null && value.value !== undefined && typeof value.value !== 'boolean')
    throw new TypeError(`${path}.value is invalid.`);
  if (value.number !== null && value.number !== undefined && typeof value.number !== 'number')
    throw new TypeError(`${path}.number is invalid.`);
  return {
    type: dataType,
    ...(typeof value.value === 'boolean' ? { value: value.value } : {}),
    ...(typeof value.number === 'number' ? { number: value.number } : {}),
    ...(typeof value.quality === 'string' ? { quality: value.quality } : {})
  };
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
    typedValue = parseTypedValue(value.typedValue, `nodes[${index}].typedValue`);
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
  const capabilities = isRecord(value.capabilities) ? value.capabilities : {};
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
    host: value.host === 'server' || value.host === 'emulator' ? value.host : 'controller',
    capabilities: {
      stepTick: capabilities.stepTick !== false,
      stepNode: capabilities.stepNode === true,
      stepInstruction: capabilities.stepInstruction === true,
      continue: capabilities.continue === true,
      pause: capabilities.pause === true,
      runTo: capabilities.runTo === true,
      maximumBreakpoints: number(capabilities.maximumBreakpoints ?? 0, 'maximumBreakpoints'),
      maximumInspectableSlots: number(
        capabilities.maximumInspectableSlots ?? 0,
        'maximumInspectableSlots'
      )
    },
    breakpoints: Array.isArray(value.breakpoints)
      ? value.breakpoints.map((item) => item as unknown as FlowDebugBreakpoint)
      : [],
    executionOrder: Array.isArray(value.executionOrder)
      ? value.executionOrder.filter((item): item is string => typeof item === 'string')
      : [],
    ...(isRecord(value.inspection)
      ? { inspection: value.inspection as unknown as FlowDebugInspection }
      : {}),
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
    const response = await waitForFetch(url, init);
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
  load: (
    source: ExecutableFlowSource,
    host: 'server' | 'emulator' | 'controller',
    emulatorId?: string,
    signal?: AbortSignal
  ) =>
    request(
      base(source.id),
      {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ source, host, emulatorId, replaceExisting: false }),
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
  stepInstruction: (flowId: string, sessionId: string, signal?: AbortSignal) =>
    request(
      `${base(flowId)}/${encodeURIComponent(sessionId)}/step-instruction`,
      { method: 'POST', signal },
      parseSession
    ),
  stepNode: (flowId: string, sessionId: string, signal?: AbortSignal) =>
    request(
      `${base(flowId)}/${encodeURIComponent(sessionId)}/step-node`,
      { method: 'POST', signal },
      parseSession
    ),
  runTo: (
    flowId: string,
    sessionId: string,
    breakpoint: FlowDebugBreakpoint,
    signal?: AbortSignal
  ) =>
    request(
      `${base(flowId)}/${encodeURIComponent(sessionId)}/run-to`,
      {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(breakpoint),
        signal
      },
      parseSession
    ),
  restart: (flowId: string, sessionId: string, signal?: AbortSignal) =>
    request(
      `${base(flowId)}/${encodeURIComponent(sessionId)}/restart`,
      { method: 'POST', signal },
      parseSession
    ),
  replaceBreakpoints: (
    flowId: string,
    sessionId: string,
    breakpoints: FlowDebugBreakpoint[],
    signal?: AbortSignal
  ) =>
    request(
      `${base(flowId)}/${encodeURIComponent(sessionId)}/breakpoints`,
      {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(breakpoints),
        signal
      },
      parseSession
    ),
  inspectFrame: (flowId: string, sessionId: string, signal?: AbortSignal) =>
    request(
      `${base(flowId)}/${encodeURIComponent(sessionId)}/frame`,
      { method: 'GET', signal },
      (value) => value as FlowDebugInspection
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
    const response = await waitForFetch(`${base(flowId)}/${encodeURIComponent(sessionId)}/stop`, {
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
