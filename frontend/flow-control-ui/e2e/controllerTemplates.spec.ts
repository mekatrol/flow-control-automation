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
              pointTypes: ['analog', 'digital', 'multi_state', 'integer', 'text'],
              pointDirections: ['input', 'output', 'input_output', 'value'],
              pointFeatures: ['read', 'command', 'quality'],
              connectorDataTypes: ['any', 'boolean', 'event', 'number', 'string'],
              flowFunctions: ['and', 'read-point', 'write-point'],
              executionModes: ['event', 'interval'],
              runtimeFeatures: ['virtual_points', 'bound_points']
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

test('shows and keyboard-navigates the exhaustive read-only default', async ({ page }) => {
  await page.goto('/controller-templates');
  await expect(page.getByRole('heading', { name: 'Controller templates' })).toBeVisible();
  const row = page.getByRole('row', { name: /Flow Control Automation/ });
  await expect(row).toContainText('Built-in, read-only');
  await expect(row).toContainText('analog, digital, multi state, integer, text');
  await expect(row).toContainText('any, boolean, event, number, string');
  await expect(row).toContainText('Unrestricted');
  expect((await new AxeBuilder({ page }).include('main').analyze()).violations).toEqual([]);

  await page.getByLabel('Filter controller templates').focus();
  await page.keyboard.type('Flow Control');
  await expect(row).toBeVisible();
  await page.keyboard.press('Tab');
  await expect(page.getByRole('region', { name: 'Controller templates table' })).toBeFocused();
});
