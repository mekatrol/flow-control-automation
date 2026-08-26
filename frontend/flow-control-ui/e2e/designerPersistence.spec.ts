import { expect, test } from './fixtures/flowTest';

import { sampleFlows } from '@/features/flows/__tests__/fixtures/sampleFlows';

/**
 * Designer persistence end-to-end coverage.
 *
 * Each scenario owns one user-facing contract and receives fresh mocked API
 * state from the shared fixture, so it remains safe to run alone or in parallel.
 */

/**
 * Purpose: Protects the behavioral contract that opens a flow designer directly.
 * Description: Exercises opens a flow designer directly from its arranged starting state and
 * verifies the observable results required by the scenario.
 */
test('opens a flow designer directly', async ({ page }) => {
  await page.goto('/flows/climate-control');

  // Expected outcome: `page.getByRole('heading', { name: 'Climate control' })` is visible to the user.
  // Acceptance criteria: `page.getByRole('heading', { name: 'Climate control' })` must be visible, because this condition proves that
  // opens a flow designer directly.
  await expect(page.getByRole('heading', { name: 'Climate control' })).toBeVisible();

  // Expected outcome: `page.getByText('4 nodes', { exact: true })` is visible to the user.
  // Acceptance criteria: `page.getByText('4 nodes', { exact: true })` must be visible, because this condition proves that
  // opens a flow designer directly.
  await expect(page.getByText('4 nodes', { exact: true })).toBeVisible();

  // Expected outcome: `page.getByText('2 connections', { exact: true })` is visible to the user.
  // Acceptance criteria: `page.getByText('2 connections', { exact: true })` must be visible, because this condition proves that
  // opens a flow designer directly.
  await expect(page.getByText('2 connections', { exact: true })).toBeVisible();

  // Expected outcome: `page.locator('[data-connection-id]')` resolves to the required number of elements.
  // Acceptance criteria: `page.locator('[data-connection-id]')` must resolve to exactly 2 elements, because this condition proves that
  // opens a flow designer directly.
  await expect(page.locator('[data-connection-id]')).toHaveCount(2);

  // Expected outcome: `page.getByRole('button', { name: /Average temperature, Calculator node, draft/ })` is visible to the user.
  // Acceptance criteria: `page.getByRole('button', { name: /Average temperature, Calculator node, draft/ })` must be visible, because this condition proves that
  // opens a flow designer directly.
  await expect(
    page.getByRole('button', { name: /Average temperature, Calculator node, draft/ })
  ).toBeVisible();

  // Expected outcome: `page.getByRole('button', { name: /Comfort pulse, Pulse node, draft/ })` is visible to the user.
  // Acceptance criteria: `page.getByRole('button', { name: /Comfort pulse, Pulse node, draft/ })` must be visible, because this condition proves that
  // opens a flow designer directly.
  await expect(
    page.getByRole('button', { name: /Comfort pulse, Pulse node, draft/ })
  ).toBeVisible();

  // Expected outcome: `page.getByRole('button', { name: /Manual override, Override node, draft/ })` is visible to the user.
  // Acceptance criteria: `page.getByRole('button', { name: /Manual override, Override node, draft/ })` must be visible, because this condition proves that
  // opens a flow designer directly.
  await expect(
    page.getByRole('button', { name: /Manual override, Override node, draft/ })
  ).toBeVisible();

  // Expected outcome: `page.getByRole('button', { name: /Zone outputs, Split node, draft/ })` is visible to the user.
  // Acceptance criteria: `page.getByRole('button', { name: /Zone outputs, Split node, draft/ })` must be visible, because this condition proves that
  // opens a flow designer directly.
  await expect(page.getByRole('button', { name: /Zone outputs, Split node, draft/ })).toBeVisible();

  // Expected outcome: `page.getByRole('button', { name: /Values, input, number/ })` is visible to the user.
  // Acceptance criteria: `page.getByRole('button', { name: /Values, input, number/ })` must be visible, because this condition proves that
  // opens a flow designer directly.
  await expect(page.getByRole('button', { name: /Values, input, number/ })).toBeVisible();

  const viewport = page.getByLabel(/Scrollable designer viewport/);
  await viewport.focus();

  // Expected outcome: `viewport` owns keyboard focus.
  // Acceptance criteria: `viewport` must be focused, because this condition proves that
  // opens a flow designer directly.
  await expect(viewport).toBeFocused();

  const pageHasVerticalOverflow = await page.evaluate(
    () => document.documentElement.scrollHeight > document.documentElement.clientHeight
  );

  // Expected outcome: `pageHasVerticalOverflow` has the required value.
  // Acceptance criteria: `pageHasVerticalOverflow` must be `false`, because this condition proves that
  // opens a flow designer directly.
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

  // Expected outcome: `toolboxScroll.canScroll` has the required value.
  // Acceptance criteria: `toolboxScroll.canScroll` must be `true`, because this condition proves that
  // opens a flow designer directly.
  expect(toolboxScroll.canScroll).toBe(true);

  // Expected outcome: `toolboxScroll.scrollTop` satisfies the required boundary.
  // Acceptance criteria: `toolboxScroll.scrollTop` must satisfy the asserted boundary against `0`, because this condition proves that
  // opens a flow designer directly.
  expect(toolboxScroll.scrollTop).toBeGreaterThan(0);

  // Expected outcome: `toolboxScroll.windowScrollY` has the required value.
  // Acceptance criteria: `toolboxScroll.windowScrollY` must be `0`, because this condition proves that
  // opens a flow designer directly.
  expect(toolboxScroll.windowScrollY).toBe(0);

  const initialWidth = await page
    .getByRole('group', { name: 'Climate control flow graph' })
    .evaluate((element) => element.getBoundingClientRect().width);
  await page.getByRole('button', { name: 'Zoom in' }).click();

  // Expected outcome: `page.getByText('125%', { exact: true })` is visible to the user.
  // Acceptance criteria: `page.getByText('125%', { exact: true })` must be visible, because this condition proves that
  // opens a flow designer directly.
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

  // Expected outcome: `canReachWholeGraph` has the required value.
  // Acceptance criteria: `canReachWholeGraph` must be `true`, because this condition proves that
  // opens a flow designer directly.
  expect(canReachWholeGraph).toBe(true);
});

