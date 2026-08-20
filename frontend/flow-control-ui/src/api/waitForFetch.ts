import { getActivePinia } from 'pinia';
import { useWaitStore } from '@/stores/wait';

export const waitForFetch = async (
  input: RequestInfo | URL,
  init?: RequestInit
): Promise<Response> => {
  const pinia = getActivePinia();
  if (!pinia) return fetch(input, init);

  const waitStore = useWaitStore(pinia);
  waitStore.wait();
  try {
    return await fetch(input, init);
  } finally {
    waitStore.endWait();
  }
};
