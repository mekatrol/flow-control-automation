import { expect, test } from '@playwright/test';

const themeStates = [
  {
    preference: 'system',
    next: 'Dark',
    status: 'System theme preference selected',
    appliedTheme: null
  },
  {
    preference: 'dark',
    next: 'Light',
    status: 'Dark theme preference selected',
    appliedTheme: 'dark'
  },
  {
    preference: 'light',
    next: 'System',
    status: 'Light theme preference selected',
    appliedTheme: 'light'
  }
] as const;

test.beforeEach(async ({ page }) => {
  await page.route('**/api/flows', async (route) => {
    await route.fulfill({ json: [] });
  });
  await page.goto('/flows');
});

test('cycles through every theme and exposes the correct accessible state', async ({ page }) => {
  const selector = page.locator('.theme-selector');
  const help = page.locator('#theme-selector-help');
  const status = page.locator('.visually-hidden[role="status"]');

  await expect(selector).toHaveAttribute('aria-describedby', 'theme-selector-help');
  await expect(help).toHaveText('Cycles between system, dark, and light theme preferences.');
  await expect(status).toHaveAttribute('aria-live', 'polite');
  await expect(status).toHaveAttribute('aria-atomic', 'true');
  await expect(selector.locator('.theme-selector-icon')).toHaveAttribute('aria-hidden', 'true');

  for (const [index, state] of themeStates.entries()) {
    const name = `Theme preference: ${
      state.preference[0]!.toUpperCase() + state.preference.slice(1)
    }. Activate to use ${state.next} theme`;

    await expect(page.getByRole('button', { name })).toBeVisible();
    await expect(selector).toHaveAttribute('aria-label', name);
    await expect(selector).toHaveAttribute('title', name);
    await expect(selector).toHaveAttribute('data-theme-preference', state.preference);
    await expect(status).toHaveText(state.status);
    await expect(page.locator('html')).toHaveAttribute('data-theme-preference', state.preference);

    if (state.appliedTheme === null) {
      await expect(page.locator('html')).not.toHaveAttribute('data-theme');
    } else {
      await expect(page.locator('html')).toHaveAttribute('data-theme', state.appliedTheme);
    }

    if (index < themeStates.length - 1) {
      await selector.click();
    }
  }

  await selector.click();
  await expect(selector).toHaveAttribute('data-theme-preference', 'system');
  await expect(page.locator('html')).not.toHaveAttribute('data-theme');
  await expect
    .poll(() => page.evaluate(() => localStorage.getItem('theme-preference')))
    .toBe('system');
});

test('supports keyboard selection and restores the saved preference after reload', async ({
  page
}) => {
  const selector = page.locator('.theme-selector');

  await selector.focus();
  await page.keyboard.press('Enter');
  await expect(selector).toHaveAttribute('data-theme-preference', 'dark');
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'dark');
  await expect
    .poll(() => page.evaluate(() => localStorage.getItem('theme-preference')))
    .toBe('dark');

  await page.reload();

  await expect(
    page.getByRole('button', {
      name: 'Theme preference: Dark. Activate to use Light theme'
    })
  ).toBeVisible();
  await expect(page.locator('html')).toHaveAttribute('data-theme-preference', 'dark');
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'dark');
  await expect(page.locator('.visually-hidden[role="status"]')).toHaveText(
    'Dark theme preference selected'
  );
});
