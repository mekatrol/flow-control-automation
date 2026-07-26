import { expect, test } from './fixtures/flowTest';

import { sampleFlows } from '@/features/flows/__tests__/fixtures/sampleFlows';
import type { FlowDefinition } from '@/features/flows/types';

/**
 * Runtime end-to-end coverage.
 *
 * Each scenario owns one user-facing contract and receives fresh mocked API
 * state from the shared fixture, so it remains safe to run alone or in parallel.
 */

/**
 * Purpose: Protects the behavioral contract that confirms deployment and announces successful and failed runtime updates.
 * Description: Exercises confirms deployment and announces successful and failed runtime updates from its arranged starting state and
 * verifies the observable results required by the scenario.
 */
test('confirms deployment and announces successful and failed runtime updates', async ({
  page
}) => {
  let deployShouldFail = false;
  await page.route('**/api/flows/climate-control/deploy', async (route) => {
    if (deployShouldFail) {
      await route.fulfill({ status: 503, json: { message: 'startup failed' } });
      return;
    }
    await route.fulfill({
      json: {
        flowId: 'climate-control',
        state: 'running',
        updatedAt: '2026-07-14T08:01:00+10:00',
        nodes: {
          'temperature-average': {
            state: 'running',
            value: '22.4 C',
            updatedAt: '2026-07-14T08:01:00+10:00'
          }
        }
      }
    });
  });

  await page.goto('/flows/climate-control');

  // Expected outcome: `page.getByRole('status', { name: 'Runtime state: stopped' })` is visible to the user.
  // Acceptance criteria: `page.getByRole('status', { name: 'Runtime state: stopped' })` must be visible, because this condition proves that
  // confirms deployment and announces successful and failed runtime updates.
  await expect(page.getByRole('status', { name: 'Runtime state: stopped' })).toBeVisible();
  await page.getByRole('button', { name: 'Deploy flow' }).click();

  // Expected outcome: `page.getByRole('alertdialog', { name: 'Deploy this flow?' })` is visible to the user.
  // Acceptance criteria: `page.getByRole('alertdialog', { name: 'Deploy this flow?' })` must be visible, because this condition proves that
  // confirms deployment and announces successful and failed runtime updates.
  await expect(page.getByRole('alertdialog', { name: 'Deploy this flow?' })).toBeVisible();
  await page.getByRole('button', { name: 'Deploy now' }).click();

  // Expected outcome: `page.getByRole('status', { name: 'Runtime state: running' })` is visible to the user.
  // Acceptance criteria: `page.getByRole('status', { name: 'Runtime state: running' })` must be visible, because this condition proves that
  // confirms deployment and announces successful and failed runtime updates.
  await expect(page.getByRole('status', { name: 'Runtime state: running' })).toBeVisible();

  // Expected outcome: `page.getByRole('button', { name: /Average temperature, Calculator node, running/ })` is visible to the user.
  // Acceptance criteria: `page.getByRole('button', { name: /Average temperature, Calculator node, running/ })` must be visible, because this condition proves that
  // confirms deployment and announces successful and failed runtime updates.
  await expect(
    page.getByRole('button', { name: /Average temperature, Calculator node, running/ })
  ).toBeVisible();

  // Expected outcome: `page.getByRole('button', { name: /Average temperature, Calculator node, running, 22.4 C/ })` is visible to the user.
  // Acceptance criteria: `page.getByRole('button', { name: /Average temperature, Calculator node, running, 22.4 C/ })` must be visible, because this condition proves that
  // confirms deployment and announces successful and failed runtime updates.
  await expect(
    page.getByRole('button', { name: /Average temperature, Calculator node, running, 22.4 C/ })
  ).toBeVisible();
  const runtimeNode = page.locator('[data-node-id="temperature-average"]');

  // Expected outcome: `runtimeNode.locator('.node-status')` displays the required content.
  // Acceptance criteria: `runtimeNode.locator('.node-status')` must contain the text `'22.4 C'`, because this condition proves that
  // confirms deployment and announces successful and failed runtime updates.
  await expect(runtimeNode.locator('.node-status')).toContainText('22.4 C');

  // Expected outcome: `runtimeNode.locator('.node-marker')` resolves to the required number of elements.
  // Acceptance criteria: `runtimeNode.locator('.node-marker')` must resolve to exactly 3 elements, because this condition proves that
  // confirms deployment and announces successful and failed runtime updates.
  await expect(runtimeNode.locator('.node-marker')).toHaveCount(3);

  // Expected outcome: `runtimeNode.locator('rect.connector-port')` resolves to the required number of elements.
  // Acceptance criteria: `runtimeNode.locator('rect.connector-port')` must resolve to exactly 2 elements, because this condition proves that
  // confirms deployment and announces successful and failed runtime updates.
  await expect(runtimeNode.locator('rect.connector-port')).toHaveCount(2);

  deployShouldFail = true;
  await page.getByRole('button', { name: 'Deploy flow' }).click();
  await page.getByRole('button', { name: 'Deploy now' }).click();

  // Expected outcome: `page.getByRole('alert')` displays the required content.
  // Acceptance criteria: `page.getByRole('alert')` must contain the text `'status 503'`, because this condition proves that
  // confirms deployment and announces successful and failed runtime updates.
  await expect(page.getByRole('alert')).toContainText('status 503');

  // Expected outcome: `page.getByRole('status', { name: 'Runtime state: running' })` is visible to the user.
  // Acceptance criteria: `page.getByRole('status', { name: 'Runtime state: running' })` must be visible, because this condition proves that
  // confirms deployment and announces successful and failed runtime updates.
  await expect(page.getByRole('status', { name: 'Runtime state: running' })).toBeVisible();
});

