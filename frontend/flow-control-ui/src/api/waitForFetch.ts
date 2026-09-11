import { getActivePinia } from 'pinia';
import { getApiKey } from '@/config/apiAccess';
import { useSpinnerStore } from '@/stores/spinner';

export const waitForFetch = async (
  input: RequestInfo | URL,
  init?: RequestInit,
  options: { trackWait?: boolean } = {}
): Promise<Response> => {
  const apiKey = getApiKey();
  let authenticatedInit = init;
  if (apiKey) {
    const headers = new Headers(init?.headers);
    headers.set('X-Api-Key', apiKey);
    authenticatedInit = { ...init, headers };
  }
  const pinia = getActivePinia();
  if (!pinia || options.trackWait === false) return fetch(input, authenticatedInit);

  const spinnerStore = useSpinnerStore(pinia);
  spinnerStore.showSpinner();
  try {
    return await fetch(input, authenticatedInit);
  } finally {
    spinnerStore.hideSpinner();
  }
};
