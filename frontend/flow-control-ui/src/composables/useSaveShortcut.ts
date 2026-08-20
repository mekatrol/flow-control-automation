import { onBeforeUnmount, onMounted } from 'vue';

export const useSaveShortcut = (
  save: () => void | Promise<void>,
  canSave: () => boolean = () => true
): void => {
  const handleKeydown = (event: KeyboardEvent): void => {
    if (
      event.key.toLowerCase() !== 's' ||
      (!event.ctrlKey && !event.metaKey) ||
      event.altKey ||
      event.shiftKey
    )
      return;

    event.preventDefault();
    if (event.repeat || !canSave()) return;
    void save();
  };

  onMounted(() => window.addEventListener('keydown', handleKeydown));
  onBeforeUnmount(() => window.removeEventListener('keydown', handleKeydown));
};
