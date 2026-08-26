<template>
  <AppDialog
    ref="dialog"
    content-label="Create new flow"
    v-bind="automation()"
    :dismissible="false"
  >
    <AppForm class="new-flow-form" v-bind="automation('form')" @submit.prevent="confirm">
      <h2>Flow details</h2>
      <label for="flow-name">Flow name</label>
      <input id="flow-name" v-model="value" required autocomplete="off" />
      <div class="editor-actions">
        <AppButton
          v-bind="automation('save')"
          type="submit"
          text="Create flow"
          :icon="createIcon"
        />
        <AppButton v-bind="automation('cancel')" text="Cancel" :icon="cancelIcon" @click="cancel" />
      </div>
    </AppForm>
  </AppDialog>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue';

import { useAutomation } from '@/composables/useAutomation';

import AppButton from '@/components/AppButton.vue';
import AppDialog from '@/components/AppDialog.vue';
import AppForm from '@/components/AppForm.vue';

import cancelIcon from '@/assets/icons/cancel-icon.svg';
import createIcon from '@/assets/icons/new-icon.svg';

import { EVENTS } from '@/constants/events';

const props = withDefaults(
  defineProps<{
    automation: string;
    modelValue: string;
  }>(),
  {}
);

const emit = defineEmits({
  [EVENTS.CANCEL]: (): boolean => true,
  [EVENTS.CONFIRM]: (): boolean => true,
  'update:modelValue': (value: string): boolean => typeof value === 'string'
});

const dialog = ref<InstanceType<typeof AppDialog>>();
const automation = useAutomation(props.automation);

const value = computed({
  get: (): string => props.modelValue,
  set: (newValue: string): void => {
    emit('update:modelValue', newValue);
  }
});

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
