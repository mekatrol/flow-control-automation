import { describe, expect, it } from 'vitest';

import { createDefaultNode } from '@/features/flows/graph/createNode';

describe('default node creation', () => {
  it('creates a serialisable node from registry defaults and a supplied ID', () => {
    const node = createDefaultNode('calculator', { x: 120, y: 144 }, 4, 'node-5');

    expect(node).toMatchObject({
      id: 'node-5',
      kind: 'calculator',
      label: 'New Calculator',
      x: 120,
      y: 144,
      zOrder: 4,
      configuration: { operation: 'average' }
    });
    expect(node.connectors.map(({ id }) => id)).toEqual([
      'analogue-input',
      'digital-input',
      'analogue-output',
      'digital-output'
    ]);
    expect(() => JSON.stringify(node)).not.toThrow();
  });

  it('does not share mutable connector or configuration defaults', () => {
    const first = createDefaultNode('pulse', { x: 0, y: 0 }, 0, 'first');
    const second = createDefaultNode('pulse', { x: 0, y: 0 }, 1, 'second');
    first.connectors[0]!.label = 'Changed';
    first.configuration.durationSeconds = 99;

    expect(second.connectors[0]!.label).not.toBe('Changed');
    expect(second.configuration.durationSeconds).toBe(30);
  });
});
