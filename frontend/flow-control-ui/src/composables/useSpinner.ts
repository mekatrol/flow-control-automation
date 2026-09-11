import { storeToRefs } from 'pinia';
import type { ComputedRef, Ref } from 'vue';
import { useSpinnerStore } from '@/stores/spinner';

interface SpinnerControls {
  spinnerWaitingCount: Ref<number>;
  isSpinnerVisible: ComputedRef<boolean>;
  showSpinner: () => void;
  hideSpinner: () => void;
  withSpinner: <T>(
    pre: (() => void | Promise<void>) | null,
    call: () => T | Promise<T>,
    post: ((result: T) => void | Promise<void>) | null
  ) => Promise<T>;
}

export const useSpinner = (): SpinnerControls => {
  const store = useSpinnerStore();
  const { spinnerWaitingCount, isSpinnerVisible } = storeToRefs(store);

  const withSpinner = async <T>(
    pre: (() => void | Promise<void>) | null,
    call: () => T | Promise<T>,
    post: ((result: T) => void | Promise<void>) | null
  ): Promise<T> => {
    store.showSpinner();
    try {
      const preResult = pre?.();
      if (preResult) await preResult;
      const result = await call();
      await post?.(result);
      return result;
    } finally {
      store.hideSpinner();
    }
  };

  return {
    spinnerWaitingCount,
    isSpinnerVisible,
    showSpinner: store.showSpinner,
    hideSpinner: store.hideSpinner,
    withSpinner
  };
};
