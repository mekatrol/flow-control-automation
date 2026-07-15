import { describe, expect, it } from 'vitest';

import {
  calculateDraggedPosition,
  constrainNodePosition,
  snapCoordinate,
  useNodeDragging,
  type NodeDragState
} from '@/features/flows/composables/useNodeDragging';

const state: NodeDragState = {
  nodeId: 'node-1',
  pointerId: 7,
  pointerStart: { x: 100, y: 100 },
  nodeStart: { x: 48, y: 72 }
};
const bounds = { width: 500, height: 300, nodeWidth: 200, nodeHeight: 60 };

describe('node dragging', () => {
  it('calculates a snapped drag delta', () => {
    expect(calculateDraggedPosition(state, { x: 131, y: 143 }, bounds, 24, true)).toEqual({
      x: 72,
      y: 120
    });
  });

  it('supports disabled snapping and positive and negative grid values', () => {
    expect(snapCoordinate(35, 24, true)).toBe(24);
    expect(snapCoordinate(-35, 24, true)).toBe(-24);
    expect(snapCoordinate(35, 24, false)).toBe(35);
  });

  it('clamps nodes at every canvas boundary after snapping', () => {
    expect(constrainNodePosition({ x: -24, y: -48 }, bounds)).toEqual({ x: 0, y: 0 });
    expect(constrainNodePosition({ x: 400, y: 280 }, bounds)).toEqual({ x: 300, y: 240 });
  });

  it('finishes only the active pointer and restores the original position on cancellation', () => {
    const dragging = useNodeDragging();
    dragging.startDrag(state);

    expect(dragging.finishDrag(8)).toBe(false);
    expect(dragging.dragState.value).toEqual(state);
    expect(dragging.cancelDrag(7)).toEqual({ x: 48, y: 72 });
    expect(dragging.dragState.value).toBeUndefined();
  });
});
