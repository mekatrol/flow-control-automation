<template>
  <AppDialog
    :id="id"
    ref="dialog"
    :content-label="contentLabel"
    :dismissible="false"
    role="alertdialog"
  >
    <slot name="prompt" :cancel="cancel" :confirm="confirm">
      <section
        class="prompt-dialog-content"
        :aria-labelledby="`${id}-title`"
        :aria-describedby="`${id}-description`"
      >
        <h2 :id="`${id}-title`">{{ title }}</h2>
        <p :id="`${id}-description`">{{ message }}</p>
        <div class="prompt-dialog-actions">
          <AppButton :text="confirmText" :icon="deleteIcon" @click="confirm" />
          <AppButton :text="cancelText" :icon="cancelIcon" @click="cancel" />
        </div>
      </section>
    </slot>
  </AppDialog>
</template>

<script setup lang="ts">
import { ref } from 'vue';
import cancelIcon from '@/assets/icons/cancel-icon.svg';
import deleteIcon from '@/assets/icons/delete-flow-icon.svg';
import AppButton from '@/components/AppButton.vue';
import AppDialog from '@/components/AppDialog.vue';
import { EVENTS } from '@/constants/events';

withDefaults(
  defineProps<{
    id: string;
    contentLabel: string;
    title?: string;
    message?: string;
    cancelText?: string;
    confirmText?: string;
  }>(),
  {
    title: 'Discard unsaved changes?',
    message: 'Your changes have not been saved and will be lost.',
    cancelText: 'Keep editing',
    confirmText: 'Discard changes'
  }
);

const emit = defineEmits({
  [EVENTS.CANCEL]: (): boolean => true,
  [EVENTS.CONFIRM]: (): boolean => true
});

const dialog = ref<InstanceType<typeof AppDialog>>();

const close = (): void => {
  dialog.value?.close();
};

const cancel = (): void => {
  close();
  emit(EVENTS.CANCEL);
};

const confirm = (): void => {
  close();
  emit(EVENTS.CONFIRM);
};

defineExpose({
  showModal: (): void => dialog.value?.showModal(),
  close,
  cancel,
  confirm
});
</script>

<style scoped>
.prompt-dialog-content {
  display: flex;
  flex-direction: column;
  gap: var(--space-5-5);
}

.prompt-dialog-content h2 {
  margin-top: var(--space-0);
}

.prompt-dialog-content p {
  line-height: 1.5;
}

.prompt-dialog-actions {
  display: flex;
  gap: var(--space-4-5);
  justify-content: center;
}
</style>
