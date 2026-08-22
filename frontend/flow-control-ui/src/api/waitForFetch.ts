import { getActivePinia } from 'pinia';
import { getApiKey } from '@/config/apiAccess';
import { useWaitStore } from '@/stores/wait';

export const waitForFetch = async (
  input: RequestInfo | URL,
  init?: RequestInit
): Promise<Response> => {
  const apiKey = getApiKey();
  let authenticatedInit = init;
  if (apiKey) {
    const headers = new Headers(init?.headers);
    headers.set('X-Api-Key', apiKey);
    authenticatedInit = { ...init, headers };
  }
  const pinia = getActivePinia();
  if (!pinia) return fetch(input, authenticatedInit);

  const waitStore = useWaitStore(pinia);
  waitStore.wait();
  try {
    return await fetch(input, authenticatedInit);
  } finally {
    waitStore.endWait();
  }
};
