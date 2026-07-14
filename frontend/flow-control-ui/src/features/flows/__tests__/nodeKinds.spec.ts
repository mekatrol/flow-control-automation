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

  it('uses the calendar colour scheme for every timing block', () => {
    const calendarColor = nodeKindRegistry.calendar.color;
    const relatedKinds = ['delay', 'pulse', 'schedule', 'timer'] as const;
    expect(relatedKinds.map((kind) => nodeKindRegistry[kind].color)).toEqual(
      Array(relatedKinds.length).fill(calendarColor)
    );
  });

  it('uses blue for logic and the former timing amber for routing blocks', () => {
    const logicDefinitions = Object.values(nodeKindRegistry).filter(
      ({ category }) => category === 'logic'
    );
    expect(new Set(logicDefinitions.map(({ color }) => color))).toEqual(new Set(['#64a7ff']));

    const routingDefinitions = Object.values(nodeKindRegistry).filter(
      ({ category }) => category === 'routing'
    );
    expect(routingDefinitions.map(({ kind }) => kind).sort()).toEqual([
      'selector',
      'sequence',
      'split'
    ]);
    expect(new Set(routingDefinitions.map(({ color }) => color))).toEqual(new Set(['#f5b942']));
  });

  it('keeps Override in its own green function group', () => {
    expect(nodeKindRegistry.override).toMatchObject({
      category: 'override',
      color: '#65d6ad'
    });
  });
});
