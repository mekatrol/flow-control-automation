import type { ExecutableFlowSource } from '@/features/flows/api/flowDebugApi';
import type { FlowDebugTarget } from '@/features/flows/debugTargets';
import type { FlowDefinition, FlowNode } from '@/features/flows/types';

const supportedKinds = new Set([
  'digitalInput',
  'digitalConstant',
  'not',
  'and',
  'or',
  'memory',
  'digitalOutput'
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
  const graph = JSON.stringify({ nodes: flow.nodes, connections: flow.connections });
  let hash = 2166136261;
  for (let index = 0; index < graph.length; index += 1) {
    hash ^= graph.charCodeAt(index);
    hash = Math.imul(hash, 16777619);
  }
  return hash >>> 0 || 1;
};

const configurationFor = (node: FlowNode): Record<string, unknown> => {
  if (node.kind === 'digitalInput' || node.kind === 'digitalOutput') {
    const pointId = node.configuration.pointId;
    if (typeof pointId !== 'string' || !pointId.trim())
      throw new FlowDebugSourceError(`${node.label} (${node.id}) requires a point ID.`, node.id);
    return { pointId: pointId.trim() };
  }
  if (node.kind === 'digitalConstant' || node.kind === 'memory')
    return { value: Boolean(node.configuration.value) };
  return {};
};

export const createExecutableFlowSource = (
  flow: FlowDefinition,
  target: FlowDebugTarget
): ExecutableFlowSource => {
  if (!target.controllerTemplateId || !target.controllerTemplateRevision)
    throw new FlowDebugSourceError('Choose a compatible controller target.');
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
    execution: { mode: 'manual', intervalMs: 0, inputQualityPolicy: 'require_good' },
    nodes: flow.nodes.map((node) => ({
      id: node.id,
      kind: node.kind,
      configuration: configurationFor(node)
    })),
    connections: flow.connections.map((connection) => ({
      source: { nodeId: connection.start.nodeId, portId: connection.start.connectorId },
      target: { nodeId: connection.end.nodeId, portId: connection.end.connectorId }
    }))
  };
};
