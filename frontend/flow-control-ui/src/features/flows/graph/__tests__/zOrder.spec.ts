import { describe, expect, it } from 'vitest';

import { sampleFlows } from '../../__tests__/fixtures/sampleFlows';
import { canReorderNode, reorderNode, type ZOrderCommand } from '../zOrder';

const ids = (command: ZOrderCommand) =>
  reorderNode(sampleFlows[0]!.nodes, 'comfort-pulse', command).map((node) => node.id);

describe('z-order operations', () => {
  it('moves a node to the front, forward, backward, and back', () => {
    expect(ids('front')).toEqual([
      'temperature-average',
      'manual-override',
      'zone-split',
      'comfort-pulse'
    ]);
    expect(ids('forward')).toEqual([
      'temperature-average',
      'manual-override',
      'comfort-pulse',
      'zone-split'
    ]);
    expect(ids('backward')).toEqual([
      'comfort-pulse',
      'temperature-average',
      'manual-override',
      'zone-split'
    ]);
    expect(ids('back')).toEqual([
      'comfort-pulse',
      'temperature-average',
      'manual-override',
      'zone-split'
    ]);
  });

  it('returns immutable nodes with normalised order values', () => {
    const source = sampleFlows[0]!.nodes;
    const result = reorderNode(source, 'comfort-pulse', 'front');

    expect(result).not.toBe(source);
    expect(result.map((node) => node.zOrder)).toEqual([0, 1, 2, 3]);
    expect(source.map((node) => node.zOrder)).toEqual([0, 1, 2, 3]);
  });

  it('reports and preserves boundary no-ops', () => {
    const nodes = sampleFlows[0]!.nodes;
    expect(canReorderNode(nodes, 'temperature-average', 'back')).toBe(false);
    expect(canReorderNode(nodes, 'zone-split', 'front')).toBe(false);
    expect(canReorderNode(nodes, 'missing', 'front')).toBe(false);
    expect(reorderNode(nodes, 'temperature-average', 'back')).toBe(nodes);
    expect(reorderNode(nodes, 'missing', 'front')).toBe(nodes);
  });
});
