import { storeToRefs } from 'pinia';
import type { ComputedRef, Ref } from 'vue';
import { useWaitStore } from '@/stores/wait';

interface WaitControls {
  waitCount: Ref<number>;
  isWaiting: ComputedRef<boolean>;
  wait: () => void;
  endWait: () => void;
}

export const useWait = (): WaitControls => {
  const store = useWaitStore();
  const { waitCount, isWaiting } = storeToRefs(store);

  return {
    waitCount,
    isWaiting,
    wait: store.wait,
    endWait: store.endWait
  };
};
