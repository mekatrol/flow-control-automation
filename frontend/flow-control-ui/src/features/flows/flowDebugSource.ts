import type { ExecutableFlowSource } from '@/features/flows/api/flowDebugApi';
import type { FlowDebugTarget } from '@/features/flows/debugTargets';
import type { FlowDefinition, FlowNode } from '@/features/flows/types';

const supportedKinds = new Set([
  'digitalInput',
  'digitalConstant',
  'not',
  'and',
  'or',
  'nand',
  'nor',
  'xor',
  'xnor',
  'numericConstant',
  'analogInput',
  'analogOutput',
  'add',
  'subtract',
  'multiply',
  'divide',
  'power',
  'negate',
  'comparator',
  'counter',
  'levelShifter',
  'qualityGood',
  'onDelay',
  'risingEdge',
  'memory',
  'digitalOutput',
  'average',
  'calculator',
  'calendar',
  'clamp',
  'delay',
  'digitalSwitch',
  'line',
  'max',
  'min',
  'override',
  'pulse',
  'schedule',
  'analogSwitch',
  'sequence',
  'split',
  'timer',
  'a2d',
  'd2a'
]);

export class FlowDebugSourceError extends Error {
  constructor(
    message: string,
    readonly nodeId?: string
  ) {
    super(message);
    this.name = 'FlowDebugSourceError';
  }
}

export const graphRevision = (flow: FlowDefinition): number => {
  const graph = JSON.stringify({
    nodes: flow.nodes,
    connections: flow.connections
  });
  let hash = 2166136261;
  for (let index = 0; index < graph.length; index += 1) {
    hash ^= graph.charCodeAt(index);
    hash = Math.imul(hash, 16777619);
  }
  return hash >>> 0 || 1;
};

const configurationFor = (node: FlowNode): Record<string, unknown> => {
  if (
    node.kind === 'digitalInput' ||
    node.kind === 'digitalOutput' ||
    node.kind === 'analogInput' ||
    node.kind === 'analogOutput'
  ) {
    const pointId = node.configuration.pointId;
    if (typeof pointId !== 'string' || !pointId.trim())
      throw new FlowDebugSourceError(`${node.label} (${node.id}) requires a point ID.`, node.id);
    return { pointId: pointId.trim() };
  }
  if (node.kind === 'digitalConstant') return { value: Boolean(node.configuration.value) };
  if (node.kind === 'numericConstant' || node.kind === 'memory')
    return { value: Number(node.configuration.value) };
  if (node.kind === 'comparator') return { operator: String(node.configuration.operator) };
  if (node.kind === 'calculator') return { formula: String(node.configuration.formula) };
  if (node.kind === 'levelShifter')
    return { gain: Number(node.configuration.gain), offset: Number(node.configuration.offset) };
  if (node.kind === 'onDelay') return { durationMs: Number(node.configuration.durationMs) };
  if (node.kind === 'delay' || node.kind === 'pulse' || node.kind === 'timer')
    return { durationMs: Number(node.configuration.durationMs) };
  if (node.kind === 'clamp')
    return {
      minimum: Number(node.configuration.minimum),
      maximum: Number(node.configuration.maximum)
    };
  if (node.kind === 'line')
    return { gain: Number(node.configuration.gain), offset: Number(node.configuration.offset) };
  if (node.kind === 'a2d')
    return {
      activeLowThreshold: Number(node.configuration.activeLowThreshold),
      activeHighThreshold: Number(node.configuration.activeHighThreshold)
    };
  if (node.kind === 'd2a')
    return {
      lowValue: Number(node.configuration.lowValue),
      highValue: Number(node.configuration.highValue)
    };
  if (node.kind === 'schedule' || node.kind === 'calendar')
    return { enabled: Boolean(node.configuration.enabled) };
  return {};
};

export const createExecutableFlowSource = (
  flow: FlowDefinition,
  target: FlowDebugTarget
): ExecutableFlowSource => {
  if (!target.controllerTemplateId || !target.controllerTemplateRevision)
    throw new FlowDebugSourceError('Choose a compatible execution target.');
  const unsupported = flow.nodes.find((node) => !supportedKinds.has(node.kind));
  if (unsupported)
    throw new FlowDebugSourceError(
      `${unsupported.label} (${unsupported.id}) uses unsupported debug function “${unsupported.kind}”.`,
      unsupported.id
    );
  if (flow.nodes.length === 0) throw new FlowDebugSourceError('Add at least one debug node.');

  return {
    schemaVersion: 1,
    id: flow.id,
    revision: graphRevision(flow),
    controllerTemplateId: target.controllerTemplateId,
    controllerTemplateRevision: target.controllerTemplateRevision,
    execution: {
      mode: 'manual',
      intervalMs: 0,
      inputQualityPolicy: flow.nodes.some((node) => node.kind === 'qualityGood')
        ? 'propagate'
        : 'requireGood'
    },
    nodes: flow.nodes.map((node) => ({
      id: node.id,
      kind: node.kind,
      configuration: configurationFor(node),
      label: node.label,
      x: node.x,
      y: node.y,
      zOrder: node.zOrder,
      ...(node.groupId ? { groupId: node.groupId } : {})
    })),
    connections: flow.connections.map((connection) => ({
      source: { nodeId: connection.start.nodeId, portId: connection.start.connectorId },
      target: { nodeId: connection.end.nodeId, portId: connection.end.connectorId }
    })),
    // Flow definitions come from a reactive Pinia store. Browser structuredClone
    // rejects Vue proxy objects, while declarations contain only scalar fields.
    virtualPointDeclarations: (flow.virtualPointDeclarations ?? []).map((declaration) => ({
      ...declaration
    }))
  };
};
