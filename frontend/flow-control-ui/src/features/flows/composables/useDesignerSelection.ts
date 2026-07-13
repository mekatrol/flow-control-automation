import { computed, ref } from 'vue';

export const useDesignerSelection = () => {
  const selectedNodeId = ref<string>();
  const selectedConnectionId = ref<string>();

  const canDelete = computed(() => Boolean(selectedNodeId.value || selectedConnectionId.value));

  const selectNode = (nodeId: string): void => {
    // Node and connection selection are mutually exclusive because Delete and
    // the inspector must always have one unambiguous target.
    selectedNodeId.value = nodeId;
    selectedConnectionId.value = undefined;
  };

  const selectConnection = (connectionId: string): void => {
    selectedConnectionId.value = connectionId;
    selectedNodeId.value = undefined;
  };

  const clearSelection = (): void => {
    selectedNodeId.value = undefined;
    selectedConnectionId.value = undefined;
  };

  const handleSelectionKeydown = (
    event: Pick<KeyboardEvent, 'key' | 'preventDefault'>
  ): boolean => {
    // Returning whether Escape was handled lets the canvas decide whether the
    // same key should instead cancel another transient interaction.
    if (event.key !== 'Escape' || !canDelete.value) return false;
    event.preventDefault();
    clearSelection();
    return true;
  };

  return {
    selectedNodeId,
    selectedConnectionId,
    canDelete,
    selectNode,
    selectConnection,
    clearSelection,
    handleSelectionKeydown
  };
};
