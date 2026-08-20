import type { Page } from '@playwright/test';

import { expect, test } from './fixtures/flowTest';
import { sampleFlows } from '@/features/flows/__tests__/fixtures/sampleFlows';

const changeWaitCount = async (
  page: Page,
  action: 'wait' | 'endWait'
): Promise<void> => {
  await page.evaluate(async (waitAction) => {
    // @ts-expect-error The path is resolved by the browser-facing Vite module graph.
    const { useWait } = await import('/src/composables/useWait.ts');
    useWait()[waitAction]();
  }, action);
};

test('blocks interaction until every concurrent wait has ended', async ({ page }) => {
  await page.goto('/flows');

  const application = page.locator('.app-content');
  const overlay = page.getByRole('status', { name: 'Please wait' });
  const themeSelector = page.getByRole('button', { name: /^Theme preference:/ });

  await changeWaitCount(page, 'wait');
  await changeWaitCount(page, 'wait');

  await expect(overlay).toBeVisible();
  await expect(application).toHaveAttribute('inert', '');
  await themeSelector.focus();
  await expect(themeSelector).not.toBeFocused();

  await changeWaitCount(page, 'endWait');
  await expect(overlay).toBeVisible();
  await expect(application).toHaveAttribute('inert', '');

  await changeWaitCount(page, 'endWait');
  await expect(overlay).toBeHidden();
  await expect(application).not.toHaveAttribute('inert', '');
  await themeSelector.focus();
  await expect(themeSelector).toBeFocused();
});

test('blocks the application while a flow save is in progress', async ({ page }) => {
  await page.unroute('**/api/flows/*');
  const payload = structuredClone(sampleFlows[0]!);
  let releaseSave!: () => void;
  const saveCanComplete = new Promise<void>((resolve) => {
    releaseSave = resolve;
  });

  await page.route('**/api/flows/climate-control', async (route) => {
    if (route.request().method() === 'PUT') {
      await saveCanComplete;
      await route.fulfill({ json: route.request().postDataJSON() });
      return;
    }

    await route.fulfill({ json: payload });
  });

  await page.goto('/flows/climate-control');
  await page.getByRole('button', { name: 'Save flow' }).click();

  const overlay = page.getByRole('status', { name: 'Please wait' });
  await expect(overlay).toBeVisible();
  await expect(page.locator('.app-content')).toHaveAttribute('inert', '');

  releaseSave();

  await expect(overlay).toBeHidden();
  await expect(page.locator('.app-content')).not.toHaveAttribute('inert', '');
  await expect(page.getByRole('button', { name: 'Save flow' })).toBeEnabled();
});
