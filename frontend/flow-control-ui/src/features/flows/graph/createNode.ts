import { getNodeKind } from '../nodeKinds';
import type { FlowNode, FlowNodeKind } from '../types';

export const createDefaultNode = (
  kind: FlowNodeKind,
  position: { x: number; y: number },
  zOrder: number,
  id = `node-${crypto.randomUUID()}`
): FlowNode => {
  // The registry is the single source for each kind's initial appearance,
  // connectors, and settings. Copy its nested values so two new nodes never
  // share mutable defaults with each other or with the registry itself.
  const definition = getNodeKind(kind);
  return {
    id,
    kind,
    label: `New ${definition.label}`,
    x: position.x,
    y: position.y,
    zOrder,
    color: definition.color,
    connectors: definition.connectors.map((connector) => ({ ...connector })),
    configuration: { ...definition.defaultConfiguration }
  };
};
