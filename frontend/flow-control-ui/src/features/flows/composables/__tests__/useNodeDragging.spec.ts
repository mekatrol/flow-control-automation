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
  /**
   * Purpose: Protects the behavioral contract that calculates a snapped drag delta.
   * Description: Exercises calculates a snapped drag delta from its arranged starting state and
   * verifies the observable results required by the scenario.
   */
  it('calculates a snapped drag delta', () => {
    // Expected outcome: `calculateDraggedPosition(state, { x: 131, y: 143 }, bounds, 24, true)` matches the required structure.
    // Acceptance criteria: `calculateDraggedPosition(state, { x: 131, y: 143 }, bounds, 24, true)` must equal `{ x: 72, y: 120 }`, because this condition proves that
    // calculates a snapped drag delta.
    expect(calculateDraggedPosition(state, { x: 131, y: 143 }, bounds, 24, true)).toEqual({
      x: 72,
      y: 120
    });
  });

  /**
   * Purpose: Protects the behavioral contract that supports disabled snapping and positive and negative grid values.
   * Description: Exercises supports disabled snapping and positive and negative grid values from its arranged starting state and
   * verifies the observable results required by the scenario.
   */
  it('supports disabled snapping and positive and negative grid values', () => {
    // Expected outcome: `snapCoordinate(35, 24, true)` has the required value.
    // Acceptance criteria: `snapCoordinate(35, 24, true)` must be `24`, because this condition proves that
    // supports disabled snapping and positive and negative grid values.
    expect(snapCoordinate(35, 24, true)).toBe(24);

    // Expected outcome: `snapCoordinate(-35, 24, true)` has the required value.
    // Acceptance criteria: `snapCoordinate(-35, 24, true)` must be `-24`, because this condition proves that
    // supports disabled snapping and positive and negative grid values.
    expect(snapCoordinate(-35, 24, true)).toBe(-24);

    // Expected outcome: `snapCoordinate(35, 24, false)` has the required value.
    // Acceptance criteria: `snapCoordinate(35, 24, false)` must be `35`, because this condition proves that
    // supports disabled snapping and positive and negative grid values.
    expect(snapCoordinate(35, 24, false)).toBe(35);
  });

  /**
   * Purpose: Protects the behavioral contract that clamps nodes at every canvas boundary after snapping.
   * Description: Exercises clamps nodes at every canvas boundary after snapping from its arranged starting state and
   * verifies the observable results required by the scenario.
   */
  it('clamps nodes at every canvas boundary after snapping', () => {
    // Expected outcome: `constrainNodePosition({ x: -24, y: -48 }, bounds)` matches the required structure.
    // Acceptance criteria: `constrainNodePosition({ x: -24, y: -48 }, bounds)` must equal `{ x: 0, y: 0 }`, because this condition proves that
    // clamps nodes at every canvas boundary after snapping.
    expect(constrainNodePosition({ x: -24, y: -48 }, bounds)).toEqual({ x: 0, y: 0 });

    // Expected outcome: `constrainNodePosition({ x: 400, y: 280 }, bounds)` matches the required structure.
    // Acceptance criteria: `constrainNodePosition({ x: 400, y: 280 }, bounds)` must equal `{ x: 300, y: 240 }`, because this condition proves that
    // clamps nodes at every canvas boundary after snapping.
    expect(constrainNodePosition({ x: 400, y: 280 }, bounds)).toEqual({ x: 300, y: 240 });
  });

  /**
   * Purpose: Protects the behavioral contract that finishes only the active pointer and restores the original position on cancellation.
   * Description: Exercises finishes only the active pointer and restores the original position on cancellation from its arranged starting state and
   * verifies the observable results required by the scenario.
   */
  it('finishes only the active pointer and restores the original position on cancellation', () => {
    const dragging = useNodeDragging();
    dragging.startDrag(state);

    // Expected outcome: `dragging.finishDrag(8)` has the required value.
    // Acceptance criteria: `dragging.finishDrag(8)` must be `false`, because this condition proves that
    // finishes only the active pointer and restores the original position on cancellation.
    expect(dragging.finishDrag(8)).toBe(false);

    // Expected outcome: `dragging.dragState.value` matches the required structure.
    // Acceptance criteria: `dragging.dragState.value` must equal `state`, because this condition proves that
    // finishes only the active pointer and restores the original position on cancellation.
    expect(dragging.dragState.value).toEqual(state);

    // Expected outcome: `dragging.cancelDrag(7)` matches the required structure.
    // Acceptance criteria: `dragging.cancelDrag(7)` must equal `{ x: 48, y: 72 }`, because this condition proves that
    // finishes only the active pointer and restores the original position on cancellation.
    expect(dragging.cancelDrag(7)).toEqual({ x: 48, y: 72 });

    // Expected outcome: `dragging.dragState.value` is not supplied.
    // Acceptance criteria: `dragging.dragState.value` must be undefined, because this condition proves that
    // finishes only the active pointer and restores the original position on cancellation.
    expect(dragging.dragState.value).toBeUndefined();
  });
});
