import { expect, test } from './fixtures/flowTest';

/**
 * Runtime end-to-end coverage.
 *
 * Each scenario owns one user-facing contract and receives fresh mocked API
 * state from the shared fixture, so it remains safe to run alone or in parallel.
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
  await expect(page.getByRole('status', { name: 'Runtime state: stopped' })).toBeVisible();
  await page.getByRole('button', { name: 'Deploy flow' }).click();
  await expect(page.getByRole('alertdialog', { name: 'Deploy this flow?' })).toBeVisible();
  await page.getByRole('button', { name: 'Deploy now' }).click();

  await expect(page.getByRole('status', { name: 'Runtime state: running' })).toBeVisible();
  await expect(
    page.getByRole('button', { name: /Average temperature, Calculator node, running/ })
  ).toBeVisible();
  await expect(
    page.getByRole('button', { name: /Average temperature, Calculator node, running, 22.4 C/ })
  ).toBeVisible();
  const runtimeNode = page.locator('[data-node-id="temperature-average"]');
  await expect(runtimeNode.locator('.node-status')).toContainText('22.4 C');
  await expect(runtimeNode.locator('.node-marker')).toHaveCount(3);
  await expect(runtimeNode.locator('rect.connector-port')).toHaveCount(2);

  deployShouldFail = true;
  await page.getByRole('button', { name: 'Deploy flow' }).click();
  await page.getByRole('button', { name: 'Deploy now' }).click();
  await expect(page.getByRole('alert')).toContainText('status 503');
  await expect(page.getByRole('status', { name: 'Runtime state: running' })).toBeVisible();
});

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
  await expect(page.getByRole('status', { name: 'Runtime state: error' })).toBeVisible();
  await expect(
    page.getByRole('button', {
      name: /Average temperature, Calculator node, error, Sensor unavailable/
    })
  ).toBeVisible();

  connected = false;
  await page.getByRole('button', { name: 'Refresh runtime' }).click();
  await expect(page.getByRole('alert')).toContainText('status 503');
  await expect(page.getByRole('status', { name: 'Runtime state: disconnected' })).toBeVisible();
  await expect(page.getByRole('button', { name: /Sensor unavailable/ })).toHaveCount(0);
});

test('announces deployed node state independently of colour', async ({ page }) => {
  await page.goto('/flows/garden-irrigation');

  await expect(
    page.getByRole('button', { name: /Watering pulse, Pulse node, deployed/ })
  ).toBeVisible();
});
