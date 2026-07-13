import type { FlowNode } from '../types';

export type ZOrderCommand = 'front' | 'forward' | 'backward' | 'back';

const ordered = (nodes: FlowNode[]): FlowNode[] =>
  // SVG paints later elements over earlier ones, so ascending z-order is also
  // the order in which node groups must be rendered.
  [...nodes].sort((left, right) => left.zOrder - right.zOrder);

export const canReorderNode = (
  nodes: FlowNode[],
  nodeId: string,
  command: ZOrderCommand
): boolean => {
  const sorted = ordered(nodes);
  const index = sorted.findIndex((node) => node.id === nodeId);
  if (index < 0) return false;
  return command === 'front' || command === 'forward' ? index < sorted.length - 1 : index > 0;
};

export const reorderNode = (
  nodes: FlowNode[],
  nodeId: string,
  command: ZOrderCommand
): FlowNode[] => {
  // Returning the existing array for an impossible move avoids reporting a
  // graph change when the node is already at the requested boundary.
  if (!canReorderNode(nodes, nodeId, command)) return nodes;

  const next = ordered(nodes);
  const index = next.findIndex((node) => node.id === nodeId);
  const [node] = next.splice(index, 1);
  if (!node) return nodes;

  const target =
    command === 'front'
      ? next.length
      : command === 'forward'
        ? index + 1
        : command === 'backward'
          ? index - 1
          : 0;
  next.splice(target, 0, node);
  // Normalize to consecutive values after every move. Persisted ordering then
  // stays deterministic even if the incoming graph contained gaps or ties.
  return next.map((entry, zOrder) => ({ ...entry, zOrder }));
};
