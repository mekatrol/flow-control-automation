import { describe, expect, it } from 'vitest';

import { flowNodeKinds, nodeKindRegistry } from '@/features/flows/nodeKinds';

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

  it('preserves the legacy multi-port calculator and split blocks', () => {
    expect(nodeKindRegistry.calculator.connectors).toHaveLength(4);
    expect(
      nodeKindRegistry.calculator.connectors.filter(({ side }) => side === 'left')
    ).toHaveLength(2);
    expect(
      nodeKindRegistry.calculator.connectors.filter(({ side }) => side === 'right')
    ).toHaveLength(2);
    expect(nodeKindRegistry.split.connectors).toHaveLength(3);
    expect(nodeKindRegistry.split.connectors.filter(({ side }) => side === 'right')).toHaveLength(
      2
    );
  });

  it('groups every clock-driven function with the calendar timing category', () => {
    const relatedKinds = ['delay', 'pulse', 'schedule', 'timer'] as const;
    expect(relatedKinds.map((kind) => nodeKindRegistry[kind].category)).toEqual(
      Array(relatedKinds.length).fill('timing')
    );
  });

  it('keeps logic and routing blocks in their presentation categories', () => {
    const logicDefinitions = Object.values(nodeKindRegistry).filter(
      ({ category }) => category === 'logic'
    );
    expect(logicDefinitions).not.toHaveLength(0);

    const routingDefinitions = Object.values(nodeKindRegistry).filter(
      ({ category }) => category === 'routing'
    );
    expect(routingDefinitions.map(({ kind }) => kind).sort()).toEqual([
      'selector',
      'sequence',
      'split'
    ]);
  });

  it('keeps Override in its own function group', () => {
    expect(nodeKindRegistry.override).toMatchObject({
      category: 'override'
    });
  });
});
