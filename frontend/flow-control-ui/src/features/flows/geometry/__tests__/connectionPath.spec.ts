import { describe, expect, it } from 'vitest';

import { connectionPath } from '@/features/flows/geometry/connectionPath';

describe('connection path', () => {

  /**
   * Purpose: Protects the behavioral contract that draws forward and reverse horizontal splines.
   * Description: Exercises draws forward and reverse horizontal splines from its arranged starting state and
   * verifies the observable results required by the scenario.
   */
  it('draws forward and reverse horizontal splines', () => {

    // Expected outcome: `connectionPath({ x: 0, y: 10 }, { x: 200, y: 30 })` has the required value.
    // Acceptance criteria: `connectionPath({ x: 0, y: 10 }, { x: 200, y: 30 })` must be `'M 0 10 C 100 10, 100 30, 200 30'`, because this condition proves that
    // draws forward and reverse horizontal splines.
    expect(connectionPath({ x: 0, y: 10 }, { x: 200, y: 30 })).toBe(
      'M 0 10 C 100 10, 100 30, 200 30'
    );

    // Expected outcome: `connectionPath({ x: 200, y: 10 }, { x: 0, y: 30 })` has the required value.
    // Acceptance criteria: `connectionPath({ x: 200, y: 10 }, { x: 0, y: 30 })` must be `'M 200 10 C 288.3883476483185 10, -88.38834764831844 30, 0 30'`, because this condition proves that
    // draws forward and reverse horizontal splines.
    expect(connectionPath({ x: 200, y: 10 }, { x: 0, y: 30 })).toBe(
      'M 200 10 C 288.3883476483185 10, -88.38834764831844 30, 0 30'
    );
  });

  /**
   * Purpose: Protects the behavioral contract that routes from the side of vertically arranged connectors.
   * Description: Exercises routes from the side of vertically arranged connectors from its arranged starting state and
   * verifies the observable results required by the scenario.
   */
  it('routes from the side of vertically arranged connectors', () => {

    // Expected outcome: `connectionPath({ x: 10, y: 0 }, { x: 30, y: 200 }, 'bottom', 'top')` has the required value.
    // Acceptance criteria: `connectionPath({ x: 10, y: 0 }, { x: 30, y: 200 }, 'bottom', 'top')` must be `'M 10 0 C 10 100, 30 100, 30 200'`, because this condition proves that
    // routes from the side of vertically arranged connectors.
    expect(connectionPath({ x: 10, y: 0 }, { x: 30, y: 200 }, 'bottom', 'top')).toBe(
      'M 10 0 C 10 100, 30 100, 30 200'
    );
  });

  /**
   * Purpose: Protects the behavioral contract that supports connectors on different sides.
   * Description: Exercises supports connectors on different sides from its arranged starting state and
   * verifies the observable results required by the scenario.
   */
  it('supports connectors on different sides', () => {

    // Expected outcome: `connectionPath({ x: 0, y: 100 }, { x: 200, y: 0 }, 'left', 'bottom')` has the required value.
    // Acceptance criteria: `connectionPath({ x: 0, y: 100 }, { x: 200, y: 0 }, 'left', 'bottom')` must be `'M 0 100 C -88.38834764831844 100, 200 50, 200 0'`, because this condition proves that
    // supports connectors on different sides.
    expect(connectionPath({ x: 0, y: 100 }, { x: 200, y: 0 }, 'left', 'bottom')).toBe(
      'M 0 100 C -88.38834764831844 100, 200 50, 200 0'
    );
  });

  /**
   * Purpose: Protects the behavioral contract that returns no path for a missing endpoint.
   * Description: Exercises returns no path for a missing endpoint from its arranged starting state and
   * verifies the observable results required by the scenario.
   */
  it('returns no path for a missing endpoint', () => {

    // Expected outcome: `connectionPath({ x: 0, y: 0 }, undefined)` has the required value.
    // Acceptance criteria: `connectionPath({ x: 0, y: 0 }, undefined)` must be `''`, because this condition proves that
    // returns no path for a missing endpoint.
    expect(connectionPath({ x: 0, y: 0 }, undefined)).toBe('');

    // Expected outcome: `connectionPath(undefined, { x: 0, y: 0 })` has the required value.
    // Acceptance criteria: `connectionPath(undefined, { x: 0, y: 0 })` must be `''`, because this condition proves that
    // returns no path for a missing endpoint.
    expect(connectionPath(undefined, { x: 0, y: 0 })).toBe('');
  });
});
