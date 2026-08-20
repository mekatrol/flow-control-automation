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
  flowFunctions: [and, readPoint, writePoint]
  executionModes: [interval]
  runtimeFeatures: [boundPoints]
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

/**
 * Purpose: Protects the behavioral contract that keeps the default selectable but immutable.
 * Description: Exercises keeps the default selectable but immutable from its arranged starting state and
 * verifies the observable results required by the scenario.
 */
test('keeps the default selectable but immutable', async ({ page }) => {
  await page.goto('/controller-templates/default');

  // Expected outcome: `page.locator('.monaco-editor')` is visible to the user.
  // Acceptance criteria: `page.locator('.monaco-editor')` must be visible, because this condition proves that
  // keeps the default selectable but immutable.
  await expect(page.locator('.monaco-editor')).toBeVisible({ timeout: 60_000 });

  // Expected outcome: `page.getByRole('button', { name: 'Save' })` resolves to the required number of elements.
  // Acceptance criteria: `page.getByRole('button', { name: 'Save' })` must resolve to exactly 0 elements, because this condition proves that
  // keeps the default selectable but immutable.
  await expect(page.getByRole('button', { name: 'Save' })).toHaveCount(0);

  // Expected outcome: `page.getByRole('link', { name: 'Create custom template from example' })` is visible to the user.
  // Acceptance criteria: `page.getByRole('link', { name: 'Create custom template from example' })` must be visible, because this condition proves that
  // keeps the default selectable but immutable.
  await expect(page.getByRole('link', { name: 'Create custom template from example' })).toBeVisible();
  await page.waitForTimeout(250);

  // Expected outcome: `(await new AxeBuilder({ page }` matches the required structure.
  // Acceptance criteria: `(await new AxeBuilder({ page }` must equal `[]`, because this condition proves that
  // keeps the default selectable but immutable.
  await expect(async () => {
    expect((await new AxeBuilder({ page }).include('main').analyze()).violations).toEqual([]);
  }).toPass({ timeout: 10_000 });
});

/**
 * Purpose: Protects the behavioral contract that validates and saves a custom template with keyboard controls.
 * Description: Exercises validates and saves a custom template with keyboard controls from its arranged starting state and
 * verifies the observable results required by the scenario.
 */
test('validates and saves a custom template with keyboard controls', async ({ page }) => {
  await page.goto('/controller-templates/new');

  // Expected outcome: `page.locator('.monaco-editor')` is visible to the user.
  // Acceptance criteria: `page.locator('.monaco-editor')` must be visible, because this condition proves that
  // validates and saves a custom template with keyboard controls.
  await expect(page.locator('.monaco-editor')).toBeVisible({ timeout: 60_000 });
  await page.getByRole('button', { name: 'Validate' }).press('Enter');

  // Expected outcome: `page.getByText('Controller template YAML is valid.')` is present in the rendered document.
  // Acceptance criteria: `page.getByText('Controller template YAML is valid.')` must be attached to the document, because this condition proves that
  // validates and saves a custom template with keyboard controls.
  await expect(page.getByText('Controller template YAML is valid.')).toBeAttached();
  await page.getByRole('button', { name: 'Save' }).press('Enter');

  // Expected outcome: Navigation reaches the required route.
  // Acceptance criteria: the page URL must match `'/controller-templates/custom-controller'`, because this condition proves that
  // validates and saves a custom template with keyboard controls.
  await expect(page).toHaveURL('/controller-templates/custom-controller');
});
