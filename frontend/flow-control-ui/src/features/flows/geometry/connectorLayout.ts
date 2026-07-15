import type { ConnectorSide, FlowNodeConnector } from '@/features/flows/types';

export interface Point {
  x: number;
  y: number;
}

export interface ConnectorLayout extends Point {
  connector: FlowNodeConnector;
}

const pointOnSide = (
  side: ConnectorSide,
  position: number,
  width: number,
  height: number
): Point => {
  // Connector positions are node-local SVG coordinates. Position zero is the
  // top or left corner and position one is the opposite corner of that side.
  switch (side) {
    case 'left':
      return { x: 0, y: height * position };
    case 'right':
      return { x: width, y: height * position };
    case 'top':
      return { x: width * position, y: 0 };
    case 'bottom':
      return { x: width * position, y: height };
  }
};

export const layoutConnectors = (
  connectors: FlowNodeConnector[],
  width: number,
  height: number
): ConnectorLayout[] => {
  const sides: ConnectorSide[] = ['left', 'right', 'top', 'bottom'];
  return sides.flatMap((side) => {
    const onSide = connectors.filter((connector) => connector.side === side);
    // Dividing a side into N + 1 gaps spaces connectors evenly while reserving
    // room at both corners, where a connector would be harder to distinguish.
    return onSide.map((connector, index) => ({
      connector,
      ...pointOnSide(side, (index + 1) / (onSide.length + 1), width, height)
    }));
  });
};
