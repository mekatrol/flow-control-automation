// @vitest-environment jsdom

import { describe, expect, it } from 'vitest';

import { interpretDesignerKey } from '@/features/flows/graph/keyboardCommands';

describe('designer keyboard commands', () => {

  /**
   * Purpose: Protects the behavioral contract that maps arrow, Delete, and Backspace keys.
   * Description: Exercises maps arrow, Delete, and Backspace keys from its arranged starting state and
   * verifies the observable results required by the scenario.
   */
  it('maps arrow, Delete, and Backspace keys', () => {

    // Expected outcome: `interpretDesignerKey({ key: 'ArrowLeft', target: document.body })` matches the required structure.
    // Acceptance criteria: `interpretDesignerKey({ key: 'ArrowLeft', target: document.body })` must equal `{ type: 'move', deltaX: -24, deltaY: 0 }`, because this condition proves that
    // maps arrow, Delete, and Backspace keys.
    expect(interpretDesignerKey({ key: 'ArrowLeft', target: document.body })).toEqual({
      type: 'move',
      deltaX: -24,
      deltaY: 0
    });

    // Expected outcome: `interpretDesignerKey({ key: 'ArrowDown', target: document.body }, 10)` matches the required structure.
    // Acceptance criteria: `interpretDesignerKey({ key: 'ArrowDown', target: document.body }, 10)` must equal `{ type: 'move', deltaX: 0, deltaY: 10 }`, because this condition proves that
    // maps arrow, Delete, and Backspace keys.
    expect(interpretDesignerKey({ key: 'ArrowDown', target: document.body }, 10)).toEqual({
      type: 'move',
      deltaX: 0,
      deltaY: 10
    });

    // Expected outcome: `interpretDesignerKey({ key: 'Delete', target: document.body })` matches the required structure.
    // Acceptance criteria: `interpretDesignerKey({ key: 'Delete', target: document.body })` must equal `{ type: 'delete' }`, because this condition proves that
    // maps arrow, Delete, and Backspace keys.
    expect(interpretDesignerKey({ key: 'Delete', target: document.body })).toEqual({
      type: 'delete'
    });

    // Expected outcome: `interpretDesignerKey({ key: 'Backspace', target: document.body })` matches the required structure.
    // Acceptance criteria: `interpretDesignerKey({ key: 'Backspace', target: document.body })` must equal `{ type: 'delete' }`, because this condition proves that
    // maps arrow, Delete, and Backspace keys.
    expect(interpretDesignerKey({ key: 'Backspace', target: document.body })).toEqual({
      type: 'delete'
    });
  });

  /**
   * Purpose: Protects the behavioral contract that ignores unrelated keys.
   * Description: Exercises ignores unrelated keys from its arranged starting state and
   * verifies the observable results required by the scenario.
   */
  it('ignores unrelated keys', () => {

    // Expected outcome: `interpretDesignerKey({ key: 'Enter', target: document.body })` is not supplied.
    // Acceptance criteria: `interpretDesignerKey({ key: 'Enter', target: document.body })` must be undefined, because this condition proves that
    // ignores unrelated keys.
    expect(interpretDesignerKey({ key: 'Enter', target: document.body })).toBeUndefined();
  });

  /**
   * Purpose: Protects the behavioral contract that does not interpret commands from editable controls.
   * Description: Exercises does not interpret commands from editable controls from its arranged starting state and
   * verifies the observable results required by the scenario.
   */
  it('does not interpret commands from editable controls', () => {
    for (const tagName of ['input', 'textarea', 'select']) {

      // Expected outcome: `interpretDesignerKey({ key: 'Delete', target: document.createElement(tagName) })` is not supplied.
      // Acceptance criteria: `interpretDesignerKey({ key: 'Delete', target: document.createElement(tagName) })` must be undefined, because this condition proves that
      // does not interpret commands from editable controls.
      expect(
        interpretDesignerKey({ key: 'Delete', target: document.createElement(tagName) })
      ).toBeUndefined();
    }
    const editable = document.createElement('div');
    editable.contentEditable = 'true';
    document.body.append(editable);

    // Expected outcome: `interpretDesignerKey({ key: 'ArrowRight', target: editable })` is not supplied.
    // Acceptance criteria: `interpretDesignerKey({ key: 'ArrowRight', target: editable })` must be undefined, because this condition proves that
    // does not interpret commands from editable controls.
    expect(interpretDesignerKey({ key: 'ArrowRight', target: editable })).toBeUndefined();
  });
});
