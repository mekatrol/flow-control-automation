import { expect, test } from './fixtures/flowTest';

import { sampleFlows } from '@/features/flows/__tests__/fixtures/sampleFlows';

/**
 * Designer persistence end-to-end coverage.
 *
 * Each scenario owns one user-facing contract and receives fresh mocked API
 * state from the shared fixture, so it remains safe to run alone or in parallel.
 */

test('opens a flow designer directly', async ({ page }) => {
  await page.goto('/flows/climate-control');

  await expect(page.getByRole('heading', { name: 'Climate control' })).toBeVisible();
  await expect(page.getByText('4 nodes', { exact: true })).toBeVisible();
  await expect(page.getByText('2 connections', { exact: true })).toBeVisible();
  await expect(page.locator('[data-connection-id]')).toHaveCount(2);
  await expect(
    page.getByRole('button', { name: /Average temperature, Calculator node, draft/ })
  ).toBeVisible();
  await expect(
    page.getByRole('button', { name: /Comfort pulse, Pulse node, draft/ })
  ).toBeVisible();
  await expect(
    page.getByRole('button', { name: /Manual override, Override node, draft/ })
  ).toBeVisible();
  await expect(page.getByRole('button', { name: /Zone outputs, Split node, draft/ })).toBeVisible();
  await expect(page.getByRole('button', { name: /Values, input, number/ })).toBeVisible();

  const viewport = page.getByLabel(/Scrollable designer viewport/);
  await viewport.focus();
  await expect(viewport).toBeFocused();

  const pageHasVerticalOverflow = await page.evaluate(
    () => document.documentElement.scrollHeight > document.documentElement.clientHeight
  );
  expect(pageHasVerticalOverflow).toBe(false);

  const toolbox = page.getByRole('complementary', { name: 'Function block toolbox' });
  const toolboxScroll = await toolbox.evaluate((element) => {
    element.scrollTop = element.scrollHeight;
    return {
      canScroll: element.scrollHeight > element.clientHeight,
      scrollTop: element.scrollTop,
      windowScrollY: window.scrollY
    };
  });
  expect(toolboxScroll.canScroll).toBe(true);
  expect(toolboxScroll.scrollTop).toBeGreaterThan(0);
  expect(toolboxScroll.windowScrollY).toBe(0);

  const initialWidth = await page
    .getByRole('group', { name: 'Climate control flow graph' })
    .evaluate((element) => element.getBoundingClientRect().width);
  await page.getByRole('button', { name: 'Zoom in' }).click();
  await expect(page.getByText('125%', { exact: true })).toBeVisible();
  await expect
    .poll(() =>
      page
        .getByRole('group', { name: 'Climate control flow graph' })
        .evaluate((element) => element.getBoundingClientRect().width)
    )
    .toBeGreaterThan(initialWidth);

  const canReachWholeGraph = await viewport.evaluate(
    (element) =>
      element.scrollWidth >= element.clientWidth && element.scrollHeight >= element.clientHeight
  );
  expect(canReachWholeGraph).toBe(true);
});

test('renders a validated mocked API payload and rejects an invalid one visibly', async ({
  page
}) => {
  await page.unroute('**/api/flows/*');
  const payload = structuredClone(sampleFlows[0]!);
  payload.nodes[0]!.label = 'Temperature from API';
  await page.route('**/api/flows/climate-control', (route) => route.fulfill({ json: payload }));

  await page.goto('/flows/climate-control');
  await expect(
    page.getByRole('button', { name: /Temperature from API, Calculator node/ })
  ).toBeVisible();
  const override = page.locator('[data-node-id="manual-override"]');
  await expect(override).toHaveAttribute('data-node-category', 'override');
  await expect(override.locator('.node-body')).not.toHaveAttribute('fill');

  await page.unroute('**/api/flows/climate-control');
  const invalidPayload = structuredClone(payload);
  invalidPayload.connections[0]!.end.nodeId = 'missing-node';
  await page.route('**/api/flows/climate-control', (route) =>
    route.fulfill({ json: invalidPayload })
  );
  await page.reload();

  await expect(page.getByRole('alert')).toContainText('invalid flow');
  await expect(page.getByText('Flow not found', { exact: true })).toBeVisible();
  await expect(page.getByRole('group', { name: /flow graph/ })).toHaveCount(0);
});

