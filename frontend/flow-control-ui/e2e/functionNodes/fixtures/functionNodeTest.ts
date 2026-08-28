import { expect, test as base } from '@playwright/test';

const testApiKey = 'flow-control-e2e-administrator-key';

export { expect };

/**
 * Function-node tests always use the managed real backend. Seed the same
 * browser session credential as the Vite-injected E2E page before any app code
 * runs. This also makes direct launches from the Playwright extension
 * independent of an older cached Vite index document.
 */
export const test = base.extend<{ functionNodeApiAccess: void }>({
  functionNodeApiAccess: [
    async ({ page }, use) => {
      await page.addInitScript(
        ({ apiKey, storageKey }) => window.sessionStorage.setItem(storageKey, apiKey),
        { apiKey: testApiKey, storageKey: 'flow-control-api-key' }
      );
      await use();
    },
    { auto: true }
  ]
});
