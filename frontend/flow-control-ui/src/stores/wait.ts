import { computed, ref } from 'vue';
import { defineStore } from 'pinia';

export const useWaitStore = defineStore('wait', () => {
  const waitCount = ref(0);
  const isWaiting = computed(() => waitCount.value > 0);

  const wait = (): void => {
    waitCount.value += 1;
  };

  const endWait = (): void => {
    waitCount.value = Math.max(0, waitCount.value - 1);
  };

  return { waitCount, isWaiting, wait, endWait };
});
