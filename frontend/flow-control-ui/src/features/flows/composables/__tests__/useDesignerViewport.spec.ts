import { describe, expect, it } from 'vitest';

import {
  calculateCanvasSize,
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
