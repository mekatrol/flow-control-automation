import type { ConnectorSide, FlowNodeConnector } from '@/features/flows/types';

export interface Point {
  x: number;
  y: number;
}

export interface ConnectorLayout extends Point {
  connector: FlowNodeConnector;
  hitRadius: number;
}

const pointOnSide = (
  side: ConnectorSide,
  position: number,
  width: number,
  height: number
): Point => {
  const alongWidth = Math.round(width * position * 1000) / 1000;
  const alongHeight = Math.round(height * position * 1000) / 1000;
  // Connector positions are node-local SVG coordinates. Position zero is the
  // top or left corner and position one is the opposite corner of that side.
  switch (side) {
    case 'left':
      return { x: 0, y: alongHeight };
    case 'right':
      return { x: width, y: alongHeight };
    case 'top':
      return { x: alongWidth, y: 0 };
    case 'bottom':
      return { x: alongWidth, y: height };
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
    const sideLength = side === 'left' || side === 'right' ? height : width;
    const isVertical = side === 'left' || side === 'right';
    const baseSpacing = sideLength / (onSide.length + 1);
    const spacing = isVertical && onSide.length > 1 ? baseSpacing + 1 : baseSpacing;
    const firstPosition = (sideLength - spacing * (onSide.length - 1)) / 2;
    // Keep adjacent hit targets from overlapping. An overlap lets a connector
    // rendered later in the SVG intercept a pointer intended for its neighbour.
    // Vertically stacked targets retain a one-pixel clear gap as an additional
    // visual and interaction boundary between closely grouped ports.
    const hitRadius = Math.min(16, (spacing - (isVertical && onSide.length > 1 ? 1 : 0)) / 2);
    // Dividing a side into N + 1 gaps spaces connectors evenly while reserving
    // room at both corners, where a connector would be harder to distinguish.
    return onSide.map((connector, index) => ({
      connector,
      hitRadius,
      ...pointOnSide(side, (firstPosition + spacing * index) / sideLength, width, height)
    }));
  });
};
