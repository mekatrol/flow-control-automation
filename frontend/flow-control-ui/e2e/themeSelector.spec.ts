import { expect, test } from '@playwright/test';

import { flowsCollectionPattern, pagedFlows } from './fixtures/flowTest';

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
  await page.route(flowsCollectionPattern, async (route) => {
    await route.fulfill({ json: pagedFlows([], route.request().url()) });
  });
  await page.goto('/flows');
});

/**
 * Purpose: Protects the behavioral contract that cycles through every theme and exposes the correct accessible state.
 * Description: Exercises cycles through every theme and exposes the correct accessible state from its arranged starting state and
 * verifies the observable results required by the scenario.
 */
test('cycles through every theme and exposes the correct accessible state', async ({ page }) => {
  const selector = page.locator('.theme-selector');
  const help = page.locator('#theme-selector-help');
  const status = page.locator('.visually-hidden[role="status"]');

  await expect(selector).toHaveCSS('width', '72px');
  await expect(selector).toHaveCSS('height', '40px');
  await expect(selector.locator('.button-icon-slot')).toHaveCSS('width', '64px');
  await expect(selector.locator('.button-icon-slot')).toHaveCSS('height', '32px');

  // Expected outcome: `selector` exposes the required attribute.
  // Acceptance criteria: `selector` must have attribute arguments `'aria-describedby', 'theme-selector-help'`, because this condition proves that
  // cycles through every theme and exposes the correct accessible state.
  await expect(selector).toHaveAttribute('aria-describedby', 'theme-selector-help');

  // Expected outcome: `help` displays the required text.
  // Acceptance criteria: `help` must display `'Cycles between system, dark, and light theme preferences.'`, because this condition proves that
  // cycles through every theme and exposes the correct accessible state.
  await expect(help).toHaveText('Cycles between system, dark, and light theme preferences.');

  // Expected outcome: `status` exposes the required attribute.
  // Acceptance criteria: `status` must have attribute arguments `'aria-live', 'polite'`, because this condition proves that
  // cycles through every theme and exposes the correct accessible state.
  await expect(status).toHaveAttribute('aria-live', 'polite');

  // Expected outcome: `status` exposes the required attribute.
  // Acceptance criteria: `status` must have attribute arguments `'aria-atomic', 'true'`, because this condition proves that
  // cycles through every theme and exposes the correct accessible state.
  await expect(status).toHaveAttribute('aria-atomic', 'true');

  // Expected outcome: `selector.locator('.theme-selector-icon')` exposes the required attribute.
  // Acceptance criteria: `selector.locator('.theme-selector-icon')` must have attribute arguments `'aria-hidden', 'true'`, because this condition proves that
  // cycles through every theme and exposes the correct accessible state.
  await expect(selector.locator('.theme-selector-icon')).toHaveAttribute('aria-hidden', 'true');

  for (const [index, state] of themeStates.entries()) {
    const name = `Theme preference: ${
      state.preference[0]!.toUpperCase() + state.preference.slice(1)
    }. Activate to use ${state.next} theme`;

    // Expected outcome: `page.getByRole('button', { name })` is visible to the user.
    // Acceptance criteria: `page.getByRole('button', { name })` must be visible, because this condition proves that
    // cycles through every theme and exposes the correct accessible state.
    await expect(page.getByRole('button', { name })).toBeVisible();

    // Expected outcome: `selector` exposes the required attribute.
    // Acceptance criteria: `selector` must have attribute arguments `'aria-label', name`, because this condition proves that
    // cycles through every theme and exposes the correct accessible state.
    await expect(selector).toHaveAttribute('aria-label', name);

    // Expected outcome: `selector` exposes the required attribute.
    // Acceptance criteria: `selector` must have attribute arguments `'title', name`, because this condition proves that
    // cycles through every theme and exposes the correct accessible state.
    await expect(selector).toHaveAttribute('title', name);

    // Expected outcome: `selector` exposes the required attribute.
    // Acceptance criteria: `selector` must have attribute arguments `'data-theme-preference', state.preference`, because this condition proves that
    // cycles through every theme and exposes the correct accessible state.
    await expect(selector).toHaveAttribute('data-theme-preference', state.preference);

    // Expected outcome: `status` displays the required text.
    // Acceptance criteria: `status` must display `state.status`, because this condition proves that
    // cycles through every theme and exposes the correct accessible state.
    await expect(status).toHaveText(state.status);

    // Expected outcome: `page.locator('html')` exposes the required attribute.
    // Acceptance criteria: `page.locator('html')` must have attribute arguments `'data-theme-preference', state.preference`, because this condition proves that
    // cycles through every theme and exposes the correct accessible state.
    await expect(page.locator('html')).toHaveAttribute('data-theme-preference', state.preference);

    if (state.appliedTheme === null) {

      // Expected outcome: `page.locator('html')` exposes the required attribute.
      // Acceptance criteria: `page.locator('html')` must have attribute arguments `'data-theme'`, because this condition proves that
      // cycles through every theme and exposes the correct accessible state.
      await expect(page.locator('html')).not.toHaveAttribute('data-theme');
    } else {

      // Expected outcome: `page.locator('html')` exposes the required attribute.
      // Acceptance criteria: `page.locator('html')` must have attribute arguments `'data-theme', state.appliedTheme`, because this condition proves that
      // cycles through every theme and exposes the correct accessible state.
      await expect(page.locator('html')).toHaveAttribute('data-theme', state.appliedTheme);
    }

    if (index < themeStates.length - 1) {
      await selector.click();
    }
  }

  await selector.click();

  // Expected outcome: `selector` exposes the required attribute.
  // Acceptance criteria: `selector` must have attribute arguments `'data-theme-preference', 'system'`, because this condition proves that
  // cycles through every theme and exposes the correct accessible state.
  await expect(selector).toHaveAttribute('data-theme-preference', 'system');

  // Expected outcome: `page.locator('html')` exposes the required attribute.
  // Acceptance criteria: `page.locator('html')` must have attribute arguments `'data-theme'`, because this condition proves that
  // cycles through every theme and exposes the correct accessible state.
  await expect(page.locator('html')).not.toHaveAttribute('data-theme');
  await expect
    .poll(() => page.evaluate(() => localStorage.getItem('theme-preference')))
    .toBe('system');
});

