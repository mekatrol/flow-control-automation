import { computed, onBeforeUnmount, onMounted, ref, type ComputedRef, type Ref } from 'vue';

import type { Point } from '../geometry/connectorLayout';

export const DESIGNER_WIDTH = 1100;
export const DESIGNER_HEIGHT = 560;
export const MIN_ZOOM = 0.5;
export const MAX_ZOOM = 2;

export interface ViewportRect {
  left: number;
  top: number;
  width: number;
  height: number;
}

export const clampZoom = (zoom: number): number => Math.min(MAX_ZOOM, Math.max(MIN_ZOOM, zoom));

export const clientToSvgPoint = (
  client: Point,
  rect: ViewportRect,
  viewBox = { x: 0, y: 0, width: DESIGNER_WIDTH, height: DESIGNER_HEIGHT }
): Point => ({
  // Pointer events report CSS pixels in the browser window, while nodes live in
  // the SVG's logical viewBox. Scale each axis through the displayed rectangle
  // so dragging remains accurate when the canvas is zoomed or resized.
  x: viewBox.x + ((client.x - rect.left) / rect.width) * viewBox.width,
  y: viewBox.y + ((client.y - rect.top) / rect.height) * viewBox.height
});

export interface DesignerViewport {
  zoom: Ref<number>;
  width: Ref<number>;
  canvasSize: ComputedRef<{ width: number; height: number }>;
  setZoom: (nextZoom: number) => void;
}

export const useDesignerViewport = (
  element: Ref<HTMLElement | undefined>
): DesignerViewport => {
  const zoom = ref(1);
  const width = ref(0);
  let observer: ResizeObserver | undefined;

  const canvasSize = computed(() => ({
    // Zoom changes the displayed CSS size but leaves graph coordinates fixed.
    // Persisted node positions therefore do not change as the user zooms.
    width: DESIGNER_WIDTH * zoom.value,
    height: DESIGNER_HEIGHT * zoom.value
  }));

  const setZoom = (nextZoom: number): void => {
    zoom.value = clampZoom(nextZoom);
  };

  onMounted(() => {
    if (!element.value) return;
    width.value = element.value.clientWidth;
    // A ResizeObserver also catches layout changes that do not resize the whole
    // browser window, such as an inspector or navigation panel opening.
    observer = new ResizeObserver(([entry]) => {
      if (entry) width.value = entry.contentRect.width;
    });
    observer.observe(element.value);
  });

  // Observers retain their target until disconnected, so release it when Vue
  // removes the designer to avoid work against a detached element.
  onBeforeUnmount(() => observer?.disconnect());

  return { zoom, width, canvasSize, setZoom };
};
