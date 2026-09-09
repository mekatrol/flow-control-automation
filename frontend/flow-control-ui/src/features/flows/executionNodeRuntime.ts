import type { EmulatorSnapshot } from '@/features/flows/api/flowEmulatorApi';
import type { DebugRuntimeSnapshot, FlowDebugInspection } from '@/features/flows/api/flowDebugApi';
import type {
  FlowRuntimeSnapshot,
  FlowRuntimeState,
  NodeRuntimeState
} from '@/features/flows/api/flowRuntimeApi';
import { isInputPointNode, isOutputPointNode } from '@/features/flows/flowPointValidation';
import { isVirtualPointNode, type FlowDefinition } from '@/features/flows/types';

interface ExecutionNodeRuntimeOptions {
  flow: FlowDefinition;
  flowId: string;
  snapshot?: DebugRuntimeSnapshot;
  inspection?: FlowDebugInspection;
  io?: EmulatorSnapshot;
  state: FlowRuntimeState;
}

interface CompatibleTypedValue {
  type?: string;
  dataType?: string;
  value?: boolean;
  boolean?: boolean;
  number?: number;
}

const displayValue = (typed?: CompatibleTypedValue): string | undefined => {
  if (!typed) return undefined;
  const type = typed.type || typed.dataType;
  const value = type === 'number' ? typed.number : (typed.value ?? typed.boolean);
  return value === undefined ? undefined : String(value);
};

/** Builds the canvas runtime shared by simulator and debugger execution views. */
export const createExecutionNodeRuntime = ({
  flow,
  flowId,
  snapshot,
  inspection,
  io,
  state
}: ExecutionNodeRuntimeOptions): FlowRuntimeSnapshot => {
  const snapshotsByNode = new Map(snapshot?.nodes.map((node) => [node.nodeId, node]) ?? []);
  const inspectedValues = inspection?.nodeValues ?? {};
  const proposedValues = new Map(
    snapshot?.proposedOutputs.map((output) => [output.pointId, output.typedValue]) ?? []
  );
  const inputValues = new Map(io?.inputs.map((input) => [input.pointId, input.typedValue]) ?? []);
  const outputValues = new Map(
    (io?.outputHistory ?? []).map((output) => [output.outputId, output.effectiveValue])
  );
  const updatedAt = new Date(snapshot?.completedAtMs ?? Date.now()).toISOString();

  const nodes = Object.fromEntries(
    flow.nodes.map((flowNode) => {
      const writeNodeId = `${flowNode.id}--write`;
      const node = snapshotsByNode.get(flowNode.id) ?? snapshotsByNode.get(writeNodeId);
      const pointId = String(flowNode.configuration.pointId ?? '');
      const ioValue = isInputPointNode(flowNode)
        ? inputValues.get(pointId)
        : isOutputPointNode(flowNode)
          ? outputValues.get(pointId)
          : undefined;
      const value =
        displayValue(
          node?.typedValue ??
            inspectedValues[flowNode.id] ??
            inspectedValues[writeNodeId] ??
            proposedValues.get(pointId)
        ) ?? displayValue(ioValue);
      const nodeState: NodeRuntimeState =
        node?.state === 'fault' || snapshot?.lastReasonPath.includes(flowNode.id)
          ? 'error'
          : state;

      return [
        flowNode.id,
        {
          state: nodeState,
          ...(value === undefined ? {} : { value }),
          updatedAt
        }
      ];
    })
  );

  // A proposed point command is the value on the connector feeding its output
  // sink. Controller snapshots may omit the source node's sampled slot, so use
  // that command to fill only the immediately connected source node.
  const flowNodesById = new Map(flow.nodes.map((node) => [node.id, node]));
  for (const connection of flow.connections) {
    const target = flowNodesById.get(connection.end.nodeId);
    const targetValue = nodes[connection.end.nodeId]?.value;
    if (
      !target ||
      (!isOutputPointNode(target) && !isVirtualPointNode(target)) ||
      targetValue === undefined ||
      nodes[connection.start.nodeId]?.value !== undefined
    )
      continue;
    nodes[connection.start.nodeId] = {
      ...nodes[connection.start.nodeId]!,
      value: targetValue
    };
  }

  return {
    flowId,
    state,
    updatedAt,
    nodes
  };
};
