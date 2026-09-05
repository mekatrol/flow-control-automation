import { expect, test } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';

test.beforeEach(async ({ page }) => {
  await page.route('**/api/controller-templates', async (route) => {
    await route.fulfill({
      json: {
        items: [
          {
            schemaVersion: 1,
            id: 'default',
            name: 'Flow Control Automation',
            description: 'Built-in unrestricted application target',
            readOnly: true,
            capabilities: {
              pointTypes: ['analog', 'digital', 'multiState', 'integer', 'text'],
              pointDirections: ['input', 'output', 'inputOutput', 'value'],
              pointFeatures: ['read', 'command', 'quality'],
              connectorDataTypes: ['any', 'boolean', 'event', 'number', 'string'],
              flowFunctions: ['and', 'readPoint', 'writePoint'],
              executionModes: ['event', 'interval'],
              runtimeFeatures: ['virtualPoints', 'physicalPoints']
            },
            limits: {
              maxFlows: null,
              maxNodesPerFlow: null,
              maxConnectionsPerFlow: null,
              minimumIntervalMilliseconds: null
            },
            revision: 0
          }
        ]
      }
    });
  });
});

/**
 * Purpose: Protects the behavioral contract that shows and keyboard-navigates the exhaustive read-only default.
 * Description: Exercises shows and keyboard-navigates the exhaustive read-only default from its arranged starting state and
 * verifies the observable results required by the scenario.
 */
test('shows and keyboard-navigates the exhaustive read-only default', async ({ page }) => {
  await page.goto('/controller-templates');

  // Expected outcome: `page.getByRole('heading', { name: 'Controller templates' })` is visible to the user.
  // Acceptance criteria: `page.getByRole('heading', { name: 'Controller templates' })` must be visible, because this condition proves that
  // shows and keyboard-navigates the exhaustive read-only default.
  await expect(page.getByRole('heading', { name: 'Controller templates' })).toBeVisible();
  const row = page.getByRole('row', { name: /Flow Control Automation/ });

  // Expected outcome: `row` displays the required content.
  // Acceptance criteria: `row` must contain the text `'Built-in, read-only'`, because this condition proves that
  // shows and keyboard-navigates the exhaustive read-only default.
  await expect(row).toContainText('Built-in, read-only');

  // Expected outcome: `row` displays the required content.
  // Acceptance criteria: `row` must contain the text `'analog, digital, multi state, integer, text'`, because this condition proves that
  // shows and keyboard-navigates the exhaustive read-only default.
  await expect(row).toContainText('analog, digital, multi state, integer, text');

  // Expected outcome: `row` displays the required content.
  // Acceptance criteria: `row` must contain the text `'any, boolean, event, number, string'`, because this condition proves that
  // shows and keyboard-navigates the exhaustive read-only default.
  await expect(row).toContainText('any, boolean, event, number, string');

  // Expected outcome: `row` displays the required content.
  // Acceptance criteria: `row` must contain the text `'Unrestricted'`, because this condition proves that
  // shows and keyboard-navigates the exhaustive read-only default.
  await expect(row).toContainText('Unrestricted');

  // Expected outcome: `(await new AxeBuilder({ page }` matches the required structure.
  // Acceptance criteria: `(await new AxeBuilder({ page }` must equal `[]`, because this condition proves that
  // shows and keyboard-navigates the exhaustive read-only default.
  expect((await new AxeBuilder({ page }).include('main').analyze()).violations).toEqual([]);

  await page.getByLabel('Filter controller templates').focus();
  await page.keyboard.type('Flow Control');
  await page.getByRole('button', { name: 'Apply filter' }).click();

  // Expected outcome: `row` is visible to the user.
  // Acceptance criteria: `row` must be visible, because this condition proves that
  // shows and keyboard-navigates the exhaustive read-only default.
  await expect(row).toBeVisible();
  await page.getByRole('button', { name: 'Apply filter' }).press('Tab');

  // Expected outcome: `page.getByRole('region', { name: 'Controller templates table' })` owns keyboard focus.
  // Acceptance criteria: `page.getByRole('region', { name: 'Controller templates table' })` must be focused, because this condition proves that
  // shows and keyboard-navigates the exhaustive read-only default.
  await expect(page.getByRole('region', { name: 'Controller templates table' })).toBeFocused();
});
