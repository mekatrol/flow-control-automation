import { computed, ref } from 'vue';
import { defineStore } from 'pinia';

export const useSpinnerStore = defineStore('spinner', () => {
  const spinnerWaitingCount = ref(0); // The number of calls to showSpinner that have not yet been matched by a call to hideSpinner
  const isSpinnerVisible = computed(() => spinnerWaitingCount.value > 0);

  const showSpinner = (): void => {
    spinnerWaitingCount.value += 1;
  };

  const hideSpinner = (): void => {
    spinnerWaitingCount.value = Math.max(0, spinnerWaitingCount.value - 1);
  };

  return { spinnerWaitingCount, isSpinnerVisible, showSpinner, hideSpinner };
});
