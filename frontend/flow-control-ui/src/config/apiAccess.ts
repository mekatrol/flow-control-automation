const apiKeyPlaceholder = '__FLOW_CONTROL_API_KEY__';
const apiKeyStorageKey = 'flow-control-api-key';

export const getInjectedApiKey = (): string | null => {
  if (typeof document === 'undefined') return null;

  const value = document
    .querySelector<HTMLMetaElement>('meta[name="flow-control-api-key"]')
    ?.content.trim();

  return value && value !== apiKeyPlaceholder ? value : null;
};

export const getApiKey = (): string | null => {
  const injectedApiKey = getInjectedApiKey();
  if (injectedApiKey || typeof sessionStorage === 'undefined') return injectedApiKey;

  return sessionStorage.getItem(apiKeyStorageKey);
};

export const storeApiKey = (apiKey: string): void =>
  sessionStorage.setItem(apiKeyStorageKey, apiKey);

export const removeStoredApiKey = (): void => sessionStorage.removeItem(apiKeyStorageKey);
