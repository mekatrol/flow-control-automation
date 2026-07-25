import AxeBuilder from '@axe-core/playwright';
import { expect, test } from '@playwright/test';

const templateYaml = `schemaVersion: 1
id: custom-controller
name: Custom controller
readOnly: false
capabilities:
  pointTypes: [digital]
  pointDirections: [input, output]
  pointFeatures: [read, command]
  connectorDataTypes: [boolean]
  flowFunctions: [and, read-point, write-point]
  executionModes: [interval]
  runtimeFeatures: [bound_points]
limits:
  maxFlows: 8
`;

test.beforeEach(async ({ page }) => {
  await page.route('**/api/controller-templates/default/yaml', async (route) => {
    await route.fulfill({
      body: templateYaml.replace('custom-controller', 'default').replace('false', 'true'),
      headers: { 'Content-Type': 'application/yaml', ETag: '0' }
    });
  });
  await page.route('**/api/controller-templates/custom-controller/yaml', async (route) => {
    await route.fulfill({
      body: templateYaml,
      headers: { 'Content-Type': 'application/yaml', ETag: '1' }
    });
  });
  await page.route('**/api/controller-templates/validate', async (route) => {
    await route.fulfill({ json: { valid: true, diagnostics: [] } });
  });
  await page.route('**/api/controller-templates', async (route) => {
    await route.fulfill({
      status: 201,
      json: { id: 'custom-controller' },
      headers: { ETag: '1' }
    });
  });
});

test('keeps the default selectable but immutable', async ({ page }) => {
  await page.goto('/controller-templates/default');
  await expect(page.locator('.monaco-editor')).toBeVisible({ timeout: 60_000 });
  await expect(page.getByRole('button', { name: 'Save' })).toHaveCount(0);
  await expect(page.getByRole('link', { name: 'Create custom template from example' })).toBeVisible();
  await page.waitForTimeout(250);
  expect((await new AxeBuilder({ page }).include('main').analyze()).violations).toEqual([]);
});

test('validates and saves a custom template with keyboard controls', async ({ page }) => {
  await page.goto('/controller-templates/new');
  await expect(page.locator('.monaco-editor')).toBeVisible({ timeout: 60_000 });
  await page.getByRole('button', { name: 'Validate' }).press('Enter');
  await expect(page.getByText('Controller template YAML is valid.')).toBeAttached();
  await page.getByRole('button', { name: 'Save' }).press('Enter');
  await expect(page).toHaveURL('/controller-templates/custom-controller');
});
