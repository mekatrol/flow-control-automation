import { expect, test } from './fixtures/flowTest';

/**
 * Accessibility end-to-end coverage.
 *
 * Each scenario owns one user-facing contract and receives fresh mocked API
 * state from the shared fixture, so it remains safe to run alone or in parallel.
 */

/**
 * Purpose: Protects the behavioral contract that supports bypass navigation and modal use with only the keyboard.
 * Description: Exercises supports bypass navigation and modal use with only the keyboard from its arranged starting state and
 * verifies the observable results required by the scenario.
 */
test('supports bypass navigation and modal use with only the keyboard', async ({ page }) => {
  await page.goto('/flows');

  await page.keyboard.press('Tab');
  const skipLink = page.getByRole('link', { name: 'Skip to main content' });

  // Expected outcome: `skipLink` owns keyboard focus.
  // Acceptance criteria: `skipLink` must be focused, because this condition proves that
  // supports bypass navigation and modal use with only the keyboard.
  await expect(skipLink).toBeFocused();
  await page.keyboard.press('Enter');

  // Expected outcome: `page.locator('#main-content')` owns keyboard focus.
  // Acceptance criteria: `page.locator('#main-content')` must be focused, because this condition proves that
  // supports bypass navigation and modal use with only the keyboard.
  await expect(page.locator('#main-content')).toBeFocused();

  await page.goto('/flows/climate-control');
  const deployButton = page.getByRole('button', { name: 'Deploy flow' });
  await deployButton.focus();
  await page.keyboard.press('Enter');

  const dialog = page.getByRole('alertdialog', { name: 'Deploy this flow?' });
  const cancelButton = dialog.getByRole('button', { name: 'Cancel' });
  const confirmButton = dialog.getByRole('button', { name: 'Deploy now' });

  // Expected outcome: `cancelButton` owns keyboard focus.
  // Acceptance criteria: `cancelButton` must be focused, because this condition proves that
  // supports bypass navigation and modal use with only the keyboard.
  await expect(cancelButton).toBeFocused();

  await page.keyboard.press('Shift+Tab');

  // Expected outcome: `confirmButton` owns keyboard focus.
  // Acceptance criteria: `confirmButton` must be focused, because this condition proves that
  // supports bypass navigation and modal use with only the keyboard.
  await expect(confirmButton).toBeFocused();
  await page.keyboard.press('Tab');

  // Expected outcome: `cancelButton` owns keyboard focus.
  // Acceptance criteria: `cancelButton` must be focused, because this condition proves that
  // supports bypass navigation and modal use with only the keyboard.
  await expect(cancelButton).toBeFocused();

  await page.keyboard.press('Escape');

  // Expected outcome: `dialog` is not exposed to the user.
  // Acceptance criteria: `dialog` must be hidden, because this condition proves that
  // supports bypass navigation and modal use with only the keyboard.
  await expect(dialog).toBeHidden();

  // Expected outcome: `deployButton` owns keyboard focus.
  // Acceptance criteria: `deployButton` must be focused, because this condition proves that
  // supports bypass navigation and modal use with only the keyboard.
  await expect(deployButton).toBeFocused();

  const graph = page.getByRole('group', { name: 'Climate control flow graph' });

  // Expected outcome: `graph.getByRole('button', { name: /Average temperature/ })` is visible to the user.
  // Acceptance criteria: `graph.getByRole('button', { name: /Average temperature/ })` must be visible, because this condition proves that
  // supports bypass navigation and modal use with only the keyboard.
  await expect(graph.getByRole('button', { name: /Average temperature/ })).toBeVisible();

  // Expected outcome: `graph.getByRole('button', { name: /Values, input, number/ })` is visible to the user.
  // Acceptance criteria: `graph.getByRole('button', { name: /Values, input, number/ })` must be visible, because this condition proves that
  // supports bypass navigation and modal use with only the keyboard.
  await expect(graph.getByRole('button', { name: /Values, input, number/ })).toBeVisible();
});
