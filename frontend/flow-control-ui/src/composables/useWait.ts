import { storeToRefs } from 'pinia';
import type { ComputedRef, Ref } from 'vue';
import { useWaitStore } from '@/stores/wait';

interface WaitControls {
  waitCount: Ref<number>;
  isWaiting: ComputedRef<boolean>;
  wait: () => void;
  endWait: () => void;
  withSpinner: <T>(
    pre: (() => void | Promise<void>) | null,
    call: () => T | Promise<T>,
    post: ((result: T) => void | Promise<void>) | null
  ) => Promise<T>;
}

export const useWait = (): WaitControls => {
  const store = useWaitStore();
  const { waitCount, isWaiting } = storeToRefs(store);

  const withSpinner = async <T>(
    pre: (() => void | Promise<void>) | null,
    call: () => T | Promise<T>,
    post: ((result: T) => void | Promise<void>) | null
  ): Promise<T> => {
    store.wait();
    try {
      const preResult = pre?.();
      if (preResult) await preResult;
      const result = await call();
      await post?.(result);
      return result;
    } finally {
      store.endWait();
    }
  };

  return {
    waitCount,
    isWaiting,
    wait: store.wait,
    endWait: store.endWait,
    withSpinner
  };
};
