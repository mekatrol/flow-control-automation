import { getActivePinia } from 'pinia';
import { useWaitStore } from '@/stores/wait';

export const waitForFetch = async (
  input: RequestInfo | URL,
  init?: RequestInit
): Promise<Response> => {
  const apiKey =
    import.meta.env.VITE_FLOW_CONTROL_API_KEY || sessionStorage.getItem('flow-control-api-key');
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
