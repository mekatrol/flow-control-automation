import { describe, expect, it } from 'vitest';

import {
  calculateCanvasSize,
  calculateViewBoxHeight,
  calculateViewBoxWidth,
  clampZoom,
  clientToSvgPoint
} from '@/features/flows/composables/useDesignerViewport';

describe('designer viewport calculations', () => {
  it('clamps zoom to usable boundaries', () => {
    expect(clampZoom(0.1)).toBe(0.5);
    expect(clampZoom(1.25)).toBe(1.25);
    expect(clampZoom(3)).toBe(2);
  });

  it('fits the canvas to the available viewport width', () => {
    expect(calculateCanvasSize(880, 1)).toEqual({
      width: 880,
      height: 448
    });
  });

  it('fills a wide viewport without enlarging the graph', () => {
    expect(calculateCanvasSize(1400, 1)).toEqual({
      width: 1400,
      height: 560
    });
    expect(calculateViewBoxWidth(1400)).toBe(1400);
  });

  it('keeps the full logical graph width on a narrow viewport', () => {
    expect(calculateViewBoxWidth(880)).toBe(1100);
  });

  it('fills a tall viewport without enlarging the graph', () => {
    expect(calculateCanvasSize(1400, 1, 760)).toEqual({
      width: 1400,
      height: 760
    });
    expect(calculateViewBoxHeight(1400, 760)).toBe(760);
  });

  it('keeps vertical scaling proportional on a narrow viewport', () => {
    expect(calculateCanvasSize(880, 1, 600)).toEqual({
      width: 880,
      height: 600
    });
    expect(calculateViewBoxHeight(880, 600)).toBe(750);
  });

  it('applies zoom to the responsive canvas size', () => {
    expect(calculateCanvasSize(880, 1.5)).toEqual({
      width: 1320,
      height: 672
    });
  });

  it('uses the designer dimensions until the viewport has been measured', () => {
    expect(calculateCanvasSize(0, 1)).toEqual({
      width: 1100,
      height: 560
    });
  });

  it('converts client coordinates into SVG coordinates', () => {
    expect(
      clientToSvgPoint({ x: 310, y: 170 }, { left: 10, top: 20, width: 600, height: 300 })
    ).toEqual({
      x: 550,
      y: 280
    });
  });

  it('supports a translated view box', () => {
    expect(
      clientToSvgPoint(
        { x: 60, y: 45 },
        { left: 10, top: 20, width: 100, height: 50 },
        { x: 100, y: 200, width: 400, height: 200 }
      )
    ).toEqual({ x: 300, y: 300 });
  });
});
