<template>
  <dialog
    v-bind="automation()"
    :id="id"
    ref="panel"
    class="dialog-panel"
    :aria-label="contentLabel"
    @cancel="handleCancel"
    @close="emit('close', $event)"
  >
    <slot />
  </dialog>
</template>

<script setup lang="ts">
import { ref } from 'vue';
import { useAutomation } from '@/composables/useAutomation';

const props = withDefaults(
  defineProps<{
    id: string;
    contentLabel: string;
    automation?: string;
    dismissible?: boolean;
  }>(),
  {
    automation: '',
    dismissible: true
  }
);

const emit = defineEmits<{
  cancel: [event: Event];
  close: [event: Event];
}>();

const panel = ref<HTMLDialogElement>();
const automation = useAutomation(props.automation);

const handleCancel = (event: Event): void => {
  if (!props.dismissible) event.preventDefault();
  emit('cancel', event);
};

defineExpose({
  showModal: (): void => panel.value?.showModal(),
  close: (returnValue?: string): void => panel.value?.close(returnValue)
});
</script>

<style scoped>
.dialog-panel {
  width: max-content;
  max-width: calc(100vw - 2rem);
  max-height: calc(100dvh - 2rem);
  margin: auto;
  padding: var(--space-8);
  overflow: auto;
  color: var(--color-text-primary);
  background: var(--color-surface-neutral);
  border: var(--border-width-default) solid var(--color-border-default);
  border-radius: var(--radius-lg);
  box-shadow: var(--shadow-dialog-compact);
}

.dialog-panel::backdrop {
  background: color-mix(in srgb, var(--color-shadow-dialog) 100%, transparent);
}
</style>
