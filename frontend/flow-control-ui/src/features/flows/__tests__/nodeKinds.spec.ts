import { describe, expect, it } from 'vitest';

import { flowNodeKinds, nodeKindRegistry } from '../nodeKinds';

describe('node-kind registry', () => {
  it('contains complete rendering, connector, and editor metadata for every supported kind', () => {
    expect(flowNodeKinds).toHaveLength(24);
    expect(flowNodeKinds).toEqual(
      expect.arrayContaining(['and', 'average', 'calculator', 'nand', 'nor', 'not', 'xnor', 'xor'])
    );

    for (const kind of flowNodeKinds) {
      const definition = nodeKindRegistry[kind];
      expect(definition.kind).toBe(kind);
      expect(definition.label).not.toBe('');
      // Function-block assets use the persisted kind as their filename. Keeping
      // this exact mapping prevents the draggable palette from showing another
      // block's otherwise-valid icon after registry edits.
      expect(definition.icon).toBe(kind);
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
