import { getNodeTypeDefinition } from '@/features/flows/nodeTypes';
import type { FlowNode, FlowNodeType } from '@/features/flows/types';

export const createDefaultNode = (
  nodeType: FlowNodeType,
  position: { x: number; y: number },
  zOrder: number,
  id = `node-${crypto.randomUUID()}`
): FlowNode => {
  // The registry is the single source for each nodeType's initial appearance,
  // connectors, and settings. Copy its nested values so two new nodes never
  // share mutable defaults with each other or with the registry itself.
  const definition = getNodeTypeDefinition(nodeType);
  return {
    id,
    nodeType: nodeType,
    label: `New ${definition.label}`,
    x: position.x,
    y: position.y,
    zOrder,
    connectors: definition.connectors.map((connector) => ({ ...connector })),
    configuration: { ...definition.defaultConfiguration }
  };
};
