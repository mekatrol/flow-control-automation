import { ref } from 'vue';

import type { Point } from '../geometry/connectorLayout';

export interface NodeDragState {
  nodeId: string;
  pointerId: number;
  pointerStart: Point;
  nodeStart: Point;
}

export interface DragBounds {
  width: number;
  height: number;
  nodeWidth: number;
  nodeHeight: number;
}

export const snapCoordinate = (value: number, gridSize: number, enabled: boolean): number =>
  enabled ? Math.round(value / gridSize) * gridSize : value;

export const constrainNodePosition = (position: Point, bounds: DragBounds): Point => ({
  x: Math.min(Math.max(0, position.x), Math.max(0, bounds.width - bounds.nodeWidth)),
  y: Math.min(Math.max(0, position.y), Math.max(0, bounds.height - bounds.nodeHeight))
});

export const calculateDraggedPosition = (
  state: NodeDragState,
  pointer: Point,
  bounds: DragBounds,
  gridSize: number,
  snapEnabled: boolean
): Point =>
  // Always calculate from the gesture's starting positions. Reusing the last
  // snapped position would accumulate rounding error as the pointer moves.
  constrainNodePosition(
    {
      x: snapCoordinate(
        state.nodeStart.x + pointer.x - state.pointerStart.x,
        gridSize,
        snapEnabled
      ),
      y: snapCoordinate(state.nodeStart.y + pointer.y - state.pointerStart.y, gridSize, snapEnabled)
    },
    bounds
  );

export const useNodeDragging = () => {
  const dragState = ref<NodeDragState>();

  const startDrag = (state: NodeDragState): void => {
    dragState.value = state;
  };

  const finishDrag = (pointerId: number): boolean => {
    // Pointer identifiers distinguish simultaneous mouse, pen, and touch input;
    // an unrelated pointer must not finish the active node gesture.
    if (dragState.value?.pointerId !== pointerId) return false;
    dragState.value = undefined;
    return true;
  };

  const cancelDrag = (pointerId: number): Point | undefined => {
    if (dragState.value?.pointerId !== pointerId) return undefined;
    const originalPosition = dragState.value.nodeStart;
    // Browser cancellation means the gesture did not complete reliably, so the
    // canvas restores the node rather than persisting an accidental partial move.
    dragState.value = undefined;
    return originalPosition;
  };

  return { dragState, startDrag, finishDrag, cancelDrag };
};
