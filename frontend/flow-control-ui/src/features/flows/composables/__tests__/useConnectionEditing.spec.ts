import { describe, expect, it } from 'vitest';

import { useConnectionEditing } from '../useConnectionEditing';

describe('connection editing state', () => {
  it('starts, previews, reports an error, and cancels without persisted view state', () => {
    const editing = useConnectionEditing();
    editing.beginConnection({ nodeId: 'a', connectorId: 'output' }, { x: 10, y: 20 });
    expect(editing.connectionStart.value).toEqual({ nodeId: 'a', connectorId: 'output' });
    expect(editing.previewEnd.value).toEqual({ x: 10, y: 20 });

    editing.updatePreview({ x: 30, y: 40 });
    editing.reportConnectionError('Invalid link');
    expect(editing.previewEnd.value).toEqual({ x: 30, y: 40 });
    expect(editing.connectionError.value).toBe('Invalid link');

    editing.cancelConnection();
    expect(editing.connectionStart.value).toBeUndefined();
    expect(editing.previewEnd.value).toBeUndefined();
    expect(editing.connectionError.value).toBeUndefined();
  });
});
