import AxeBuilder from '@axe-core/playwright';
import { expect, test } from '@playwright/test';

const pointYaml = `schemaVersion: 1
groups: []
points:
  - id: room-value
    name: Room value
    enabled: true
    implementation: virtual
    direction: value
    valueType: analog
    units: percent
    readable: true
    commandable: true
    persistence: volatile
`;

test.beforeEach(async ({ page }) => {
  await page.route('**/api/points/room-value/runtime', async (route) => {
    await route.fulfill({
      json: {
        pointId: 'room-value',
        value: null,
        units: 'percent',
        quality: 'unavailable',
        reliability: 'not_initialized',
        sourceTimestamp: null,
        updatedAt: null,
        connectionState: 'disconnected',
        status: 'unavailable',
        diagnostic: 'Virtual point has no commissioned runtime value.'
      }
    });
  });
  await page.route('**/api/points/room-value', async (route) => {
    await route.fulfill({
      status: 200,
      body: pointYaml,
      headers: { 'Content-Type': 'application/yaml', ETag: '1' }
    });
  });
  await page.route('**/api/points', async (route) => {
    if (route.request().method() === 'POST') {
      await route.fulfill({
        status: 201,
        body: pointYaml,
        headers: { 'Content-Type': 'application/yaml', ETag: '1' }
      });
    } else {
      await route.fulfill({
        json: { items: [], totalItems: 0, page: 1, pageSize: 10, pageCount: 0 }
      });
    }
  });
});

test('creates, reloads and honestly presents an unavailable point value', async ({ page }) => {
  await page.goto('/points/new');
  await expect(page.locator('.monaco-editor')).toBeVisible({ timeout: 60_000 });
  await page.getByLabel('Start with a point example').selectOption('Digital retained');
  await page.getByRole('button', { name: 'Save' }).press('Enter');
  await expect(page).toHaveURL('/points/room-value');
  await expect(page.getByRole('heading', { name: 'Live point value' })).toBeVisible();
  await expect(page.getByText('Virtual point has no commissioned runtime value.')).toBeVisible();
  await expect(page.getByText('Unavailable', { exact: true })).toBeVisible();
  expect((await new AxeBuilder({ page }).include('main').analyze()).violations).toEqual([]);

  await page.reload();
  await expect(page.getByText('not_initialized')).toBeVisible();
  await page.getByRole('button', { name: 'Pause updates' }).press('Enter');
  await expect(page.getByRole('button', { name: 'Resume updates' })).toBeVisible();
});

test('offers explicit conflict recovery for occupied groups', async ({ page }) => {
  const groupYaml = `schemaVersion: 1
groups:
  - id: room
    name: Room
points: []
`;
  await page.route('**/api/point-groups/room?revision=1', async (route) => {
    await route.fulfill({ status: 409, json: { message: 'group contains points' } });
  });
  await page.route('**/api/point-groups/room/make-points-standalone?revision=1', async (route) => {
    await route.fulfill({ json: { items: [], updatedItems: 1 } });
  });
  await page.route('**/api/point-groups/room', async (route) => {
    await route.fulfill({
      body: groupYaml,
      headers: { 'Content-Type': 'application/yaml', ETag: '1' }
    });
  });
  await page.goto('/point-groups/room');
  await expect(page.locator('.monaco-editor')).toBeVisible({ timeout: 60_000 });
  page.once('dialog', (dialog) => void dialog.accept());
  await page.getByRole('button', { name: 'Delete' }).click();
  await expect(page.locator('.error-summary')).toContainText('group contains points');
  await page.getByRole('button', { name: 'Make member points standalone' }).click();
  await expect(page.getByText('Member points are now standalone.')).toBeAttached();
});
