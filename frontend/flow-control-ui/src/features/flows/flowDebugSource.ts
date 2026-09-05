import { FlowNodeType } from '@/types/serverTypes';
import { FlowExecutionModeType, InputQualityPolicyType } from '@/types/serverTypes';
import type { ExecutableFlowSource } from '@/features/flows/api/flowDebugApi';
import type { FlowDebugTarget } from '@/features/flows/debugTargets';
import {
  isVirtualPointNode,
  virtualPointDeclarationsFromNodes,
  unconnectedVirtualPoint,
  type FlowDefinition,
  type FlowNode
} from '@/features/flows/types';

const supportedNodeTypes = new Set<FlowNodeType>([
  FlowNodeType.DigitalInput,
  FlowNodeType.DigitalConstant,
  FlowNodeType.Not,
  FlowNodeType.And,
  FlowNodeType.Or,
  FlowNodeType.Nand,
  FlowNodeType.Nor,
  FlowNodeType.Xor,
  FlowNodeType.Xnor,
  FlowNodeType.AnalogConstant,
  FlowNodeType.AnalogInput,
  FlowNodeType.AnalogVirtual,
  FlowNodeType.AnalogOutput,
  FlowNodeType.Add,
  FlowNodeType.Subtract,
  FlowNodeType.Multiply,
  FlowNodeType.Divide,
  FlowNodeType.Power,
  FlowNodeType.Negate,
  FlowNodeType.Comparator,
  FlowNodeType.Counter,
  FlowNodeType.Clock,
  FlowNodeType.LevelShifter,
  FlowNodeType.QualityGood,
  FlowNodeType.OnDelay,
  FlowNodeType.RisingEdge,
  FlowNodeType.Memory,
  FlowNodeType.DigitalOutput,
  FlowNodeType.DigitalVirtual,
  FlowNodeType.Average,
  FlowNodeType.Calculator,
  FlowNodeType.Calendar,
  FlowNodeType.Clamp,
  FlowNodeType.Delay,
  FlowNodeType.DigitalSwitch,
  FlowNodeType.Line,
  FlowNodeType.Max,
  FlowNodeType.Min,
  FlowNodeType.Override,
  FlowNodeType.Pulse,
  FlowNodeType.Schedule,
  FlowNodeType.AnalogSwitch,
  FlowNodeType.Sequence,
  FlowNodeType.Split,
  FlowNodeType.Timer,
  FlowNodeType.A2D,
  FlowNodeType.D2A
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
    node.nodeType === FlowNodeType.DigitalInput ||
    node.nodeType === FlowNodeType.DigitalOutput ||
    node.nodeType === FlowNodeType.AnalogInput ||
    node.nodeType === FlowNodeType.AnalogOutput ||
    node.nodeType === FlowNodeType.AnalogVirtual ||
    node.nodeType === FlowNodeType.DigitalVirtual
  ) {
    const pointId = node.configuration.pointId;
    if (typeof pointId !== 'string' || !pointId.trim())
      throw new FlowDebugSourceError(`${node.label} (${node.id}) requires a point ID.`, node.id);
    return { pointId: pointId.trim() };
  }
  if (node.nodeType === FlowNodeType.DigitalConstant)
    return { value: Boolean(node.configuration.value) };
  if (node.nodeType === FlowNodeType.AnalogConstant || node.nodeType === FlowNodeType.Memory)
    return { value: Number(node.configuration.value) };
  if (node.nodeType === FlowNodeType.Comparator)
    return { operator: String(node.configuration.operator) };
  if (node.nodeType === FlowNodeType.Calculator)
    return { formula: String(node.configuration.formula) };
  if (node.nodeType === FlowNodeType.LevelShifter)
    return { gain: Number(node.configuration.gain), offset: Number(node.configuration.offset) };
  if (node.nodeType === FlowNodeType.OnDelay)
    return { durationMs: Number(node.configuration.durationMs) };
  if (
    node.nodeType === FlowNodeType.Delay ||
    node.nodeType === FlowNodeType.Pulse ||
    node.nodeType === FlowNodeType.Timer
  )
    return { durationMs: Number(node.configuration.durationMs) };
  if (node.nodeType === FlowNodeType.Clock)
    return {
      frequencyHz: Number(node.configuration.frequencyHz),
      dutyCycle: Number(node.configuration.dutyCycle)
    };
  if (node.nodeType === FlowNodeType.Clamp)
    return {
      minimum: Number(node.configuration.minimum),
      maximum: Number(node.configuration.maximum)
    };
  if (node.nodeType === FlowNodeType.Line)
    return { gain: Number(node.configuration.gain), offset: Number(node.configuration.offset) };
  if (node.nodeType === FlowNodeType.A2D)
    return {
      activeLowThreshold: Number(node.configuration.activeLowThreshold),
      activeHighThreshold: Number(node.configuration.activeHighThreshold)
    };
  if (node.nodeType === FlowNodeType.D2A)
    return {
      lowValue: Number(node.configuration.lowValue),
      highValue: Number(node.configuration.highValue)
    };
  if (node.nodeType === FlowNodeType.Schedule || node.nodeType === FlowNodeType.Calendar)
    return { enabled: Boolean(node.configuration.enabled) };
  return {};
};