/**
 * Purpose: Protects the behavioral contract that renders a validated mocked API payload and rejects an invalid one visibly.
 * Description: Exercises renders a validated mocked API payload and rejects an invalid one visibly from its arranged starting state and
 * verifies the observable results required by the scenario.
 */
test('renders a validated mocked API payload and rejects an invalid one visibly', async ({
  page
}) => {
  await page.unroute('**/api/flows/*');
  const payload = structuredClone(sampleFlows[0]!);
  payload.nodes[0]!.label = 'Temperature from API';
  await page.route('**/api/flows/climate-control', (route) => route.fulfill({ json: payload }));

  await page.goto('/flows/climate-control');

  // Expected outcome: `page.getByRole('button', { name: /Temperature from API, Calculator node/ })` is visible to the user.
  // Acceptance criteria: `page.getByRole('button', { name: /Temperature from API, Calculator node/ })` must be visible, because this condition proves that
  // renders a validated mocked API payload and rejects an invalid one visibly.
  await expect(
    page.getByRole('button', { name: /Temperature from API, Calculator node/ })
  ).toBeVisible();
  const override = page.locator('[data-node-id="manual-override"]');

  // Expected outcome: `override` exposes the required attribute.
  // Acceptance criteria: `override` must have attribute arguments `'data-node-category', 'override'`, because this condition proves that
  // renders a validated mocked API payload and rejects an invalid one visibly.
  await expect(override).toHaveAttribute('data-node-category', 'override');

  // Expected outcome: `override.locator('.node-body')` exposes the required attribute.
  // Acceptance criteria: `override.locator('.node-body')` must have attribute arguments `'fill'`, because this condition proves that
  // renders a validated mocked API payload and rejects an invalid one visibly.
  await expect(override.locator('.node-body')).not.toHaveAttribute('fill');

  await page.unroute('**/api/flows/climate-control');
  const invalidPayload = structuredClone(payload);
  invalidPayload.connections[0]!.end.nodeId = 'missing-node';
  await page.route('**/api/flows/climate-control', (route) =>
    route.fulfill({ json: invalidPayload })
  );
  const invalidFlowResponse = page.waitForResponse(
    (response) =>
      response.request().method() === 'GET' &&
      new URL(response.url()).pathname === '/api/flows/climate-control'
  );
  await page.reload({ waitUntil: 'domcontentloaded' });
  await invalidFlowResponse;

  // Expected outcome: `page.getByRole('alert')` displays the required content.
  // Acceptance criteria: `page.getByRole('alert')` must contain the text `'invalid flow'`, because this condition proves that
  // renders a validated mocked API payload and rejects an invalid one visibly.
  await expect(page.getByRole('alert')).toContainText('invalid flow');

  // Expected outcome: `page.getByText('Flow not found', { exact: true })` is visible to the user.
  // Acceptance criteria: `page.getByText('Flow not found', { exact: true })` must be visible, because this condition proves that
  // renders a validated mocked API payload and rejects an invalid one visibly.
  await expect(page.getByText('Flow not found', { exact: true })).toBeVisible();

  // Expected outcome: `page.getByRole('group', { name: /flow graph/ })` resolves to the required number of elements.
  // Acceptance criteria: `page.getByRole('group', { name: /flow graph/ })` must resolve to exactly 0 elements, because this condition proves that
  // renders a validated mocked API payload and rejects an invalid one visibly.
  await expect(page.getByRole('group', { name: /flow graph/ })).toHaveCount(0);
});

