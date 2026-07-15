import { describe, expect, it, vi } from 'vitest';

import { useDesignerSelection } from '@/features/flows/composables/useDesignerSelection';

describe('designer selection', () => {
  it('moves selection between nodes and connections', () => {
    const selection = useDesignerSelection();

    selection.selectNode('node-1');
    expect(selection.selectedNodeId.value).toBe('node-1');
    expect(selection.canDelete.value).toBe(true);

    selection.selectConnection('connection-1');
    expect(selection.selectedNodeId.value).toBeUndefined();
    expect(selection.selectedConnectionId.value).toBe('connection-1');
  });

  it('clears selection directly and with Escape', () => {
    const selection = useDesignerSelection();
    const preventDefault = vi.fn<() => void>();
    selection.selectNode('node-1');

    expect(selection.handleSelectionKeydown({ key: 'Escape', preventDefault })).toBe(true);
    expect(preventDefault).toHaveBeenCalledOnce();
    expect(selection.canDelete.value).toBe(false);

    selection.selectNode('node-2');
    selection.clearSelection();
    expect(selection.selectedNodeId.value).toBeUndefined();
  });

  it('ignores unrelated keys and Escape when nothing is selected', () => {
    const selection = useDesignerSelection();
    const preventDefault = vi.fn<() => void>();

    expect(selection.handleSelectionKeydown({ key: 'Enter', preventDefault })).toBe(false);
    expect(selection.handleSelectionKeydown({ key: 'Escape', preventDefault })).toBe(false);
    expect(preventDefault).not.toHaveBeenCalled();
  });
});
