<template>
  <dialog
    v-bind="automation()"
    :id="dialogId"
    ref="panel"
    class="dialog-panel"
    role="alertdialog"
    :aria-label="contentLabel"
    @cancel="handleCancel"
    @close="emit(EVENTS.CLOSE, $event)"
  >
    <slot />
  </dialog>
</template>

<script setup lang="ts">
import { computed, ref, useId } from 'vue';
import { useAutomation } from '@/composables/useAutomation';
import { EVENTS } from '@/constants/events';

const props = withDefaults(
  defineProps<{
    id?: string;
    contentLabel: string;
    automation: string;
    dismissible?: boolean;
  }>(),
  {
    dismissible: true,
    id: undefined
  }
);

const emit = defineEmits({
  [EVENTS.CANCEL]: (nativeEvent: Event): boolean => nativeEvent instanceof Event,
  [EVENTS.CLOSE]: (nativeEvent: Event): boolean => nativeEvent instanceof Event
});

const generatedId = useId();
const dialogId = computed((): string => props.id ?? generatedId);
const panel = ref<HTMLDialogElement>();
const automation = useAutomation(props.automation);

const handleCancel = (event: Event): void => {
  if (!props.dismissible) event.preventDefault();
  emit(EVENTS.CANCEL, event);
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
