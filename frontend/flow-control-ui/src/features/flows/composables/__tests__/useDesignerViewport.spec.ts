import { describe, expect, it } from 'vitest';

import {
  calculateCanvasSize,
  calculateViewBoxHeight,
  calculateViewBoxWidth,
  clampZoom,
  clientToSvgPoint
} from '@/features/flows/composables/useDesignerViewport';

describe('designer viewport calculations', () => {
  /**
   * Purpose: Protects the behavioral contract that clamps zoom to usable boundaries.
   * Description: Exercises clamps zoom to usable boundaries from its arranged starting state and
   * verifies the observable results required by the scenario.
   */
  it('clamps zoom to usable boundaries', () => {
    // Expected outcome: `clampZoom(0.1)` has the required value.
    // Acceptance criteria: `clampZoom(0.1)` must be `0.5`, because this condition proves that
    // clamps zoom to usable boundaries.
    expect(clampZoom(0.1)).toBe(0.5);

    // Expected outcome: `clampZoom(1.25)` has the required value.
    // Acceptance criteria: `clampZoom(1.25)` must be `1.25`, because this condition proves that
    // clamps zoom to usable boundaries.
    expect(clampZoom(1.25)).toBe(1.25);

    // Expected outcome: `clampZoom(3)` has the required value.
    // Acceptance criteria: `clampZoom(3)` must be `2`, because this condition proves that
    // clamps zoom to usable boundaries.
    expect(clampZoom(3)).toBe(2);
  });

  /**
   * Purpose: Protects the behavioral contract that fits the canvas to the available viewport width.
   * Description: Exercises fits the canvas to the available viewport width from its arranged starting state and
   * verifies the observable results required by the scenario.
   */
  it('fits the canvas to the available viewport width', () => {
    // Expected outcome: `calculateCanvasSize(880, 1)` matches the required structure.
    // Acceptance criteria: `calculateCanvasSize(880, 1)` must equal `{ width: 880, height: 448 }`, because this condition proves that
    // fits the canvas to the available viewport width.
    expect(calculateCanvasSize(880, 1)).toEqual({
      width: 880,
      height: 448
    });
  });

  /**
   * Purpose: Protects the behavioral contract that fills a wide viewport without enlarging the graph.
   * Description: Exercises fills a wide viewport without enlarging the graph from its arranged starting state and
   * verifies the observable results required by the scenario.
   */
  it('fills a wide viewport without enlarging the graph', () => {
    // Expected outcome: `calculateCanvasSize(1400, 1)` matches the required structure.
    // Acceptance criteria: `calculateCanvasSize(1400, 1)` must equal `{ width: 1400, height: 560 }`, because this condition proves that
    // fills a wide viewport without enlarging the graph.
    expect(calculateCanvasSize(1400, 1)).toEqual({
      width: 1400,
      height: 560
    });

    // Expected outcome: `calculateViewBoxWidth(1400)` has the required value.
    // Acceptance criteria: `calculateViewBoxWidth(1400)` must be `1400`, because this condition proves that
    // fills a wide viewport without enlarging the graph.
    expect(calculateViewBoxWidth(1400)).toBe(1400);
  });

  /**
   * Purpose: Protects the behavioral contract that keeps the full logical graph width on a narrow viewport.
   * Description: Exercises keeps the full logical graph width on a narrow viewport from its arranged starting state and
   * verifies the observable results required by the scenario.
   */
  it('keeps the full logical graph width on a narrow viewport', () => {
    // Expected outcome: `calculateViewBoxWidth(880)` has the required value.
    // Acceptance criteria: `calculateViewBoxWidth(880)` must be `1100`, because this condition proves that
    // keeps the full logical graph width on a narrow viewport.
    expect(calculateViewBoxWidth(880)).toBe(1100);
  });

  /**
   * Purpose: Protects the behavioral contract that fills a tall viewport without enlarging the graph.
   * Description: Exercises fills a tall viewport without enlarging the graph from its arranged starting state and
   * verifies the observable results required by the scenario.
   */
  it('fills a tall viewport without enlarging the graph', () => {
    // Expected outcome: `calculateCanvasSize(1400, 1, 760)` matches the required structure.
    // Acceptance criteria: `calculateCanvasSize(1400, 1, 760)` must equal `{ width: 1400, height: 760 }`, because this condition proves that
    // fills a tall viewport without enlarging the graph.
    expect(calculateCanvasSize(1400, 1, 760)).toEqual({
      width: 1400,
      height: 760
    });

    // Expected outcome: `calculateViewBoxHeight(1400, 760)` has the required value.
    // Acceptance criteria: `calculateViewBoxHeight(1400, 760)` must be `760`, because this condition proves that
    // fills a tall viewport without enlarging the graph.
    expect(calculateViewBoxHeight(1400, 760)).toBe(760);
  });

  /**
   * Purpose: Protects the behavioral contract that keeps vertical scaling proportional on a narrow viewport.
   * Description: Exercises keeps vertical scaling proportional on a narrow viewport from its arranged starting state and
   * verifies the observable results required by the scenario.
   */
  it('keeps vertical scaling proportional on a narrow viewport', () => {
    // Expected outcome: `calculateCanvasSize(880, 1, 600)` matches the required structure.
    // Acceptance criteria: `calculateCanvasSize(880, 1, 600)` must equal `{ width: 880, height: 600 }`, because this condition proves that
    // keeps vertical scaling proportional on a narrow viewport.
    expect(calculateCanvasSize(880, 1, 600)).toEqual({
      width: 880,
      height: 600
    });

    // Expected outcome: `calculateViewBoxHeight(880, 600)` has the required value.
    // Acceptance criteria: `calculateViewBoxHeight(880, 600)` must be `750`, because this condition proves that
    // keeps vertical scaling proportional on a narrow viewport.
    expect(calculateViewBoxHeight(880, 600)).toBe(750);
  });

  /**
   * Purpose: Protects the behavioral contract that applies zoom to the responsive canvas size.
   * Description: Exercises applies zoom to the responsive canvas size from its arranged starting state and
   * verifies the observable results required by the scenario.
   */
  it('applies zoom to the responsive canvas size', () => {
    // Expected outcome: `calculateCanvasSize(880, 1.5)` matches the required structure.
    // Acceptance criteria: `calculateCanvasSize(880, 1.5)` must equal `{ width: 1320, height: 672 }`, because this condition proves that
    // applies zoom to the responsive canvas size.
    expect(calculateCanvasSize(880, 1.5)).toEqual({
      width: 1320,
      height: 672
    });
  });

  /**
   * Purpose: Protects the behavioral contract that uses the designer dimensions until the viewport has been measured.
   * Description: Exercises uses the designer dimensions until the viewport has been measured from its arranged starting state and
   * verifies the observable results required by the scenario.
   */
  it('uses the designer dimensions until the viewport has been measured', () => {
    // Expected outcome: `calculateCanvasSize(0, 1)` matches the required structure.
    // Acceptance criteria: `calculateCanvasSize(0, 1)` must equal `{ width: 1100, height: 560 }`, because this condition proves that
    // uses the designer dimensions until the viewport has been measured.
    expect(calculateCanvasSize(0, 1)).toEqual({
      width: 1100,
      height: 560
    });
  });

  /**
   * Purpose: Protects the behavioral contract that converts client coordinates into SVG coordinates.
   * Description: Exercises converts client coordinates into SVG coordinates from its arranged starting state and
   * verifies the observable results required by the scenario.
   */
  it('converts client coordinates into SVG coordinates', () => {
    // Expected outcome: `clientToSvgPoint({ x: 310, y: 170 }, { left: 10, top: 20, width: 600, height: 300 })` matches the required structure.
    // Acceptance criteria: `clientToSvgPoint({ x: 310, y: 170 }, { left: 10, top: 20, width: 600, height: 300 })` must equal `{ x: 550, y: 280 }`, because this condition proves that
    // converts client coordinates into SVG coordinates.
    expect(
      clientToSvgPoint({ x: 310, y: 170 }, { left: 10, top: 20, width: 600, height: 300 })
    ).toEqual({
      x: 550,
      y: 280
    });
  });

  /**
   * Purpose: Protects the behavioral contract that supports a translated view box.
   * Description: Exercises supports a translated view box from its arranged starting state and
   * verifies the observable results required by the scenario.
   */
  it('supports a translated view box', () => {
    // Expected outcome: `clientToSvgPoint( { x: 60, y: 45 }, { left: 10, top: 20, width: 100, height: 50 }, { x: 100, y: 200,` matches the required structure.
    // Acceptance criteria: `clientToSvgPoint( { x: 60, y: 45 }, { left: 10, top: 20, width: 100, height: 50 }, { x: 100, y: 200,` must equal `{ x: 300, y: 300 }`, because this condition proves that
    // supports a translated view box.
    expect(
      clientToSvgPoint(
        { x: 60, y: 45 },
        { left: 10, top: 20, width: 100, height: 50 },
        { x: 100, y: 200, width: 400, height: 200 }
      )
    ).toEqual({ x: 300, y: 300 });
  });
});
