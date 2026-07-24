import { expect, test } from './fixtures/flowTest';

/**
 * Accessibility end-to-end coverage.
 *
 * Each scenario owns one user-facing contract and receives fresh mocked API
 * state from the shared fixture, so it remains safe to run alone or in parallel.
 */

test('supports bypass navigation and modal use with only the keyboard', async ({ page }) => {
  await page.goto('/flows');

  await page.keyboard.press('Tab');
  const skipLink = page.getByRole('link', { name: 'Skip to main content' });
  await expect(skipLink).toBeFocused();
  await page.keyboard.press('Enter');
  await expect(page.locator('#main-content')).toBeFocused();

  await page.goto('/flows/climate-control');
  const deployButton = page.getByRole('button', { name: 'Deploy flow' });
  await deployButton.focus();
  await page.keyboard.press('Enter');

  const dialog = page.getByRole('alertdialog', { name: 'Deploy this flow?' });
  const cancelButton = dialog.getByRole('button', { name: 'Cancel' });
  const confirmButton = dialog.getByRole('button', { name: 'Deploy now' });
  await expect(cancelButton).toBeFocused();

  await page.keyboard.press('Shift+Tab');
  await expect(confirmButton).toBeFocused();
  await page.keyboard.press('Tab');
  await expect(cancelButton).toBeFocused();

  await page.keyboard.press('Escape');
  await expect(dialog).toBeHidden();
  await expect(deployButton).toBeFocused();

  const graph = page.getByRole('group', { name: 'Climate control flow graph' });
  await expect(graph.getByRole('button', { name: /Average temperature/ })).toBeVisible();
  await expect(graph.getByRole('button', { name: /Values, input, number/ })).toBeVisible();
});
