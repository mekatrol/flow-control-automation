import { expect, test } from '@playwright/test';

test('opens YAML help while its guidance is loading', async ({ page }) => {
  let releaseGuidance: () => void = () => undefined;
  const guidanceReleased = new Promise<void>((resolve) => {
    releaseGuidance = resolve;
  });
  await page.route('**/api/configuration-guidance/point', async (route) => {
    await guidanceReleased;
    await route.fulfill({ contentType: 'text/markdown', body: '# Point guidance' });
  });
  await page.goto('/points/new');
  await expect(page.locator('.monaco-editor')).toBeVisible({ timeout: 60_000 });

  await page.getByRole('button', { name: 'YAML help' }).click();

  const panel = page.getByRole('complementary', { name: 'YAML configuration guidance' });
  await expect(panel).toBeVisible();
  await expect(panel.getByRole('status')).toHaveText('Generating guidance…');
  await expect(page.locator('.spinner-overlay')).toHaveCount(0);

  releaseGuidance();
  await expect(panel.getByRole('heading', { name: 'Point guidance' })).toBeVisible();
});