/**
 * Purpose: Protects the behavioral contract that announces runtime errors and clears stale node values after disconnect.
 * Description: Exercises announces runtime errors and clears stale node values after disconnect from its arranged starting state and
 * verifies the observable results required by the scenario.
 */
test('announces runtime errors and clears stale node values after disconnect', async ({ page }) => {
  let connected = true;
  await page.route('**/api/flows/climate-control/runtime', async (route) => {
    if (!connected) {
      await route.fulfill({ status: 503 });
      return;
    }
    await route.fulfill({
      json: {
        flowId: 'climate-control',
        state: 'error',
        updatedAt: '2026-07-14T08:02:00+10:00',
        nodes: {
          'temperature-average': {
            state: 'error',
            value: 'Sensor unavailable',
            updatedAt: '2026-07-14T08:02:00+10:00'
          }
        }
      }
    });
  });

  await page.goto('/flows/climate-control');

  // Expected outcome: `page.getByRole('status', { name: 'Runtime state: error' })` is visible to the user.
  // Acceptance criteria: `page.getByRole('status', { name: 'Runtime state: error' })` must be visible, because this condition proves that
  // announces runtime errors and clears stale node values after disconnect.
  await expect(page.getByRole('status', { name: 'Runtime state: error' })).toBeVisible();

  // Expected outcome: `page.getByRole('button', { name: /Average temperature, Calculator node, error, Sensor unavailable/ }` is visible to the user.
  // Acceptance criteria: `page.getByRole('button', { name: /Average temperature, Calculator node, error, Sensor unavailable/ }` must be visible, because this condition proves that
  // announces runtime errors and clears stale node values after disconnect.
  await expect(
    page.getByRole('button', {
      name: /Average temperature, Calculator node, error, Sensor unavailable/
    })
  ).toBeVisible();

  connected = false;
  await page.getByRole('button', { name: 'Refresh runtime' }).click();

  // Expected outcome: `page.getByRole('alert')` displays the required content.
  // Acceptance criteria: `page.getByRole('alert')` must contain the text `'status 503'`, because this condition proves that
  // announces runtime errors and clears stale node values after disconnect.
  await expect(page.getByRole('alert')).toContainText('status 503');

  // Expected outcome: `page.getByRole('status', { name: 'Runtime state: disconnected' })` is visible to the user.
  // Acceptance criteria: `page.getByRole('status', { name: 'Runtime state: disconnected' })` must be visible, because this condition proves that
  // announces runtime errors and clears stale node values after disconnect.
  await expect(page.getByRole('status', { name: 'Runtime state: disconnected' })).toBeVisible();

  // Expected outcome: `page.getByRole('button', { name: /Sensor unavailable/ })` resolves to the required number of elements.
  // Acceptance criteria: `page.getByRole('button', { name: /Sensor unavailable/ })` must resolve to exactly 0 elements, because this condition proves that
  // announces runtime errors and clears stale node values after disconnect.
  await expect(page.getByRole('button', { name: /Sensor unavailable/ })).toHaveCount(0);
});

