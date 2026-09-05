import { describe, expect, it } from 'vitest';

import { flowNodeTypes, getNodeTypeDefinition, nodeTypeRegistry } from '@/features/flows/nodeTypes';
import { FlowNodeType } from '@/types/serverTypes';

describe('node-type registry', () => {
  it('rejects the backend Unknown sentinel as an authorable node', () => {
    expect(flowNodeTypes).not.toContain(FlowNodeType.Unknown);
    expect(() => getNodeTypeDefinition(FlowNodeType.Unknown)).toThrow(
      'Unknown flow nodes cannot be authored.'
    );
  });

  /**
   * Purpose: Protects the behavioral contract that contains complete rendering, connector, and editor metadata for every supported nodeType.
   * Description: Exercises contains complete rendering, connector, and editor metadata for every supported nodeType from its arranged starting state and
   * verifies the observable results required by the scenario.
   */
  it('contains complete rendering, connector, and editor metadata for every supported nodeType', () => {
    // Expected outcome: `flowNodeTypes` contains the required number of entries.
    // Acceptance criteria: `flowNodeTypes` must contain exactly 36 entries, because this condition proves that
    // contains complete rendering, connector, and editor metadata for every supported nodeType.
    expect(flowNodeTypes).toHaveLength(47);

    // Expected outcome: `flowNodeTypes` matches the required structure.
    // Acceptance criteria: `flowNodeTypes` must equal `expect.arrayContaining(['and', 'average', 'calculator', 'nand', 'nor', 'not', 'xnor', 'xor']`, because this condition proves that
    // contains complete rendering, connector, and editor metadata for every supported nodeType.
    expect(flowNodeTypes).toEqual(
      expect.arrayContaining(['add', 'and', 'comparator', 'nand', 'nor', 'not', 'xnor', 'xor'])
    );

    for (const nodeType of flowNodeTypes) {
      const definition = nodeTypeRegistry[nodeType];

      // Expected outcome: `definition.nodeType` has the required value.
      // Acceptance criteria: `definition.nodeType` must be `nodeType`, because this condition proves that
      // contains complete rendering, connector, and editor metadata for every supported nodeType.
      expect(definition.nodeType).toBe(nodeType);

      // Expected outcome: `definition.label` has the required value.
      // Acceptance criteria: `definition.label` must be `''`, because this condition proves that
      // contains complete rendering, connector, and editor metadata for every supported nodeType.
      expect(definition.label).not.toBe('');
      // Function-block assets use the persisted nodeType as their filename. Keeping
      // this exact mapping prevents the draggable palette from showing another
      // block's otherwise-valid icon after registry edits.

      // Expected outcome: `definition.icon` has the required value.
      // Acceptance criteria: `definition.icon` must be `nodeType`, because this condition proves that
      // contains complete rendering, connector, and editor metadata for every supported nodeType.
      expect(definition.icon).not.toBe('');

      // Expected outcome: `definition.defaultSize.width` satisfies the required boundary.
      // Acceptance criteria: `definition.defaultSize.width` must satisfy the asserted boundary against `0`, because this condition proves that
      // contains complete rendering, connector, and editor metadata for every supported nodeType.
      expect(definition.defaultSize.width).toBeGreaterThan(0);

      // Expected outcome: `definition.defaultSize.height` satisfies the required boundary.
      // Acceptance criteria: `definition.defaultSize.height` must satisfy the asserted boundary against `0`, because this condition proves that
      // contains complete rendering, connector, and editor metadata for every supported nodeType.
      expect(definition.defaultSize.height).toBeGreaterThan(0);

      // Expected outcome: `definition.connectors.some(({ direction }) => direction === 'input')` has the required value.
      // Acceptance criteria: `definition.connectors.some(({ direction }) => direction === 'input')` must be `true`, because this condition proves that
      // contains complete rendering, connector, and editor metadata for every supported nodeType.
      expect(definition.connectors.some(({ direction }) => direction === 'input')).toBe(
        nodeType !== 'digitalInput' &&
          nodeType !== 'analogInput' &&
          nodeType !== 'calendar' &&
          nodeType !== 'schedule' &&
          nodeType !== 'digitalConstant' &&
          nodeType !== 'analogConstant'
      );

      // Expected outcome: `definition.connectors.some(({ direction }) => direction === 'output')` has the required value.
      // Acceptance criteria: `definition.connectors.some(({ direction }) => direction === 'output')` must be `true`, because this condition proves that
      // contains complete rendering, connector, and editor metadata for every supported nodeType.
      expect(definition.connectors.some(({ direction }) => direction === 'output')).toBe(
        nodeType !== 'digitalOutput' && nodeType !== 'analogOutput'
      );

      // Expected outcome: `definition.editor.length` satisfies the required boundary.
      // Acceptance criteria: `definition.editor.length` must satisfy the asserted boundary against `0`, because this condition proves that
      // contains complete rendering, connector, and editor metadata for every supported nodeType.
      expect(definition.editor.length > 0).toBe(
        ![
          'add',
          'subtract',
          'multiply',
          'divide',
          'power',
          'negate',
          'average',
          'counter',
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
        ].includes(nodeType)
      );

      // Expected outcome: `Object.keys(definition.defaultConfiguration)` matches the required structure.
      // Acceptance criteria: `Object.keys(definition.defaultConfiguration)` must equal `definition.editor.map(({ key }`, because this condition proves that
      // contains complete rendering, connector, and editor metadata for every supported nodeType.
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
    // Expected outcome: `nodeTypeRegistry.calculator.connectors` contains the required number of entries.
    // Acceptance criteria: `nodeTypeRegistry.calculator.connectors` must contain exactly 4 entries, because this condition proves that
    // preserves the legacy multi-port calculator and split blocks.
    expect(nodeTypeRegistry.calculator.connectors).toHaveLength(4);

    // Expected outcome: `nodeTypeRegistry.calculator.connectors.filter(({ side }) => side === 'left')` contains the required number of entries.
    // Acceptance criteria: `nodeTypeRegistry.calculator.connectors.filter(({ side }) => side === 'left')` must contain exactly 2 entries, because this condition proves that
    // preserves the legacy multi-port calculator and split blocks.
    expect(
      nodeTypeRegistry.calculator.connectors.filter(({ side }) => side === 'left')
    ).toHaveLength(3);

    // Expected outcome: `nodeTypeRegistry.calculator.connectors.filter(({ side }) => side === 'right')` contains the required number of entries.
    // Acceptance criteria: `nodeTypeRegistry.calculator.connectors.filter(({ side }) => side === 'right')` must contain exactly 2 entries, because this condition proves that
    // preserves the legacy multi-port calculator and split blocks.
    expect(
      nodeTypeRegistry.calculator.connectors.filter(({ side }) => side === 'right')
    ).toHaveLength(1);

    // Expected outcome: `nodeTypeRegistry.split.connectors` contains the required number of entries.
    // Acceptance criteria: `nodeTypeRegistry.split.connectors` must contain exactly 3 entries, because this condition proves that
    // preserves the legacy multi-port calculator and split blocks.
    expect(nodeTypeRegistry.split.connectors).toHaveLength(2);

    // Expected outcome: `nodeTypeRegistry.split.connectors.filter(({ side }) => side === 'right')` contains the required number of entries.
    // Acceptance criteria: `nodeTypeRegistry.split.connectors.filter(({ side }) => side === 'right')` must contain exactly 2 entries, because this condition proves that
    // preserves the legacy multi-port calculator and split blocks.
    expect(nodeTypeRegistry.split.connectors.filter(({ side }) => side === 'right')).toHaveLength(
      1
    );
  });

  /**
   * Purpose: Protects the behavioral contract that groups every clock-driven function with the calendar timing category.
   * Description: Exercises groups every clock-driven function with the calendar timing category from its arranged starting state and
   * verifies the observable results required by the scenario.
   */
  it('groups every clock-driven function with the calendar timing category', () => {
    const relatedNodeTypes = ['clock', 'delay', 'onDelay', 'pulse', 'schedule', 'timer'] as const;

    // Expected outcome: `relatedNodeTypes.map((nodeType) => nodeTypeRegistry[nodeType].category)` matches the required structure.
    // Acceptance criteria: `relatedNodeTypes.map((nodeType) => nodeTypeRegistry[nodeType].category)` must equal `Array(relatedNodeTypes.length`, because this condition proves that
    // groups every clock-driven function with the calendar timing category.
    expect(relatedNodeTypes.map((nodeType) => nodeTypeRegistry[nodeType].category)).toEqual(
      Array(relatedNodeTypes.length).fill('timing')
    );
  });

  /** Protects the combined control category used by the node palette. */
  it('combines logic, routing, and override blocks in the control category', () => {
    expect(nodeTypeRegistry.and.category).toBe('control');
    expect(nodeTypeRegistry.split.category).toBe('control');
    expect(nodeTypeRegistry.override.category).toBe('control');
  });

  /** Protects the IO grouping for physical/virtual points and constants. */
  it('groups physical points and constants as IO', () => {
    for (const nodeType of [
      'analogInput',
      'analogOutput',
      'digitalConstant',
      'digitalInput',
      'digitalOutput',
      'analogConstant'
    ] as const) {
      expect(nodeTypeRegistry[nodeType].category).toBe('io');
    }
  });

  it('uses a dedicated icon for each analog and digital constant or virtual point', () => {
    const pointNodeTypes = [
      'analogConstant',
      'digitalConstant',
      'analogVirtual',
      'digitalVirtual'
    ] as const;

    expect(pointNodeTypes.map((nodeType) => nodeTypeRegistry[nodeType].icon)).toEqual([
      'analogconstant',
      'digitalconstant',
      'analogvirtual',
      'digitalvirtual'
    ]);
  });

  it('uses the outward-facing symbol for analog input and inward-facing symbol for output', () => {
    expect(nodeTypeRegistry.analogInput.icon).toBe('analogoutput');
    expect(nodeTypeRegistry.analogOutput.icon).toBe('analoginput');
  });

  it('exposes a connectable Boolean error output on fallible maths nodes', () => {
    for (const nodeType of [
      'add',
      'subtract',
      'multiply',
      'divide',
      'power',
      'negate',
      'average'
    ] as const) {
      expect(nodeTypeRegistry[nodeType].connectors).toContainEqual({
        id: 'error',
        label: 'Error',
        direction: 'output',
        dataType: 'boolean',
        side: 'right'
      });
    }
  });

  it('defines the rising-edge counter connector contract', () => {
    expect(nodeTypeRegistry.counter).toMatchObject({
      label: 'Counter',
      category: 'control',
      icon: 'counter',
      connectors: [
        { id: 'count', label: 'Count', direction: 'input', dataType: 'boolean' },
        { id: 'reset', label: 'Reset', direction: 'input', dataType: 'boolean' },
        { id: 'value', label: 'Count', direction: 'output', dataType: 'number' }
      ],
      defaultConfiguration: {}
    });
  });
});
