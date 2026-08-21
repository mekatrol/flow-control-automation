import AxeBuilder from '@axe-core/playwright';

import { expect, test } from './fixtures/flowTest';

interface CataloguePoint {
  id: string;
  name: string;
  enabled: boolean;
  groupId: string | null;
  implementation: string;
  direction: string;
  valueType: string;
  units: string | null;
  readable: boolean;
  commandable: boolean;
  persistence: string;
  sourceId: null;
  revision: number;
}

const point = (index: number): CataloguePoint => ({
  id: `point-${index}`,
  name: `Point ${String(index).padStart(2, '0')}`,
  enabled: index % 2 === 0,
  groupId: index === 1 ? 'room' : null,
  implementation: index === 1 ? 'bound' : 'virtual',
  direction: index === 1 ? 'input' : 'value',
  valueType: index === 1 ? 'analog' : 'digital',
  units: index === 1 ? 'deg_c' : null,
  readable: true,
  commandable: index !== 1,
  persistence: 'volatile',
  sourceId: null,
  revision: 1
});

test.beforeEach(async ({ page }) => {
  await page.route('**/api/points?**', async (route) => {
    const url = new URL(route.request().url());
    const filter = url.searchParams.get('filter')?.toLowerCase() ?? '';
    const pageNumber = Number(url.searchParams.get('page') ?? 1);
    const pageSize = Number(url.searchParams.get('pageSize') ?? 10);
    const all = Array.from({ length: 12 }, (_, index) => point(index + 1)).filter(({ name }) =>
      name.toLowerCase().includes(filter)
    );
    await route.fulfill({
      json: {
        items: all.slice((pageNumber - 1) * pageSize, pageNumber * pageSize),
        totalItems: all.length,
        page: pageNumber,
        pageSize,
        pageCount: Math.ceil(all.length / pageSize)
      }
    });
  });
});

/**
 * Purpose: Protects the behavioral contract that navigates, filters, pages and remains keyboard usable after reload.
 * Description: Exercises navigates, filters, pages and remains keyboard usable after reload from its arranged starting state and
 * verifies the observable results required by the scenario.
 */
test('navigates, filters, pages and remains keyboard usable after reload', async ({ page }) => {
  await page.goto('/flows');
  await page.getByRole('link', { name: 'Points', exact: true }).click();

  // Expected outcome: `page.getByRole('heading', { name: 'Points' })` is visible to the user.
  // Acceptance criteria: `page.getByRole('heading', { name: 'Points' })` must be visible, because this condition proves that
  // navigates, filters, pages and remains keyboard usable after reload.
  await expect(page.getByRole('heading', { name: 'Points' })).toBeVisible();

  // Expected outcome: `page.getByRole('region', { name: 'Configured points table' })` is visible to the user.
  // Acceptance criteria: `page.getByRole('region', { name: 'Configured points table' })` must be visible, because this condition proves that
  // navigates, filters, pages and remains keyboard usable after reload.
  await expect(page.getByRole('region', { name: 'Configured points table' })).toBeVisible();

  // Expected outcome: `(await new AxeBuilder({ page }` matches the required structure.
  // Acceptance criteria: `(await new AxeBuilder({ page }` must equal `[]`, because this condition proves that
  // navigates, filters, pages and remains keyboard usable after reload.
  expect((await new AxeBuilder({ page }).include('main').analyze()).violations).toEqual([]);

  // Expected outcome: `page.getByText('Group: room')` is visible to the user.
  // Acceptance criteria: `page.getByText('Group: room')` must be visible, because this condition proves that
  // navigates, filters, pages and remains keyboard usable after reload.
  await expect(page.getByText('Group: room')).toBeVisible();

  // Expected outcome: `page.getByText('Inherited from group')` is visible to the user.
  // Acceptance criteria: `page.getByText('Inherited from group')` must be visible, because this condition proves that
  // navigates, filters, pages and remains keyboard usable after reload.
  await expect(page.getByText('Inherited from group')).toBeVisible();

  await page.getByRole('button', { name: 'Next page' }).focus();
  await page.keyboard.press('Enter');

  // Expected outcome: `page.getByRole('rowheader', { name: /Point 11/ })` is visible to the user.
  // Acceptance criteria: `page.getByRole('rowheader', { name: /Point 11/ })` must be visible, because this condition proves that
  // navigates, filters, pages and remains keyboard usable after reload.
  await expect(page.getByRole('rowheader', { name: /Point 11/ })).toBeVisible();

  const filter = page.getByLabel('Filter points');
  await filter.fill('Point 03');
  await page.getByRole('button', { name: 'Apply filter' }).press('Enter');

  // Expected outcome: `page.getByRole('rowheader', { name: /Point 03/ })` is visible to the user.
  // Acceptance criteria: `page.getByRole('rowheader', { name: /Point 03/ })` must be visible, because this condition proves that
  // navigates, filters, pages and remains keyboard usable after reload.
  await expect(page.getByRole('rowheader', { name: /Point 03/ })).toBeVisible();

  // Expected outcome: `page.getByRole('rowheader', { name: /Point 01/ })` is not exposed to the user.
  // Acceptance criteria: `page.getByRole('rowheader', { name: /Point 01/ })` must be hidden, because this condition proves that
  // navigates, filters, pages and remains keyboard usable after reload.
  await expect(page.getByRole('rowheader', { name: /Point 01/ })).toBeHidden();

  await page.reload();

  // Expected outcome: `page.getByRole('heading', { name: 'Points' })` is visible to the user.
  // Acceptance criteria: `page.getByRole('heading', { name: 'Points' })` must be visible, because this condition proves that
  // navigates, filters, pages and remains keyboard usable after reload.
  await expect(page.getByRole('heading', { name: 'Points' })).toBeVisible();
  // Edge can restore focus to the control that was active before a reload. Clear
  // that browser-managed state so Tab starts at the document's first focusable item.
  await page.evaluate(() => (document.activeElement as HTMLElement | null)?.blur());
  await page.keyboard.press('Tab');

  // Expected outcome: `page.getByRole('link', { name: 'Skip to main content' })` owns keyboard focus.
  // Acceptance criteria: `page.getByRole('link', { name: 'Skip to main content' })` must be focused, because this condition proves that
  // navigates, filters, pages and remains keyboard usable after reload.
  await expect(page.getByRole('link', { name: 'Skip to main content' })).toBeFocused();
});

/**
 * Purpose: Protects users from an unexplained failure when the deployed backend does not support the points API.
 * Description: Makes the points endpoint return HTTP 404, opens the points catalogue, and verifies that
 * the UI explains the unsupported API and offers an explicit retry action.
 */
test('shows an actionable unavailable state when the points API is unsupported', async ({ page }) => {
  await page.unroute('**/api/points?**');
  await page.route('**/api/points?**', async (route) => {
    await route.fulfill({ status: 404, json: { message: 'not found' } });
  });
  await page.goto('/points');

  // Expected outcome: `page.getByRole('alert')` displays the required content.
  // Acceptance criteria: `page.getByRole('alert')` must contain the text `'does not support'`, because this condition proves that
  // the UI clearly explains that the deployed backend does not provide the points API.
  await expect(page.getByRole('alert')).toContainText('does not support');

  // Expected outcome: `page.getByRole('button', { name: 'Check again' })` is visible to the user.
  // Acceptance criteria: `page.getByRole('button', { name: 'Check again' })` must be visible, because this condition proves that
  // users can retry after the points API becomes available without reloading the application manually.
  await expect(page.getByRole('button', { name: 'Check again' })).toBeVisible();
});
