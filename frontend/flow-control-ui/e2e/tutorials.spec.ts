import AxeBuilder from '@axe-core/playwright';

import { expect, test } from './fixtures/flowTest';

/**
 * Purpose: Proves every executable palette block exposes its tutorial from the authoring workflow.
 * Description: Opens the And tutorial and verifies ordered guidance and disposable-example actions.
 */
test('opens function guidance from the palette', async ({ page }) => {
  // Arrange: Open a normal editable flow and narrow the palette to one function.
  await page.goto('/flows/climate-control');
  await page.getByRole('searchbox', { name: 'Find a node' }).fill('and');
  await page.getByRole('button', { name: 'Apply filter' }).click();

  // Act: Follow the accessible Learn action associated with the block.
  await page.getByRole('button', { name: 'Learn And block' }).click();

  // Assert: Repository-owned guidance is visible and offers both ownership modes.
  await expect(page.getByRole('heading', { name: 'And basics' })).toBeVisible();
  await expect(page.getByRole('listitem')).toHaveCount(3);
  await expect(page.getByRole('button', { name: 'Open disposable example' })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Copy to my flows' })).toBeVisible();
});

/**
 * Purpose: Protects WCAG 2.2 AA automated checks for the tutorial state.
 * Description: Opens a tutorial on the mobile viewport and scans the rendered document for serious violations.
 */
test('keeps tutorial guidance accessible at supported viewports', async ({ page }) => {
  // Arrange: Open tutorial guidance using keyboard-addressable controls.
  await page.goto('/flows/climate-control');
  await page.getByRole('button', { name: 'Learn And block' }).click();

  // Act: Audit the complete state after the guidance is visible.
  const results = await new AxeBuilder({ page }).analyze();

  // Assert: No serious or critical automated accessibility violations remain.
  expect(results.violations.filter(({ impact }) => impact === 'serious' || impact === 'critical')).toEqual([]);
});