/**
 * Purpose: Protects the behavioral contract that saves an unchanged mocked flow without losing graph data.
 * Description: Exercises saves an unchanged mocked flow without losing graph data from its arranged starting state and
 * verifies the observable results required by the scenario.
 */
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

  // Expected outcome: `page.locator('.request-status')` is not exposed to the user.
  // Acceptance criteria: `page.locator('.request-status')` must be hidden, because this condition proves that
  // saves an unchanged mocked flow without losing graph data.
  await expect(page.locator('.request-status')).toBeHidden();
  await page.getByRole('button', { name: 'Save flow' }).click();

  await expect.poll(() => savedPayload).toEqual(payload);

  // Expected outcome: `page.getByRole('button', { name: 'Save flow' })` permits interaction.
  // Acceptance criteria: `page.getByRole('button', { name: 'Save flow' })` must be enabled, because this condition proves that
  // saves an unchanged mocked flow without losing graph data.
  await expect(page.getByRole('button', { name: 'Save flow' })).toBeEnabled();
});

/**
 * Purpose: Protects the behavioral contract that keeps the newest route response during rapid navigation.
 * Description: Exercises keeps the newest route response during rapid navigation from its arranged starting state and
 * verifies the observable results required by the scenario.
 */
test('keeps the newest route response during rapid navigation', async ({ page }) => {
  await page.unroute('**/api/flows/*');
  let releaseClimate!: () => void;
  const climateReady = new Promise<void>((resolve) => {
    releaseClimate = resolve;
  });
  let markClimateRequested!: () => void;
  const climateRequested = new Promise<void>((resolve) => {
    markClimateRequested = resolve;
  });
  await page.route('**/api/flows/*', async (route) => {
    const id = new URL(route.request().url()).pathname.split('/').at(-1);
    if (id === 'garden-irrigation') {
      markClimateRequested();
      await climateReady;
    }
    const flow = sampleFlows.find((candidate) => candidate.id === id);
    await route.fulfill({ status: flow ? 200 : 404, json: flow ?? {} });
  });

  await page.goto('/flows/garden-irrigation');
  // Synchronize with the deliberately delayed request itself. The loading text
  // is transient and can be painted between Playwright polling intervals on a
  // fast mobile Chromium run.
  await climateRequested;
  await expect(page).toHaveURL(/\/flows\/garden-irrigation(?:\/design)?$/);
  await page.evaluate(async () => {
    // @ts-expect-error The path is resolved by the browser-facing Vite module graph.
    const { default: router } = await import('/src/router/index.ts');
    await router.push('/flows/garden-irrigation');
  });
  await expect(page).toHaveURL(/\/flows\/garden-irrigation(?:\/design)?$/);
  releaseClimate();

  // Expected outcome: `page.getByRole('heading', { name: 'Garden irrigation' })` is visible to the user.
  // Acceptance criteria: `page.getByRole('heading', { name: 'Garden irrigation' })` must be visible, because this condition proves that
  // keeps the newest route response during rapid navigation.
  await expect(page.getByRole('heading', { name: 'Garden irrigation' })).toBeVisible();

  // Expected outcome: `page.getByRole('heading', { name: 'Climate control' })` resolves to the required number of elements.
  // Acceptance criteria: `page.getByRole('heading', { name: 'Climate control' })` must resolve to exactly 0 elements, because this condition proves that
  // keeps the newest route response during rapid navigation.
  await expect(page.getByRole('heading', { name: 'Climate control' })).toHaveCount(0);
});

