import { computed, onBeforeUnmount, onMounted, ref, type ComputedRef, type Ref } from 'vue';

import type { Point } from '@/features/flows/geometry/connectorLayout';

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

export const calculateCanvasSize = (
  viewportWidth: number,
  zoom: number,
  viewportHeight = 0
): { width: number; height: number } => {
  const availableWidth = viewportWidth > 0 ? viewportWidth : DESIGNER_WIDTH;
  const responsiveScale = Math.min(1, availableWidth / DESIGNER_WIDTH);
  const availableHeight =
    viewportHeight > 0 ? viewportHeight : DESIGNER_HEIGHT * responsiveScale;
  const viewBoxHeight = Math.max(DESIGNER_HEIGHT, availableHeight / responsiveScale);

  return {
    width: availableWidth * clampZoom(zoom),
    height: viewBoxHeight * responsiveScale * clampZoom(zoom)
  };
};

export const calculateViewBoxWidth = (viewportWidth: number): number =>
  Math.max(DESIGNER_WIDTH, viewportWidth);

export const calculateViewBoxHeight = (viewportWidth: number, viewportHeight: number): number => {
  const availableWidth = viewportWidth > 0 ? viewportWidth : DESIGNER_WIDTH;
  const availableHeight = viewportHeight > 0 ? viewportHeight : DESIGNER_HEIGHT;
  const responsiveScale = Math.min(1, availableWidth / DESIGNER_WIDTH);
  return Math.max(DESIGNER_HEIGHT, availableHeight / responsiveScale);
};

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
  height: Ref<number>;
  canvasSize: ComputedRef<{ width: number; height: number }>;
  viewBoxSize: ComputedRef<{ width: number; height: number }>;
  setZoom: (nextZoom: number) => void;
}

export const useDesignerViewport = (element: Ref<HTMLElement | undefined>): DesignerViewport => {
  const zoom = ref(1);
  const width = ref(0);
  const height = ref(0);
  let observer: ResizeObserver | undefined;

  const canvasSize = computed(() => calculateCanvasSize(width.value, zoom.value, height.value));
  const viewBoxSize = computed(() => ({
    width: calculateViewBoxWidth(width.value),
    height: calculateViewBoxHeight(width.value, height.value)
  }));

  const setZoom = (nextZoom: number): void => {
    zoom.value = clampZoom(nextZoom);
  };

  const measureViewport = (): void => {
    if (!element.value) return;
    const rect = element.value.getBoundingClientRect();
    width.value = element.value.clientWidth;
    height.value = Math.max(0, window.innerHeight - rect.top);
  };

  onMounted(() => {
    if (!element.value) return;
    measureViewport();
    // A ResizeObserver also catches layout changes that do not resize the whole
    // browser window, such as an inspector or navigation panel opening.
    observer = new ResizeObserver(measureViewport);
    observer.observe(element.value);
    window.addEventListener('resize', measureViewport);
  });

  // Observers retain their target until disconnected, so release it when Vue
  // removes the designer to avoid work against a detached element.
  onBeforeUnmount(() => {
    observer?.disconnect();
    window.removeEventListener('resize', measureViewport);
  });

  return { zoom, width, height, canvasSize, viewBoxSize, setZoom };
};
