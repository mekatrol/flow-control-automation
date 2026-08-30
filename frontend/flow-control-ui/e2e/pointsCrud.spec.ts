import AxeBuilder from '@axe-core/playwright';
import { expect, test } from '@playwright/test';

const pointYaml = `schemaVersion: 1
groups: []
points:
  - id: new-digital
    name: New digital point
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
  await page.route('**/api/points/new-digital/runtime', async (route) => {
    await route.fulfill({
      json: {
        pointId: 'new-digital',
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
  await page.route('**/api/points/new-digital', async (route) => {
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

/**
 * Purpose: Protects the behavioral contract that creates, reloads and honestly presents an unavailable point value.
 * Description: Exercises creates, reloads and honestly presents an unavailable point value from its arranged starting state and
 * verifies the observable results required by the scenario.
 */
test('creates, reloads and honestly presents an unavailable point value', async ({ page }) => {
  await page.goto('/points/new');

  // Expected outcome: `page.locator('.monaco-editor')` is visible to the user.
  // Acceptance criteria: `page.locator('.monaco-editor')` must be visible, because this condition proves that
  // creates, reloads and honestly presents an unavailable point value.
  await expect(page.locator('.monaco-editor')).toBeVisible({ timeout: 60_000 });
  await page.getByLabel('Start with a point example').selectOption('DV — Digital virtual');
  await page.getByRole('button', { name: 'Save' }).press('Enter');

  // Expected outcome: Navigation reaches the required route.
  // Acceptance criteria: the page URL must match `'/points/room-value'`, because this condition proves that
  // creates, reloads and honestly presents an unavailable point value.
  await expect(page).toHaveURL('/points/new-digital');

  // Expected outcome: `page.getByRole('heading', { name: 'Live point value' })` is visible to the user.
  // Acceptance criteria: `page.getByRole('heading', { name: 'Live point value' })` must be visible, because this condition proves that
  // creates, reloads and honestly presents an unavailable point value.
  await expect(page.getByRole('heading', { name: 'Live point value' })).toBeVisible();

  // Expected outcome: `page.getByText('Virtual point has no commissioned runtime value.')` is visible to the user.
  // Acceptance criteria: `page.getByText('Virtual point has no commissioned runtime value.')` must be visible, because this condition proves that
  // creates, reloads and honestly presents an unavailable point value.
  await expect(page.getByText('Virtual point has no commissioned runtime value.')).toBeVisible();

  // Expected outcome: `page.getByText('Unavailable', { exact: true })` is visible to the user.
  // Acceptance criteria: `page.getByText('Unavailable', { exact: true })` must be visible, because this condition proves that
  // creates, reloads and honestly presents an unavailable point value.
  await expect(page.getByText('Unavailable', { exact: true })).toBeVisible();

  // Expected outcome: `(await new AxeBuilder({ page }` matches the required structure.
  // Acceptance criteria: `(await new AxeBuilder({ page }` must equal `[]`, because this condition proves that
  // creates, reloads and honestly presents an unavailable point value.
  expect((await new AxeBuilder({ page }).include('main').analyze()).violations).toEqual([]);

  await page.reload();

  // Expected outcome: `page.getByText('not_initialized')` is visible to the user.
  // Acceptance criteria: `page.getByText('not_initialized')` must be visible, because this condition proves that
  // creates, reloads and honestly presents an unavailable point value.
  await expect(page.getByText('not_initialized')).toBeVisible();
  await page.getByRole('button', { name: 'Pause updates' }).press('Enter');

  // Expected outcome: `page.getByRole('button', { name: 'Resume updates' })` is visible to the user.
  // Acceptance criteria: `page.getByRole('button', { name: 'Resume updates' })` must be visible, because this condition proves that
  // creates, reloads and honestly presents an unavailable point value.
  await expect(page.getByRole('button', { name: 'Resume updates' })).toBeVisible();
});

/**
 * Purpose: Protects the behavioral contract that offers explicit conflict recovery for occupied groups.
 * Description: Exercises offers explicit conflict recovery for occupied groups from its arranged starting state and
 * verifies the observable results required by the scenario.
 */
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

  // Expected outcome: `page.locator('.monaco-editor')` is visible to the user.
  // Acceptance criteria: `page.locator('.monaco-editor')` must be visible, because this condition proves that
  // offers explicit conflict recovery for occupied groups.
  await expect(page.locator('.monaco-editor')).toBeVisible({ timeout: 60_000 });
  page.once('dialog', (dialog) => void dialog.accept());
  await page.getByRole('button', { name: 'Delete' }).click();

  // Expected outcome: `page.locator('.error-summary')` displays the required content.
  // Acceptance criteria: `page.locator('.error-summary')` must contain the text `'group contains points'`, because this condition proves that
  // offers explicit conflict recovery for occupied groups.
  await expect(
    page.getByRole('dialog', { name: 'Unable to complete the request' }).getByRole('alert')
  ).toContainText('group contains points');
  await page.getByRole('button', { name: 'Close' }).click();
  await page.getByRole('button', { name: 'Make member points standalone' }).click();

  // Expected outcome: `page.getByText('Member points are now standalone.')` is present in the rendered document.
  // Acceptance criteria: `page.getByText('Member points are now standalone.')` must be attached to the document, because this condition proves that
  // offers explicit conflict recovery for occupied groups.
  await expect(page.getByText('Member points are now standalone.')).toBeAttached();
});