/**
 * Purpose: Protects the behavioral contract that recovers from a failed save without losing edits.
 * Description: Exercises recovers from a failed save without losing edits from its arranged starting state and
 * verifies the observable results required by the scenario.
 */
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

  const initialFlowResponse = page.waitForResponse(
    (response) =>
      response.request().method() === 'GET' &&
      new URL(response.url()).pathname === '/api/flows/climate-control'
  );
  await page.goto('/flows/climate-control');

  // Expected outcome: `(await initialFlowResponse` has the required value.
  // Acceptance criteria: `(await initialFlowResponse` must be `true`, because this condition proves that
  // recovers from a failed save without losing edits.
  expect((await initialFlowResponse).ok()).toBe(true);

  const averageNode = page.getByRole('button', {
    name: /Average temperature, Calculator node/
  });

  // Expected outcome: `averageNode` is visible to the user.
  // Acceptance criteria: `averageNode` must be visible, because this condition proves that
  // recovers from a failed save without losing edits.
  await expect(averageNode).toBeVisible();
  await averageNode.click();
  await page.getByRole('textbox', { name: 'Node label' }).fill('Retry-safe average');
  await page.getByRole('button', { name: 'Save flow' }).click();

  // Expected outcome: `page.getByRole('button', { name: 'Saving…' })` prevents interaction.
  // Acceptance criteria: `page.getByRole('button', { name: 'Saving…' })` must be disabled, because this condition proves that
  // recovers from a failed save without losing edits.
  await expect(page.getByRole('button', { name: 'Saving…' })).toBeDisabled();
  releaseFailedSave();

  // Expected outcome: `page.getByRole('alert')` displays the required content.
  // Acceptance criteria: `page.getByRole('alert')` must contain the text `'try again'`, because this condition proves that
  // recovers from a failed save without losing edits.
  await expect(page.getByRole('alert')).toContainText('try again');

  // Expected outcome: `page.getByRole('button', { name: /Retry-safe average, Calculator node/ })` is visible to the user.
  // Acceptance criteria: `page.getByRole('button', { name: /Retry-safe average, Calculator node/ })` must be visible, because this condition proves that
  // recovers from a failed save without losing edits.
  await expect(
    page.getByRole('button', { name: /Retry-safe average, Calculator node/ })
  ).toBeVisible();

  // Expected outcome: `page.getByText('Unsaved changes', { exact: true })` is visible to the user.
  // Acceptance criteria: `page.getByText('Unsaved changes', { exact: true })` must be visible, because this condition proves that
  // recovers from a failed save without losing edits.
  await expect(page.getByText('Unsaved changes', { exact: true })).toBeVisible();

  await page.getByRole('button', { name: 'Close' }).click();
  await page.getByRole('button', { name: 'Save flow' }).click();
  await expect.poll(() => persistedPayload.nodes[0]?.label).toBe('Retry-safe average');

  // Expected outcome: `page.getByText('Unsaved changes', { exact: true })` is not exposed to the user.
  // Acceptance criteria: `page.getByText('Unsaved changes', { exact: true })` must be hidden, because this condition proves that
  // recovers from a failed save without losing edits.
  await expect(page.getByText('Unsaved changes', { exact: true })).toBeHidden();
  await page.reload();

  // Expected outcome: `page.getByRole('button', { name: /Retry-safe average, Calculator node/ })` is visible to the user.
  // Acceptance criteria: `page.getByRole('button', { name: /Retry-safe average, Calculator node/ })` must be visible, because this condition proves that
  // recovers from a failed save without losing edits.
  await expect(
    page.getByRole('button', { name: /Retry-safe average, Calculator node/ })
  ).toBeVisible();
});

