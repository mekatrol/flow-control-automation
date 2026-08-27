<template>
  <AppDialog ref="dialog" content-label="Create new flow" :dismissible="false">
    <AppForm class="new-flow-form" @submit.prevent="confirm">
      <template #header>
        <section>
          <h2>Flow details</h2>
        </section>
      </template>
      <section class="body">
        <label for="flow-name">Flow name</label>
        <input id="flow-name" v-model="value" required autocomplete="off" />
      </section>
      <template #footer>
        <section class="editor-actions">
          <AppButton type="submit" text="Create flow" :icon="createIcon" />
          <AppButton text="Cancel" :icon="cancelIcon" @click="cancel" />
        </section>
      </template>
    </AppForm>
  </AppDialog>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue';

import AppButton from '@/components/AppButton.vue';
import AppDialog from '@/components/AppDialog.vue';
import AppForm from '@/components/AppForm.vue';

import cancelIcon from '@/assets/icons/cancel-icon.svg';
import createIcon from '@/assets/icons/new-icon.svg';

import { EVENTS } from '@/constants/events';

const props = withDefaults(
  defineProps<{
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

<style lang="css">
.body {
  display: flex;
  flex-direction: row;
  gap: 0.5rem;
  padding: 0.5rem 0.5rem;
  min-width: 480px;
  margin: 1rem;
}

.body > input {
  flex: 1;
  line-height: 1.2;
  font-size: 1.2rem;
}
</style>
