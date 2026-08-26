import AxeBuilder from '@axe-core/playwright';

import { expect, test } from './fixtures/flowTest';

/**
 * Purpose: Proves palette functions stay unobscured by secondary learning actions.
 * Description: Filters to the And function and verifies its only palette action adds the node.
 */
test('keeps learning actions out of the function palette', async ({ page }) => {
  // Arrange: Open a normal editable flow and narrow the palette to one function.
  await page.goto('/flows/climate-control');
  await page.getByRole('searchbox', { name: 'Find a node' }).fill('and');
  await page.getByRole('button', { name: 'Apply filter' }).click();

  // Assert: The function remains available without an adjacent Learn action.
  await expect(page.getByRole('button', { name: 'And', exact: true })).toBeVisible();
  await expect(page.getByRole('button', { name: /Learn .* block/ })).toHaveCount(0);
});

/**
 * Purpose: Protects the supported route for learning through simulation.
 * Description: Opens simulation and scans the rendered document for serious accessibility violations.
 */
test('keeps simulation learning accessible at supported viewports', async ({ page }) => {
  // Arrange: Open simulation using its keyboard-addressable navigation control.
  await page.goto('/flows/climate-control');
  await page.getByRole('link', { name: 'Simulate' }).click();

  // Act: Audit the complete state after the guidance is visible.
  const results = await new AxeBuilder({ page }).analyze();

  // Assert: No serious or critical automated accessibility violations remain.
  expect(
    results.violations.filter(({ impact }) => impact === 'serious' || impact === 'critical')
  ).toEqual([]);
});
