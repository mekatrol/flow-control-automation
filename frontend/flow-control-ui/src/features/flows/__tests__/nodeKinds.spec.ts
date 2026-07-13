import { describe, expect, it } from 'vitest';

import { flowNodeKinds, nodeKindRegistry } from '../nodeKinds';

describe('node-kind registry', () => {
  it('contains complete rendering, connector, and editor metadata for every supported kind', () => {
    expect(flowNodeKinds).toEqual(['calculator', 'override', 'pulse', 'split']);

    for (const kind of flowNodeKinds) {
      const definition = nodeKindRegistry[kind];
      expect(definition.kind).toBe(kind);
      expect(definition.label).not.toBe('');
      expect(definition.icon).not.toBe('');
      expect(definition.color).toMatch(/^#[\da-f]{6}$/i);
      expect(definition.defaultSize.width).toBeGreaterThan(0);
      expect(definition.defaultSize.height).toBeGreaterThan(0);
      expect(definition.connectors.some(({ direction }) => direction === 'input')).toBe(true);
      expect(definition.connectors.some(({ direction }) => direction === 'output')).toBe(true);
      expect(definition.editor.length).toBeGreaterThan(0);
      expect(Object.keys(definition.defaultConfiguration)).toEqual(
        definition.editor.map(({ key }) => key)
      );
    }
  });
});
