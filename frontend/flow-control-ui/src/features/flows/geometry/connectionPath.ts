import type { Point } from './connectorLayout';
import type { ConnectorSide } from '@/features/flows/types';

const calculateControlOffset = (distance: number, curvature: number): number => {
  if (distance >= 0) return 0.5 * distance;

  return curvature * 25 * Math.sqrt(-distance);
};

const controlPoint = (
  side: ConnectorSide,
  point: Point,
  oppositePoint: Point,
  curvature: number
): Point => {
  switch (side) {
    case 'left':
      return {
        x: point.x - calculateControlOffset(point.x - oppositePoint.x, curvature),
        y: point.y
      };
    case 'right':
      return {
        x: point.x + calculateControlOffset(oppositePoint.x - point.x, curvature),
        y: point.y
      };
    case 'top':
      return {
        x: point.x,
        y: point.y - calculateControlOffset(point.y - oppositePoint.y, curvature)
      };
    case 'bottom':
      return {
        x: point.x,
        y: point.y + calculateControlOffset(oppositePoint.y - point.y, curvature)
      };
  }
};

export const connectionPath = (
  start?: Point,
  end?: Point,
  startSide: ConnectorSide = 'right',
  endSide: ConnectorSide = 'left',
  curvature = 0.25
): string => {
  if (!start || !end) return '';

  // React Flow's Bezier router derives each control point from the handle side.
  // For handles facing one another, the controls meet halfway. When a handle
  // points away from its destination, the square-root offset creates a compact
  // loop without making close connections bulge excessively.
  const startControl = controlPoint(startSide, start, end, curvature);
  const endControl = controlPoint(endSide, end, start, curvature);

  return `M ${start.x} ${start.y} C ${startControl.x} ${startControl.y}, ${endControl.x} ${endControl.y}, ${end.x} ${end.y}`;
};
