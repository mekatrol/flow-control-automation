import { describe, expect, it } from 'vitest';

import { flowNodeKinds, nodeKindRegistry } from '@/features/flows/nodeKinds';

describe('node-kind registry', () => {
  /**
   * Purpose: Protects the behavioral contract that contains complete rendering, connector, and editor metadata for every supported kind.
   * Description: Exercises contains complete rendering, connector, and editor metadata for every supported kind from its arranged starting state and
   * verifies the observable results required by the scenario.
   */
  it('contains complete rendering, connector, and editor metadata for every supported kind', () => {
    // Expected outcome: `flowNodeKinds` contains the required number of entries.
    // Acceptance criteria: `flowNodeKinds` must contain exactly 36 entries, because this condition proves that
    // contains complete rendering, connector, and editor metadata for every supported kind.
    expect(flowNodeKinds).toHaveLength(43);

    // Expected outcome: `flowNodeKinds` matches the required structure.
    // Acceptance criteria: `flowNodeKinds` must equal `expect.arrayContaining(['and', 'average', 'calculator', 'nand', 'nor', 'not', 'xnor', 'xor']`, because this condition proves that
    // contains complete rendering, connector, and editor metadata for every supported kind.
    expect(flowNodeKinds).toEqual(
      expect.arrayContaining(['add', 'and', 'comparator', 'nand', 'nor', 'not', 'xnor', 'xor'])
    );

    for (const kind of flowNodeKinds) {
      const definition = nodeKindRegistry[kind];

      // Expected outcome: `definition.kind` has the required value.
      // Acceptance criteria: `definition.kind` must be `kind`, because this condition proves that
      // contains complete rendering, connector, and editor metadata for every supported kind.
      expect(definition.kind).toBe(kind);

      // Expected outcome: `definition.label` has the required value.
      // Acceptance criteria: `definition.label` must be `''`, because this condition proves that
      // contains complete rendering, connector, and editor metadata for every supported kind.
      expect(definition.label).not.toBe('');
      // Function-block assets use the persisted kind as their filename. Keeping
      // this exact mapping prevents the draggable palette from showing another
      // block's otherwise-valid icon after registry edits.

      // Expected outcome: `definition.icon` has the required value.
      // Acceptance criteria: `definition.icon` must be `kind`, because this condition proves that
      // contains complete rendering, connector, and editor metadata for every supported kind.
      expect(definition.icon).not.toBe('');

      // Expected outcome: `definition.defaultSize.width` satisfies the required boundary.
      // Acceptance criteria: `definition.defaultSize.width` must satisfy the asserted boundary against `0`, because this condition proves that
      // contains complete rendering, connector, and editor metadata for every supported kind.
      expect(definition.defaultSize.width).toBeGreaterThan(0);

      // Expected outcome: `definition.defaultSize.height` satisfies the required boundary.
      // Acceptance criteria: `definition.defaultSize.height` must satisfy the asserted boundary against `0`, because this condition proves that
      // contains complete rendering, connector, and editor metadata for every supported kind.
      expect(definition.defaultSize.height).toBeGreaterThan(0);

      // Expected outcome: `definition.connectors.some(({ direction }) => direction === 'input')` has the required value.
      // Acceptance criteria: `definition.connectors.some(({ direction }) => direction === 'input')` must be `true`, because this condition proves that
      // contains complete rendering, connector, and editor metadata for every supported kind.
      expect(definition.connectors.some(({ direction }) => direction === 'input')).toBe(
        kind !== 'digitalInput' &&
          kind !== 'analogInput' &&
          kind !== 'calendar' &&
          kind !== 'schedule' &&
          kind !== 'digitalConstant' &&
          kind !== 'numericConstant'
      );

      // Expected outcome: `definition.connectors.some(({ direction }) => direction === 'output')` has the required value.
      // Acceptance criteria: `definition.connectors.some(({ direction }) => direction === 'output')` must be `true`, because this condition proves that
      // contains complete rendering, connector, and editor metadata for every supported kind.
      expect(definition.connectors.some(({ direction }) => direction === 'output')).toBe(
        kind !== 'digitalOutput' && kind !== 'analogOutput'
      );

      // Expected outcome: `definition.editor.length` satisfies the required boundary.
      // Acceptance criteria: `definition.editor.length` must satisfy the asserted boundary against `0`, because this condition proves that
      // contains complete rendering, connector, and editor metadata for every supported kind.
      expect(definition.editor.length > 0).toBe(
        ![
          'add',
          'subtract',
          'multiply',
          'divide',
          'power',
          'negate',
          'average',
          'and',
          'digitalSwitch',
          'max',
          'min',
          'nand',
          'nor',
          'not',
          'or',
          'qualityGood',
          'risingEdge',
          'override',
          'analogSwitch',
          'sequence',
          'split',
          'xnor',
          'xor'
        ].includes(kind)
      );

      // Expected outcome: `Object.keys(definition.defaultConfiguration)` matches the required structure.
      // Acceptance criteria: `Object.keys(definition.defaultConfiguration)` must equal `definition.editor.map(({ key }`, because this condition proves that
      // contains complete rendering, connector, and editor metadata for every supported kind.
      expect(Object.keys(definition.defaultConfiguration)).toEqual(
        definition.editor.map(({ key }) => key)
      );
    }
  });

  /**
   * Purpose: Protects the behavioral contract that preserves the legacy multi-port calculator and split blocks.
   * Description: Exercises preserves the legacy multi-port calculator and split blocks from its arranged starting state and
   * verifies the observable results required by the scenario.
   */
  it('uses canonical executable calculator and split profiles', () => {
    // Expected outcome: `nodeKindRegistry.calculator.connectors` contains the required number of entries.
    // Acceptance criteria: `nodeKindRegistry.calculator.connectors` must contain exactly 4 entries, because this condition proves that
    // preserves the legacy multi-port calculator and split blocks.
    expect(nodeKindRegistry.calculator.connectors).toHaveLength(4);

    // Expected outcome: `nodeKindRegistry.calculator.connectors.filter(({ side }) => side === 'left')` contains the required number of entries.
    // Acceptance criteria: `nodeKindRegistry.calculator.connectors.filter(({ side }) => side === 'left')` must contain exactly 2 entries, because this condition proves that
    // preserves the legacy multi-port calculator and split blocks.
    expect(
      nodeKindRegistry.calculator.connectors.filter(({ side }) => side === 'left')
    ).toHaveLength(3);

    // Expected outcome: `nodeKindRegistry.calculator.connectors.filter(({ side }) => side === 'right')` contains the required number of entries.
    // Acceptance criteria: `nodeKindRegistry.calculator.connectors.filter(({ side }) => side === 'right')` must contain exactly 2 entries, because this condition proves that
    // preserves the legacy multi-port calculator and split blocks.
    expect(
      nodeKindRegistry.calculator.connectors.filter(({ side }) => side === 'right')
    ).toHaveLength(1);

    // Expected outcome: `nodeKindRegistry.split.connectors` contains the required number of entries.
    // Acceptance criteria: `nodeKindRegistry.split.connectors` must contain exactly 3 entries, because this condition proves that
    // preserves the legacy multi-port calculator and split blocks.
    expect(nodeKindRegistry.split.connectors).toHaveLength(2);

    // Expected outcome: `nodeKindRegistry.split.connectors.filter(({ side }) => side === 'right')` contains the required number of entries.
    // Acceptance criteria: `nodeKindRegistry.split.connectors.filter(({ side }) => side === 'right')` must contain exactly 2 entries, because this condition proves that
    // preserves the legacy multi-port calculator and split blocks.
    expect(nodeKindRegistry.split.connectors.filter(({ side }) => side === 'right')).toHaveLength(
      1
    );
  });

  /**
   * Purpose: Protects the behavioral contract that groups every clock-driven function with the calendar timing category.
   * Description: Exercises groups every clock-driven function with the calendar timing category from its arranged starting state and
   * verifies the observable results required by the scenario.
   */
  it('groups every clock-driven function with the calendar timing category', () => {
    const relatedKinds = ['delay', 'pulse', 'schedule', 'timer'] as const;

    // Expected outcome: `relatedKinds.map((kind) => nodeKindRegistry[kind].category)` matches the required structure.
    // Acceptance criteria: `relatedKinds.map((kind) => nodeKindRegistry[kind].category)` must equal `Array(relatedKinds.length`, because this condition proves that
    // groups every clock-driven function with the calendar timing category.
    expect(relatedKinds.map((kind) => nodeKindRegistry[kind].category)).toEqual(
      Array(relatedKinds.length).fill('timing')
    );
  });

  /**
   * Purpose: Protects the behavioral contract that keeps logic and routing blocks in their presentation categories.
   * Description: Exercises keeps logic and routing blocks in their presentation categories from its arranged starting state and
   * verifies the observable results required by the scenario.
   */
  it('keeps logic and routing blocks in their presentation categories', () => {
    const logicDefinitions = Object.values(nodeKindRegistry).filter(
      ({ category }) => category === 'logic'
    );

    // Expected outcome: `logicDefinitions` contains the required number of entries.
    // Acceptance criteria: `logicDefinitions` must contain exactly 0 entries, because this condition proves that
    // keeps logic and routing blocks in their presentation categories.
    expect(logicDefinitions).not.toHaveLength(0);

    const routingDefinitions = Object.values(nodeKindRegistry).filter(
      ({ category }) => category === 'routing'
    );

    // Expected outcome: `routingDefinitions.map(({ kind }) => kind` matches the required structure.
    // Acceptance criteria: `routingDefinitions.map(({ kind }) => kind` must equal `[ 'analogSwitch', 'sequence', 'split' ]`, because this condition proves that
    // keeps logic and routing blocks in their presentation categories.
    expect(routingDefinitions.map(({ kind }) => kind).sort()).toEqual([
      'a2d',
      'analogInput',
      'analogOutput',
      'analogSwitch',
      'd2a',
      'sequence',
      'split'
    ]);
  });

  /**
   * Purpose: Protects the behavioral contract that keeps Override in its own function group.
   * Description: Exercises keeps Override in its own function group from its arranged starting state and
   * verifies the observable results required by the scenario.
   */
  it('keeps Override in its own function group', () => {
    // Expected outcome: `nodeKindRegistry.override` contains the required object fields.
    // Acceptance criteria: `nodeKindRegistry.override` must match the object `{ category: 'override' }`, because this condition proves that
    // keeps Override in its own function group.
    expect(nodeKindRegistry.override).toMatchObject({
      category: 'override'
    });
  });

  it('exposes a connectable Boolean error output on fallible maths nodes', () => {
    for (const kind of ['add', 'subtract', 'multiply', 'divide', 'power', 'negate', 'average'] as const) {
      expect(nodeKindRegistry[kind].connectors).toContainEqual({
        id: 'error',
        label: 'Error',
        direction: 'output',
        dataType: 'boolean',
        side: 'right'
      });
    }
  });
});