test('saves an unchanged mocked flow without losing graph data', async ({ page }) => {
  await page.unroute('**/api/flows/*');
  const payload = structuredClone(sampleFlows[0]!);
  let savedPayload: unknown;
  await page.route('**/api/flows/climate-control', async (route) => {
    if (route.request().method() === 'PUT') {
      savedPayload = route.request().postDataJSON();
      await route.fulfill({ json: savedPayload });
      return;
    }
    await route.fulfill({ json: payload });
  });

  await page.goto('/flows/climate-control');
  await expect(page.locator('.request-status')).toBeHidden();
  await page.getByRole('button', { name: 'Save flow' }).click();

  await expect.poll(() => savedPayload).toEqual(payload);
  await expect(page.getByRole('button', { name: 'Save flow' })).toBeEnabled();
});

test('keeps the newest route response during rapid navigation', async ({ page }) => {
  await page.unroute('**/api/flows/*');
  let releaseClimate!: () => void;
  const climateReady = new Promise<void>((resolve) => {
    releaseClimate = resolve;
  });
  await page.route('**/api/flows/*', async (route) => {
    const id = new URL(route.request().url()).pathname.split('/').at(-1);
    if (id === 'climate-control') await climateReady;
    const flow = sampleFlows.find((candidate) => candidate.id === id);
    await route.fulfill({ status: flow ? 200 : 404, json: flow ?? {} });
  });

  await page.goto('/flows/climate-control');
  await expect(page.getByText('Loading latest flow…')).toBeVisible();
  await page.getByRole('link', { name: 'Flows', exact: true }).click();
  await page.getByRole('link', { name: /Garden irrigation/ }).click();
  await expect(page.getByRole('heading', { name: 'Garden irrigation' })).toBeVisible();
  releaseClimate();
  await expect(page.getByRole('heading', { name: 'Garden irrigation' })).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Climate control' })).toHaveCount(0);
});

test('recovers from a failed save without losing edits', async ({ page }) => {
  await page.unroute('**/api/flows/*');
  let persistedPayload = structuredClone(sampleFlows[0]!);
  let failNextSave = true;
  let releaseFailedSave!: () => void;
  const failedSaveReady = new Promise<void>((resolve) => {
    releaseFailedSave = resolve;
  });
  await page.route('**/api/flows/climate-control', async (route) => {
    if (route.request().method() === 'PUT') {
      if (failNextSave) {
        failNextSave = false;
        await failedSaveReady;
        await route.fulfill({ status: 503, json: { message: 'try again' } });
        return;
      }
      persistedPayload = route.request().postDataJSON();
    }
    await route.fulfill({ json: persistedPayload });
  });

  await page.goto('/flows/climate-control');
  await page.getByRole('button', { name: /Average temperature, Calculator node/ }).click();
  await page.getByRole('textbox', { name: 'Node label' }).fill('Retry-safe average');
  await page.getByRole('button', { name: 'Save flow' }).click();

  await expect(page.getByRole('button', { name: 'Saving…' })).toBeDisabled();
  releaseFailedSave();
  await expect(page.getByRole('alert')).toContainText('try again');
  await expect(
    page.getByRole('button', { name: /Retry-safe average, Calculator node/ })
  ).toBeVisible();
  await expect(page.getByText('Unsaved changes', { exact: true })).toBeVisible();

  await page.getByRole('button', { name: 'Save flow' }).click();
  await expect.poll(() => persistedPayload.nodes[0]?.label).toBe('Retry-safe average');
  await expect(page.getByText('Unsaved changes', { exact: true })).toBeHidden();
  await page.reload();
  await expect(
    page.getByRole('button', { name: /Retry-safe average, Calculator node/ })
  ).toBeVisible();
});

test('protects dirty navigation and supports explicit discard', async ({ page }) => {
  await page.goto('/flows/climate-control');
  await expect(page.getByText('Loading latest flow…')).toBeHidden();

  const node = page.getByRole('button', { name: /Average temperature, Calculator node/ });
  await node.focus();
  await page.keyboard.press('Enter');
  await page.keyboard.press('ArrowRight');
  await expect(page.getByText('Unsaved changes', { exact: true })).toBeVisible();

  await page.getByRole('link', { name: 'All flows' }).click();
  await expect(page.getByRole('alertdialog', { name: 'Discard unsaved changes?' })).toBeVisible();
  await expect(page).toHaveURL(/\/flows\/climate-control$/);
  await page.getByRole('button', { name: 'Keep editing' }).click();
  await expect(page.getByRole('alertdialog')).toBeHidden();

  await page.getByRole('link', { name: 'All flows' }).click();
  await page.getByRole('button', { name: 'Discard changes' }).click();
  await expect(page).toHaveURL(/\/flows$/);
  await page.getByRole('link', { name: /Climate control/ }).click();
  await expect(node).toHaveAttribute('transform', 'translate(90 110)');
});
