import { describe, expect, it } from 'vitest';

import type { FlowNodeConnector } from '@/features/flows/types';
import { layoutConnectors } from '@/features/flows/geometry/connectorLayout';

const connector = (id: string, side: FlowNodeConnector['side']): FlowNodeConnector => ({
  id,
  label: id,
  direction: side === 'left' || side === 'top' ? 'input' : 'output',
  dataType: 'number',
  side
});

describe('connector layout', () => {
  /**
   * Purpose: Protects the behavioral contract that places connectors on every supported side.
   * Description: Exercises places connectors on every supported side from its arranged starting state and
   * verifies the observable results required by the scenario.
   */
  it('places connectors on every supported side', () => {
    const layouts = layoutConnectors(
      [
        connector('left', 'left'),
        connector('right', 'right'),
        connector('top', 'top'),
        connector('bottom', 'bottom')
      ],
      200,
      100
    );

    // Expected outcome: `layouts.map(({ x, y }) => ({ x, y }))` matches the required structure.
    // Acceptance criteria: `layouts.map(({ x, y }) => ({ x, y }))` must equal `[ { x: 0, y: 50 }, { x: 200, y: 50 }, { x: 100, y: 0 }, { x: 100, y: 100 } ]`, because this condition proves that
    // places connectors on every supported side.
    expect(layouts.map(({ x, y }) => ({ x, y }))).toEqual([
      { x: 0, y: 50 },
      { x: 200, y: 50 },
      { x: 100, y: 0 },
      { x: 100, y: 100 }
    ]);
  });

  /**
   * Purpose: Protects the behavioral contract that spaces multiple connectors evenly along one side.
   * Description: Exercises spaces multiple connectors evenly along one side from its arranged starting state and
   * verifies the observable results required by the scenario.
   */
  it('spaces multiple connectors evenly along one side', () => {
    const layouts = layoutConnectors([connector('a', 'right'), connector('b', 'right')], 210, 90);

    // Expected outcome: `layouts.map(({ x, y }) => ({ x, y }))` matches the required structure.
    // Acceptance criteria: `layouts.map(({ x, y }) => ({ x, y }))` must equal `[ { x: 210, y: 30 }, { x: 210, y: 60 } ]`, because this condition proves that
    // spaces multiple connectors evenly along one side.
    expect(layouts.map(({ x, y }) => ({ x, y }))).toEqual([
      { x: 210, y: 30 },
      { x: 210, y: 60 }
    ]);
  });

  /**
   * Purpose: Ensures densely packed connector pointer targets remain independently reachable.
   * Description: Lays out the selector's three inputs on a compact node and verifies their hit
   * targets meet without overlapping, preventing a lower connector from intercepting the top port.
   */
  it('prevents hit targets from overlapping on a densely populated side', () => {
    const layouts = layoutConnectors(
      [connector('condition', 'left'), connector('a', 'left'), connector('b', 'left')],
      170,
      40
    );

    expect(layouts.map(({ y, hitRadius }) => ({ y, hitRadius }))).toEqual([
      { y: 10, hitRadius: 5 },
      { y: 20, hitRadius: 5 },
      { y: 30, hitRadius: 5 }
    ]);
  });
});
