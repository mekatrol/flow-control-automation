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

    expect(layouts.map(({ x, y }) => ({ x, y }))).toEqual([
      { x: 0, y: 50 },
      { x: 200, y: 50 },
      { x: 100, y: 0 },
      { x: 100, y: 100 }
    ]);
  });

  it('spaces multiple connectors evenly along one side', () => {
    const layouts = layoutConnectors([connector('a', 'right'), connector('b', 'right')], 210, 90);

    expect(layouts.map(({ x, y }) => ({ x, y }))).toEqual([
      { x: 210, y: 30 },
      { x: 210, y: 60 }
    ]);
  });
});
