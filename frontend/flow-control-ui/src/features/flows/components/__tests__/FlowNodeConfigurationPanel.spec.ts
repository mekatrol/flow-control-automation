// @vitest-environment jsdom

import { describe, expect, it } from 'vitest';

import { editorValueFromInput, validateNodeLabel } from '../FlowNodeConfigurationPanel.vue';

describe('node configuration validation', () => {
  it('requires a non-empty label', () => {
    expect(validateNodeLabel('   ')).toBe('Node label is required.');
    expect(validateNodeLabel('Living room average')).toBeUndefined();
  });

  it('parses typed checkbox, number, and select editor values', () => {
    const checkbox = document.createElement('input');
    checkbox.type = 'checkbox';
    checkbox.checked = true;
    expect(editorValueFromInput({ key: 'on', label: 'On', input: 'checkbox' }, checkbox)).toEqual({
      value: true
    });

    const number = document.createElement('input');
    number.value = '42.5';
    expect(
      editorValueFromInput({ key: 'duration', label: 'Duration', input: 'number' }, number)
    ).toEqual({
      value: 42.5
    });

    const select = document.createElement('select');
    select.innerHTML = '<option value="sum">sum</option>';
    expect(
      editorValueFromInput(
        { key: 'operation', label: 'Operation', input: 'select', options: ['average', 'sum'] },
        select
      )
    ).toEqual({ value: 'sum' });
  });

  it('rejects empty numbers and unsupported select values', () => {
    const number = document.createElement('input');
    expect(
      editorValueFromInput({ key: 'duration', label: 'Duration', input: 'number' }, number).error
    ).toMatch(/required/);

    const select = document.createElement('select');
    select.innerHTML = '<option value="product">product</option>';
    expect(
      editorValueFromInput(
        { key: 'operation', label: 'Operation', input: 'select', options: ['average', 'sum'] },
        select
      ).error
    ).toMatch(/valid operation/);
  });
});
