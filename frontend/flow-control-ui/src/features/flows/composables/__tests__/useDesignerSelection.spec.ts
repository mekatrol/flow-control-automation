import { describe, expect, it, vi } from 'vitest';

import { useDesignerSelection } from '@/features/flows/composables/useDesignerSelection';

describe('designer selection', () => {

  /**
   * Purpose: Protects the behavioral contract that moves selection between nodes and connections.
   * Description: Exercises moves selection between nodes and connections from its arranged starting state and
   * verifies the observable results required by the scenario.
   */
  it('moves selection between nodes and connections', () => {
    const selection = useDesignerSelection();

    selection.selectNode('node-1');

    // Expected outcome: `selection.selectedNodeId.value` has the required value.
    // Acceptance criteria: `selection.selectedNodeId.value` must be `'node-1'`, because this condition proves that
    // moves selection between nodes and connections.
    expect(selection.selectedNodeId.value).toBe('node-1');

    // Expected outcome: `selection.canDelete.value` has the required value.
    // Acceptance criteria: `selection.canDelete.value` must be `true`, because this condition proves that
    // moves selection between nodes and connections.
    expect(selection.canDelete.value).toBe(true);

    selection.selectConnection('connection-1');

    // Expected outcome: `selection.selectedNodeId.value` is not supplied.
    // Acceptance criteria: `selection.selectedNodeId.value` must be undefined, because this condition proves that
    // moves selection between nodes and connections.
    expect(selection.selectedNodeId.value).toBeUndefined();

    // Expected outcome: `selection.selectedConnectionId.value` has the required value.
    // Acceptance criteria: `selection.selectedConnectionId.value` must be `'connection-1'`, because this condition proves that
    // moves selection between nodes and connections.
    expect(selection.selectedConnectionId.value).toBe('connection-1');
  });

  /**
   * Purpose: Protects the behavioral contract that clears selection directly and with Escape.
   * Description: Exercises clears selection directly and with Escape from its arranged starting state and
   * verifies the observable results required by the scenario.
   */
  it('clears selection directly and with Escape', () => {
    const selection = useDesignerSelection();
    const preventDefault = vi.fn<() => void>();
    selection.selectNode('node-1');

    // Expected outcome: `selection.handleSelectionKeydown({ key: 'Escape', preventDefault })` has the required value.
    // Acceptance criteria: `selection.handleSelectionKeydown({ key: 'Escape', preventDefault })` must be `true`, because this condition proves that
    // clears selection directly and with Escape.
    expect(selection.handleSelectionKeydown({ key: 'Escape', preventDefault })).toBe(true);

    // Expected outcome: `preventDefault` is invoked once.
    // Acceptance criteria: `preventDefault` must have exactly one call, because this condition proves that
    // clears selection directly and with Escape.
    expect(preventDefault).toHaveBeenCalledOnce();

    // Expected outcome: `selection.canDelete.value` has the required value.
    // Acceptance criteria: `selection.canDelete.value` must be `false`, because this condition proves that
    // clears selection directly and with Escape.
    expect(selection.canDelete.value).toBe(false);

    selection.selectNode('node-2');
    selection.clearSelection();

    // Expected outcome: `selection.selectedNodeId.value` is not supplied.
    // Acceptance criteria: `selection.selectedNodeId.value` must be undefined, because this condition proves that
    // clears selection directly and with Escape.
    expect(selection.selectedNodeId.value).toBeUndefined();
  });

  /**
   * Purpose: Protects the behavioral contract that ignores unrelated keys and Escape when nothing is selected.
   * Description: Exercises ignores unrelated keys and Escape when nothing is selected from its arranged starting state and
   * verifies the observable results required by the scenario.
   */
  it('ignores unrelated keys and Escape when nothing is selected', () => {
    const selection = useDesignerSelection();
    const preventDefault = vi.fn<() => void>();

    // Expected outcome: `selection.handleSelectionKeydown({ key: 'Enter', preventDefault })` has the required value.
    // Acceptance criteria: `selection.handleSelectionKeydown({ key: 'Enter', preventDefault })` must be `false`, because this condition proves that
    // ignores unrelated keys and Escape when nothing is selected.
    expect(selection.handleSelectionKeydown({ key: 'Enter', preventDefault })).toBe(false);

    // Expected outcome: `selection.handleSelectionKeydown({ key: 'Escape', preventDefault })` has the required value.
    // Acceptance criteria: `selection.handleSelectionKeydown({ key: 'Escape', preventDefault })` must be `false`, because this condition proves that
    // ignores unrelated keys and Escape when nothing is selected.
    expect(selection.handleSelectionKeydown({ key: 'Escape', preventDefault })).toBe(false);

    // Expected outcome: `preventDefault` is not invoked.
    // Acceptance criteria: `preventDefault` must have no calls, because this condition proves that
    // ignores unrelated keys and Escape when nothing is selected.
    expect(preventDefault).not.toHaveBeenCalled();
  });
});