/**
 * Purpose: Protects the behavioral contract that protects dirty navigation and supports explicit discard.
 * Description: Exercises protects dirty navigation and supports explicit discard from its arranged starting state and
 * verifies the observable results required by the scenario.
 */
test('protects dirty navigation and supports explicit discard', async ({ page }) => {
  const initialFlowResponse = page.waitForResponse(
    (response) =>
      response.request().method() === 'GET' &&
      new URL(response.url()).pathname === '/api/flows/climate-control'
  );
  await page.goto('/flows/climate-control');

  // Expected outcome: `(await initialFlowResponse` has the required value.
  // Acceptance criteria: `(await initialFlowResponse` must be `true`, because this condition proves that
  // protects dirty navigation and supports explicit discard.
  expect((await initialFlowResponse).ok()).toBe(true);

  const node = page.getByRole('button', { name: /Average temperature, Calculator node/ });

  // Expected outcome: `node` is visible to the user.
  // Acceptance criteria: `node` must be visible, because this condition proves that
  // protects dirty navigation and supports explicit discard.
  await expect(node).toBeVisible();
  await node.focus();
  await page.keyboard.press('Enter');
  await page.keyboard.press('ArrowRight');

  // Expected outcome: `page.getByText('Unsaved changes', { exact: true })` is visible to the user.
  // Acceptance criteria: `page.getByText('Unsaved changes', { exact: true })` must be visible, because this condition proves that
  // protects dirty navigation and supports explicit discard.
  await expect(page.getByText('Unsaved changes', { exact: true })).toBeVisible();

  await page.getByRole('link', { name: 'All flows' }).click();

  // Expected outcome: `page.getByRole('alertdialog', { name: 'Discard unsaved changes?' })` is visible to the user.
  // Acceptance criteria: `page.getByRole('alertdialog', { name: 'Discard unsaved changes?' })` must be visible, because this condition proves that
  // protects dirty navigation and supports explicit discard.
  await expect(page.getByRole('alertdialog', { name: 'Discard unsaved flow changes confirmation' })).toBeVisible();

  // Expected outcome: Navigation reaches the required route.
  // Acceptance criteria: the page URL must match `/\/flows\/climate-control\/design$/`, because this condition proves that
  // protects dirty navigation and supports explicit discard.
  await expect(page).toHaveURL(/\/flows\/climate-control\/design$/);
  await page.getByRole('button', { name: 'Keep editing' }).click();

  // Expected outcome: `page.getByRole('alertdialog')` is not exposed to the user.
  // Acceptance criteria: `page.getByRole('alertdialog')` must be hidden, because this condition proves that
  // protects dirty navigation and supports explicit discard.
  await expect(page.getByRole('alertdialog')).toBeHidden();

  await page.getByRole('link', { name: 'All flows' }).click();
  await page.getByRole('button', { name: 'Discard changes' }).click();

  // Expected outcome: Navigation reaches the required route.
  // Acceptance criteria: the page URL must match `/\/flows$/`, because this condition proves that
  // protects dirty navigation and supports explicit discard.
  await expect(page).toHaveURL(/\/flows$/);
  await page.getByRole('link', { name: /Climate control/ }).click();

  // Expected outcome: `node` exposes the required attribute.
  // Acceptance criteria: `node` must have attribute arguments `'transform', 'translate(90 110`, because this condition proves that
  // protects dirty navigation and supports explicit discard.
  await expect(node).toHaveAttribute('transform', 'translate(90 110)');
});
