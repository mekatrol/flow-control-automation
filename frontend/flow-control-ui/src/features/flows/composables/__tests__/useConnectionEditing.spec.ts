import { describe, expect, it } from 'vitest';

import { useConnectionEditing } from '@/features/flows/composables/useConnectionEditing';

describe('connection editing state', () => {

  /**
   * Purpose: Protects the behavioral contract that starts, previews, reports an error, and cancels without persisted view state.
   * Description: Exercises starts, previews, reports an error, and cancels without persisted view state from its arranged starting state and
   * verifies the observable results required by the scenario.
   */
  it('starts, previews, reports an error, and cancels without persisted view state', () => {
    const editing = useConnectionEditing();
    editing.beginConnection({ nodeId: 'a', connectorId: 'output' }, { x: 10, y: 20 });

    // Expected outcome: `editing.connectionStart.value` matches the required structure.
    // Acceptance criteria: `editing.connectionStart.value` must equal `{ nodeId: 'a', connectorId: 'output' }`, because this condition proves that
    // starts, previews, reports an error, and cancels without persisted view state.
    expect(editing.connectionStart.value).toEqual({ nodeId: 'a', connectorId: 'output' });

    // Expected outcome: `editing.previewEnd.value` matches the required structure.
    // Acceptance criteria: `editing.previewEnd.value` must equal `{ x: 10, y: 20 }`, because this condition proves that
    // starts, previews, reports an error, and cancels without persisted view state.
    expect(editing.previewEnd.value).toEqual({ x: 10, y: 20 });

    editing.updatePreview({ x: 30, y: 40 });
    editing.reportConnectionError('Invalid link');

    // Expected outcome: `editing.previewEnd.value` matches the required structure.
    // Acceptance criteria: `editing.previewEnd.value` must equal `{ x: 30, y: 40 }`, because this condition proves that
    // starts, previews, reports an error, and cancels without persisted view state.
    expect(editing.previewEnd.value).toEqual({ x: 30, y: 40 });

    // Expected outcome: `editing.connectionError.value` has the required value.
    // Acceptance criteria: `editing.connectionError.value` must be `'Invalid link'`, because this condition proves that
    // starts, previews, reports an error, and cancels without persisted view state.
    expect(editing.connectionError.value).toBe('Invalid link');

    editing.cancelConnection();

    // Expected outcome: `editing.connectionStart.value` is not supplied.
    // Acceptance criteria: `editing.connectionStart.value` must be undefined, because this condition proves that
    // starts, previews, reports an error, and cancels without persisted view state.
    expect(editing.connectionStart.value).toBeUndefined();

    // Expected outcome: `editing.previewEnd.value` is not supplied.
    // Acceptance criteria: `editing.previewEnd.value` must be undefined, because this condition proves that
    // starts, previews, reports an error, and cancels without persisted view state.
    expect(editing.previewEnd.value).toBeUndefined();

    // Expected outcome: `editing.connectionError.value` is not supplied.
    // Acceptance criteria: `editing.connectionError.value` must be undefined, because this condition proves that
    // starts, previews, reports an error, and cancels without persisted view state.
    expect(editing.connectionError.value).toBeUndefined();
  });
});
