<template>
  <AppNotice
    :id="id"
    ref="notice"
    :automation="automation"
    :title="title"
    :message="message"
    variant="error"
  >
    <template v-if="details.length" #content>
      <p>{{ message }}</p>
      <ul>
        <li v-for="detail in details" :key="detail">{{ detail }}</li>
      </ul>
    </template>
    <template #footer="{ close }">
      <AppButton
        v-if="retryable"
        :automation="`${automation}-retry`"
        text="Retry"
        :icon="retryIcon"
        @click="retry(close)"
      />
      <AppButton
        :automation="`${automation}-close`"
        text="Close"
        :icon="cancelIcon"
        @click="close"
      />
    </template>
  </AppNotice>
</template>

<script setup lang="ts">
import { nextTick, ref, watch } from 'vue';

import cancelIcon from '@/assets/icons/cancel-icon.svg';
import retryIcon from '@/assets/icons/retry-icon.svg';
import AppButton from '@/components/AppButton.vue';
import AppNotice from '@/components/AppNotice.vue';

const props = withDefaults(
  defineProps<{
    id: string;
    automation: string;
    message?: string;
    title?: string;
    retryable?: boolean;
    details?: string[];
  }>(),
  {
    message: '',
    title: 'Unable to complete the request',
    retryable: false,
    details: () => []
  }
);

const emit = defineEmits<{ retry: [] }>();
const notice = ref<InstanceType<typeof AppNotice>>();

watch(
  () => props.message,
  async (message) => {
    if (!message) return;
    await nextTick();
    notice.value?.showModal();
  },
  { immediate: true }
);

const retry = (close: () => void): void => {
  close();
  emit('retry');
};
</script>
