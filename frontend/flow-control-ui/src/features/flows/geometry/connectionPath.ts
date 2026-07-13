import type { Point } from './connectorLayout';

export const connectionPath = (start?: Point, end?: Point): string => {
  if (!start || !end) return '';

  const horizontalDistance = end.x - start.x;
  const verticalDistance = end.y - start.y;

  // A mostly vertical connection bends above and below its endpoints. This keeps
  // the line clear of the node edges instead of forcing a sideways loop first.
  if (Math.abs(verticalDistance) > Math.abs(horizontalDistance) * 1.5) {
    const offset = Math.max(60, Math.abs(verticalDistance) * 0.45);
    const direction = Math.sign(verticalDistance) || 1;
    return `M ${start.x} ${start.y} C ${start.x} ${start.y + offset * direction}, ${end.x} ${end.y - offset * direction}, ${end.x} ${end.y}`;
  }

  // SVG paths use M to move to the first point and C for a cubic Bezier curve.
  // Its two control points pull the line horizontally away from both nodes. A
  // minimum offset prevents nearby connectors from producing a cramped corner.
  const offset = Math.max(80, Math.abs(horizontalDistance) * 0.45);
  const startDirection = horizontalDistance >= 0 ? 1 : -1;
  return `M ${start.x} ${start.y} C ${start.x + offset * startDirection} ${start.y}, ${end.x - offset * startDirection} ${end.y}, ${end.x} ${end.y}`;
};
