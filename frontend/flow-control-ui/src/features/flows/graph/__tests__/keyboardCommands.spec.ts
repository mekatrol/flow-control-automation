// @vitest-environment jsdom

import { describe, expect, it } from 'vitest';

import { interpretDesignerKey } from '../keyboardCommands';

describe('designer keyboard commands', () => {
  it('maps arrow, Delete, and Backspace keys', () => {
    expect(interpretDesignerKey({ key: 'ArrowLeft', target: document.body })).toEqual({
      type: 'move',
      deltaX: -24,
      deltaY: 0
    });
    expect(interpretDesignerKey({ key: 'ArrowDown', target: document.body }, 10)).toEqual({
      type: 'move',
      deltaX: 0,
      deltaY: 10
    });
    expect(interpretDesignerKey({ key: 'Delete', target: document.body })).toEqual({
      type: 'delete'
    });
    expect(interpretDesignerKey({ key: 'Backspace', target: document.body })).toEqual({
      type: 'delete'
    });
  });

  it('ignores unrelated keys', () => {
    expect(interpretDesignerKey({ key: 'Enter', target: document.body })).toBeUndefined();
  });

  it('does not interpret commands from editable controls', () => {
    for (const tagName of ['input', 'textarea', 'select']) {
      expect(
        interpretDesignerKey({ key: 'Delete', target: document.createElement(tagName) })
      ).toBeUndefined();
    }
    const editable = document.createElement('div');
    editable.contentEditable = 'true';
    document.body.append(editable);
    expect(interpretDesignerKey({ key: 'ArrowRight', target: editable })).toBeUndefined();
  });
});
