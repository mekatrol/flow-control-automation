// @vitest-environment jsdom

import { describe, expect, it } from 'vitest';

import {
  editorValueFromInput,
  validateNodeLabel
} from '@/features/flows/components/AppFlowNodeConfigurationPanel.vue';

describe('node configuration validation', () => {
  /**
   * Purpose: Protects the behavioral contract that requires a non-empty label.
   * Description: Exercises requires a non-empty label from its arranged starting state and
   * verifies the observable results required by the scenario.
   */
  it('requires a non-empty label', () => {
    // Expected outcome: `validateNodeLabel(' ')` has the required value.
    // Acceptance criteria: `validateNodeLabel(' ')` must be `'Node label is required.'`, because this condition proves that
    // requires a non-empty label.
    expect(validateNodeLabel('   ')).toBe('Node label is required.');

    // Expected outcome: `validateNodeLabel('Living room average')` is not supplied.
    // Acceptance criteria: `validateNodeLabel('Living room average')` must be undefined, because this condition proves that
    // requires a non-empty label.
    expect(validateNodeLabel('Living room average')).toBeUndefined();
  });

  /**
   * Purpose: Protects the behavioral contract that parses typed checkbox, number, and select editor values.
   * Description: Exercises parses typed checkbox, number, and select editor values from its arranged starting state and
   * verifies the observable results required by the scenario.
   */
  it('parses typed checkbox, number, and select editor values', () => {
    const checkbox = document.createElement('input');
    checkbox.type = 'checkbox';
    checkbox.checked = true;

    // Expected outcome: `editorValueFromInput({ key: 'on', label: 'On', input: 'checkbox' }, checkbox)` matches the required structure.
    // Acceptance criteria: `editorValueFromInput({ key: 'on', label: 'On', input: 'checkbox' }, checkbox)` must equal `{ value: true }`, because this condition proves that
    // parses typed checkbox, number, and select editor values.
    expect(editorValueFromInput({ key: 'on', label: 'On', input: 'checkbox' }, checkbox)).toEqual({
      value: true
    });

    const number = document.createElement('input');
    number.value = '42.5';

    // Expected outcome: `editorValueFromInput({ key: 'duration', label: 'Duration', input: 'number' }, number)` matches the required structure.
    // Acceptance criteria: `editorValueFromInput({ key: 'duration', label: 'Duration', input: 'number' }, number)` must equal `{ value: 42.5 }`, because this condition proves that
    // parses typed checkbox, number, and select editor values.
    expect(
      editorValueFromInput({ key: 'duration', label: 'Duration', input: 'number' }, number)
    ).toEqual({
      value: 42.5
    });

    const select = document.createElement('select');
    select.innerHTML = '<option value="sum">sum</option>';

    // Expected outcome: `editorValueFromInput( { key: 'operation', label: 'Operation', input: 'select', options: ['average', ` matches the required structure.
    // Acceptance criteria: `editorValueFromInput( { key: 'operation', label: 'Operation', input: 'select', options: ['average', ` must equal `{ value: 'sum' }`, because this condition proves that
    // parses typed checkbox, number, and select editor values.
    expect(
      editorValueFromInput(
        { key: 'operation', label: 'Operation', input: 'select', options: ['average', 'sum'] },
        select
      )
    ).toEqual({ value: 'sum' });
  });

  /**
   * Purpose: Protects the behavioral contract that rejects empty numbers and unsupported select values.
   * Description: Exercises rejects empty numbers and unsupported select values from its arranged starting state and
   * verifies the observable results required by the scenario.
   */
  it('rejects empty numbers and unsupported select values', () => {
    const number = document.createElement('input');

    // Expected outcome: `editorValueFromInput({ key: 'duration', label: 'Duration', input: 'number' }, number` follows the required pattern.
    // Acceptance criteria: `editorValueFromInput({ key: 'duration', label: 'Duration', input: 'number' }, number` must match `/required/`, because this condition proves that
    // rejects empty numbers and unsupported select values.
    expect(
      editorValueFromInput({ key: 'duration', label: 'Duration', input: 'number' }, number).error
    ).toMatch(/required/);

    const select = document.createElement('select');
    select.innerHTML = '<option value="product">product</option>';

    // Expected outcome: `editorValueFromInput( { key: 'operation', label: 'Operation', input: 'select', options: ['average', ` follows the required pattern.
    // Acceptance criteria: `editorValueFromInput( { key: 'operation', label: 'Operation', input: 'select', options: ['average', ` must match `/valid operation/`, because this condition proves that
    // rejects empty numbers and unsupported select values.
    expect(
      editorValueFromInput(
        { key: 'operation', label: 'Operation', input: 'select', options: ['average', 'sum'] },
        select
      ).error
    ).toMatch(/valid operation/);
  });
});
