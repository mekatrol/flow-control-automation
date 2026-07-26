import { describe, expect, it } from 'vitest';

import { sampleFlows } from '@/features/flows/__tests__/fixtures/sampleFlows';
import { canReorderNode, reorderNode, type ZOrderCommand } from '@/features/flows/graph/zOrder';

const ids = (command: ZOrderCommand): string[] =>
  reorderNode(sampleFlows[0]!.nodes, 'comfort-pulse', command).map((node) => node.id);

describe('z-order operations', () => {

  /**
   * Purpose: Protects the behavioral contract that moves a node to the front, forward, backward, and back.
   * Description: Exercises moves a node to the front, forward, backward, and back from its arranged starting state and
   * verifies the observable results required by the scenario.
   */
  it('moves a node to the front, forward, backward, and back', () => {

    // Expected outcome: `ids('front')` matches the required structure.
    // Acceptance criteria: `ids('front')` must equal `[ 'temperature-average', 'manual-override', 'zone-split', 'comfort-pulse' ]`, because this condition proves that
    // moves a node to the front, forward, backward, and back.
    expect(ids('front')).toEqual([
      'temperature-average',
      'manual-override',
      'zone-split',
      'comfort-pulse'
    ]);

    // Expected outcome: `ids('forward')` matches the required structure.
    // Acceptance criteria: `ids('forward')` must equal `[ 'temperature-average', 'manual-override', 'comfort-pulse', 'zone-split' ]`, because this condition proves that
    // moves a node to the front, forward, backward, and back.
    expect(ids('forward')).toEqual([
      'temperature-average',
      'manual-override',
      'comfort-pulse',
      'zone-split'
    ]);

    // Expected outcome: `ids('backward')` matches the required structure.
    // Acceptance criteria: `ids('backward')` must equal `[ 'comfort-pulse', 'temperature-average', 'manual-override', 'zone-split' ]`, because this condition proves that
    // moves a node to the front, forward, backward, and back.
    expect(ids('backward')).toEqual([
      'comfort-pulse',
      'temperature-average',
      'manual-override',
      'zone-split'
    ]);

    // Expected outcome: `ids('back')` matches the required structure.
    // Acceptance criteria: `ids('back')` must equal `[ 'comfort-pulse', 'temperature-average', 'manual-override', 'zone-split' ]`, because this condition proves that
    // moves a node to the front, forward, backward, and back.
    expect(ids('back')).toEqual([
      'comfort-pulse',
      'temperature-average',
      'manual-override',
      'zone-split'
    ]);
  });

  /**
   * Purpose: Protects the behavioral contract that returns immutable nodes with normalised order values.
   * Description: Exercises returns immutable nodes with normalised order values from its arranged starting state and
   * verifies the observable results required by the scenario.
   */
  it('returns immutable nodes with normalised order values', () => {
    const source = sampleFlows[0]!.nodes;
    const result = reorderNode(source, 'comfort-pulse', 'front');

    // Expected outcome: `result` has the required value.
    // Acceptance criteria: `result` must be `source`, because this condition proves that
    // returns immutable nodes with normalised order values.
    expect(result).not.toBe(source);

    // Expected outcome: `result.map((node) => node.zOrder)` matches the required structure.
    // Acceptance criteria: `result.map((node) => node.zOrder)` must equal `[0, 1, 2, 3]`, because this condition proves that
    // returns immutable nodes with normalised order values.
    expect(result.map((node) => node.zOrder)).toEqual([0, 1, 2, 3]);

    // Expected outcome: `source.map((node) => node.zOrder)` matches the required structure.
    // Acceptance criteria: `source.map((node) => node.zOrder)` must equal `[0, 1, 2, 3]`, because this condition proves that
    // returns immutable nodes with normalised order values.
    expect(source.map((node) => node.zOrder)).toEqual([0, 1, 2, 3]);
  });

  /**
   * Purpose: Protects the behavioral contract that reports and preserves boundary no-ops.
   * Description: Exercises reports and preserves boundary no-ops from its arranged starting state and
   * verifies the observable results required by the scenario.
   */
  it('reports and preserves boundary no-ops', () => {
    const nodes = sampleFlows[0]!.nodes;

    // Expected outcome: `canReorderNode(nodes, 'temperature-average', 'back')` has the required value.
    // Acceptance criteria: `canReorderNode(nodes, 'temperature-average', 'back')` must be `false`, because this condition proves that
    // reports and preserves boundary no-ops.
    expect(canReorderNode(nodes, 'temperature-average', 'back')).toBe(false);

    // Expected outcome: `canReorderNode(nodes, 'zone-split', 'front')` has the required value.
    // Acceptance criteria: `canReorderNode(nodes, 'zone-split', 'front')` must be `false`, because this condition proves that
    // reports and preserves boundary no-ops.
    expect(canReorderNode(nodes, 'zone-split', 'front')).toBe(false);

    // Expected outcome: `canReorderNode(nodes, 'missing', 'front')` has the required value.
    // Acceptance criteria: `canReorderNode(nodes, 'missing', 'front')` must be `false`, because this condition proves that
    // reports and preserves boundary no-ops.
    expect(canReorderNode(nodes, 'missing', 'front')).toBe(false);

    // Expected outcome: `reorderNode(nodes, 'temperature-average', 'back')` has the required value.
    // Acceptance criteria: `reorderNode(nodes, 'temperature-average', 'back')` must be `nodes`, because this condition proves that
    // reports and preserves boundary no-ops.
    expect(reorderNode(nodes, 'temperature-average', 'back')).toBe(nodes);

    // Expected outcome: `reorderNode(nodes, 'missing', 'front')` has the required value.
    // Acceptance criteria: `reorderNode(nodes, 'missing', 'front')` must be `nodes`, because this condition proves that
    // reports and preserves boundary no-ops.
    expect(reorderNode(nodes, 'missing', 'front')).toBe(nodes);
  });
});