export const createExecutableFlowSource = (
  flow: FlowDefinition,
  target: FlowDebugTarget
): ExecutableFlowSource => {
  if (!target.controllerTemplateId || !target.controllerTemplateRevision)
    throw new FlowDebugSourceError('Choose a compatible execution target.');
  const unsupported = flow.nodes.find((node) => !supportedNodeTypes.has(node.nodeType));
  if (unsupported)
    throw new FlowDebugSourceError(
      `${unsupported.label} (${unsupported.id}) uses unsupported debug function “${unsupported.nodeType}”.`,
      unsupported.id
    );
  if (flow.nodes.length === 0) throw new FlowDebugSourceError('Add at least one debug node.');

  const disconnectedVirtual = unconnectedVirtualPoint(flow);
  if (disconnectedVirtual)
    throw new FlowDebugSourceError(
      `${disconnectedVirtual.label} (${disconnectedVirtual.id}) must have its Set input, Value output, or both connected.`,
      disconnectedVirtual.id
    );

  const virtualUsage = new Map(
    flow.nodes.filter(isVirtualPointNode).map((node) => {
      const reads = flow.connections.some((connection) => connection.start.nodeId === node.id);
      const writes = flow.connections.some((connection) => connection.end.nodeId === node.id);
      return [node.id, { reads, writes }] as const;
    })
  );

  const executableNodes = flow.nodes.flatMap((node) => {
    if (!isVirtualPointNode(node)) return [node];
    const usage = virtualUsage.get(node.id)!;
    const result: FlowNode[] = [];
    if (usage.reads) result.push(node);
    if (usage.writes)
      result.push({
        ...node,
        id: usage.reads ? `${node.id}--write` : node.id,
        nodeType:
          node.nodeType === FlowNodeType.AnalogVirtual
            ? FlowNodeType.AnalogOutput
            : FlowNodeType.DigitalOutput
      });
    return result;
  });

  return {
    schemaVersion: 1,
    id: flow.id,
    revision: graphRevision(flow),
    controllerTemplateId: target.controllerTemplateId,
    controllerTemplateRevision: target.controllerTemplateRevision,
    execution: {
      mode: FlowExecutionModeType.Manual,
      intervalMs: 0,
      inputQualityPolicy: flow.nodes.some((node) => node.nodeType === FlowNodeType.QualityGood)
        ? InputQualityPolicyType.Propagate
        : InputQualityPolicyType.RequireGood
    },
    nodes: executableNodes.map((node) => ({
      id: node.id,
      nodeType:
        node.nodeType === FlowNodeType.AnalogVirtual
          ? FlowNodeType.AnalogInput
          : node.nodeType === FlowNodeType.DigitalVirtual
            ? FlowNodeType.DigitalInput
            : node.nodeType,
      configuration: configurationFor(node),
      label: node.label,
      x: node.x,
      y: node.y,
      zOrder: node.zOrder,
      ...(node.groupId ? { groupId: node.groupId } : {})
    })),
    connections: flow.connections.map((connection) => ({
      source: { nodeId: connection.start.nodeId, portId: connection.start.connectorId },
      target: {
        nodeId:
          virtualUsage.has(connection.end.nodeId) && virtualUsage.get(connection.end.nodeId)?.reads
            ? `${connection.end.nodeId}--write`
            : connection.end.nodeId,
        portId: connection.end.connectorId
      }
    })),
    // Flow definitions come from a reactive Pinia store. Browser structuredClone
    // rejects Vue proxy objects, while declarations contain only scalar fields.
    virtualPointDeclarations: virtualPointDeclarationsFromNodes(flow.nodes).map((declaration) => ({
      ...declaration
    }))
  };
};
