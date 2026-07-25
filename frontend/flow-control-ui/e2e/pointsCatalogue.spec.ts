import { expect, test } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';

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

test('navigates, filters, pages and remains keyboard usable after reload', async ({ page }) => {
  await page.goto('/flows');
  await page.getByRole('link', { name: 'Points', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Points' })).toBeVisible();
  await expect(page.getByRole('region', { name: 'Configured points table' })).toBeVisible();
  expect((await new AxeBuilder({ page }).include('main').analyze()).violations).toEqual([]);
  await expect(page.getByText('Group: room')).toBeVisible();
  await expect(page.getByText('Inherited from group')).toBeVisible();

  await page.getByRole('button', { name: 'Next page' }).focus();
  await page.keyboard.press('Enter');
  await expect(page.getByRole('rowheader', { name: /Point 11/ })).toBeVisible();

  const filter = page.getByLabel('Filter points');
  await filter.fill('Point 03');
  await page.getByRole('button', { name: 'Apply filter' }).press('Enter');
  await expect(page.getByRole('rowheader', { name: /Point 03/ })).toBeVisible();
  await expect(page.getByRole('rowheader', { name: /Point 01/ })).toBeHidden();

  await page.reload();
  await expect(page.getByRole('heading', { name: 'Points' })).toBeVisible();
  await page.keyboard.press('Tab');
  await expect(page.getByRole('link', { name: 'Skip to main content' })).toBeFocused();
});

test('shows an actionable unavailable state for an older backend', async ({ page }) => {
  await page.unroute('**/api/points?**');
  await page.route('**/api/points?**', async (route) => {
    await route.fulfill({ status: 404, json: { message: 'not found' } });
  });
  await page.goto('/points');
  await expect(page.getByRole('alert')).toContainText('does not support');
  await expect(page.getByRole('button', { name: 'Check again' })).toBeVisible();
});