/**
 * Purpose: Protects the behavioral contract that supports keyboard selection and restores the saved preference after reload.
 * Description: Exercises supports keyboard selection and restores the saved preference after reload from its arranged starting state and
 * verifies the observable results required by the scenario.
 */
test('supports keyboard selection and restores the saved preference after reload', async ({
  page
}) => {
  const selector = page.locator('.theme-selector');

  await selector.focus();
  await page.keyboard.press('Enter');

  // Expected outcome: `selector` exposes the required attribute.
  // Acceptance criteria: `selector` must have attribute arguments `'data-theme-preference', 'dark'`, because this condition proves that
  // supports keyboard selection and restores the saved preference after reload.
  await expect(selector).toHaveAttribute('data-theme-preference', 'dark');

  // Expected outcome: `page.locator('html')` exposes the required attribute.
  // Acceptance criteria: `page.locator('html')` must have attribute arguments `'data-theme', 'dark'`, because this condition proves that
  // supports keyboard selection and restores the saved preference after reload.
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'dark');
  await expect
    .poll(() => page.evaluate(() => localStorage.getItem('theme-preference')))
    .toBe('dark');

  await page.reload();

  // Expected outcome: `page.getByRole('button', { name: 'Theme preference: Dark. Activate to use Light theme' })` is visible to the user.
  // Acceptance criteria: `page.getByRole('button', { name: 'Theme preference: Dark. Activate to use Light theme' })` must be visible, because this condition proves that
  // supports keyboard selection and restores the saved preference after reload.
  await expect(
    page.getByRole('button', {
      name: 'Theme preference: Dark. Activate to use Light theme'
    })
  ).toBeVisible();

  // Expected outcome: `page.locator('html')` exposes the required attribute.
  // Acceptance criteria: `page.locator('html')` must have attribute arguments `'data-theme-preference', 'dark'`, because this condition proves that
  // supports keyboard selection and restores the saved preference after reload.
  await expect(page.locator('html')).toHaveAttribute('data-theme-preference', 'dark');

  // Expected outcome: `page.locator('html')` exposes the required attribute.
  // Acceptance criteria: `page.locator('html')` must have attribute arguments `'data-theme', 'dark'`, because this condition proves that
  // supports keyboard selection and restores the saved preference after reload.
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'dark');

  // Expected outcome: `page.locator('.visually-hidden[role="status"]')` displays the required text.
  // Acceptance criteria: `page.locator('.visually-hidden[role="status"]')` must display `'Dark theme preference selected'`, because this condition proves that
  // supports keyboard selection and restores the saved preference after reload.
  await expect(page.locator('.visually-hidden[role="status"]')).toHaveText(
    'Dark theme preference selected'
  );
});