/**
 * Purpose: Protects the behavioral contract that announces deployed node state independently of colour.
 * Description: Exercises announces deployed node state independently of colour from its arranged starting state and
 * verifies the observable results required by the scenario.
 */
test('announces deployed node state independently of colour', async ({ page }) => {
  await page.goto('/flows/garden-irrigation');

  // Expected outcome: `page.getByRole('button', { name: /Watering pulse, Pulse node, deployed/ })` is visible to the user.
  // Acceptance criteria: `page.getByRole('button', { name: /Watering pulse, Pulse node, deployed/ })` must be visible, because this condition proves that
  // announces deployed node state independently of colour.
  await expect(
    page.getByRole('button', { name: /Watering pulse, Pulse node, deployed/ })
  ).toBeVisible();
});

/**
 * Purpose: Protects the behavioral contract that disables execution without changing deployment status.
 * Description: Exercises disables execution without changing deployment status from its arranged starting state and
 * verifies the observable results required by the scenario.
 */
test('disables execution without changing deployment status', async ({ page }) => {
  let disabled = false;
  const deployedFlow = (): FlowDefinition => ({
    ...structuredClone(sampleFlows[1]!),
    disabled
  });
  await page.route('**/api/flows/garden-irrigation', async (route) => {
    await route.fulfill({ json: deployedFlow() });
  });
  await page.route('**/api/flows/garden-irrigation/disable', async (route) => {
    disabled = true;
    await route.fulfill({ json: deployedFlow() });
  });
  await page.route('**/api/flows/garden-irrigation/enable', async (route) => {
    disabled = false;
    await route.fulfill({ json: deployedFlow() });
  });
  await page.route('**/api/flows/garden-irrigation/runtime', async (route) => {
    await route.fulfill({
      json: {
        flowId: 'garden-irrigation',
        state: disabled ? 'stopped' : 'running',
        updatedAt: '2026-07-14T08:01:00+10:00',
        nodes: {}
      }
    });
  });

  await page.goto('/flows/garden-irrigation');
  await page.getByRole('button', { name: 'Disable' }).click();
  const titleRow = page.locator('.title-row');

  // Expected outcome: `titleRow.getByText('deployed', { exact: true })` is visible to the user.
  // Acceptance criteria: `titleRow.getByText('deployed', { exact: true })` must be visible, because this condition proves that
  // disables execution without changing deployment status.
  await expect(titleRow.getByText('deployed', { exact: true })).toBeVisible();

  // Expected outcome: `titleRow.getByText('disabled', { exact: true })` is visible to the user.
  // Acceptance criteria: `titleRow.getByText('disabled', { exact: true })` must be visible, because this condition proves that
  // disables execution without changing deployment status.
  await expect(titleRow.getByText('disabled', { exact: true })).toBeVisible();

  // Expected outcome: `page.getByRole('status', { name: 'Runtime state: stopped' })` is visible to the user.
  // Acceptance criteria: `page.getByRole('status', { name: 'Runtime state: stopped' })` must be visible, because this condition proves that
  // disables execution without changing deployment status.
  await expect(page.getByRole('status', { name: 'Runtime state: stopped' })).toBeVisible();

  await page.getByRole('button', { name: 'Enable' }).click();

  // Expected outcome: `titleRow.getByText('disabled', { exact: true })` resolves to the required number of elements.
  // Acceptance criteria: `titleRow.getByText('disabled', { exact: true })` must resolve to exactly 0 elements, because this condition proves that
  // disables execution without changing deployment status.
  await expect(titleRow.getByText('disabled', { exact: true })).toHaveCount(0);

  // Expected outcome: `page.getByRole('status', { name: 'Runtime state: running' })` is visible to the user.
  // Acceptance criteria: `page.getByRole('status', { name: 'Runtime state: running' })` must be visible, because this condition proves that
  // disables execution without changing deployment status.
  await expect(page.getByRole('status', { name: 'Runtime state: running' })).toBeVisible();
});
