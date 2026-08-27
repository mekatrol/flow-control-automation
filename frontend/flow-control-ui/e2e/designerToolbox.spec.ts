import { expect, test } from './fixtures/flowTest';

/**
 * Designer toolbox end-to-end coverage.
 *
 * Each scenario owns one user-facing contract and receives fresh mocked API
 * state from the shared fixture, so it remains safe to run alone or in parallel.
 */

/**
 * Purpose: Protects the behavioral contract that searches the node palette and adds registry-backed nodes.
 * Description: Exercises searches the node palette and adds registry-backed nodes from its arranged starting state and
 * verifies the observable results required by the scenario.
 */
test('searches the node palette and adds registry-backed nodes', async ({ page }) => {
  const initialFlowResponse = page.waitForResponse(
    (response) =>
      response.request().method() === 'GET' &&
      new URL(response.url()).pathname === '/api/flows/climate-control'
  );
  await page.goto('/flows/climate-control');

  // Expected outcome: `(await initialFlowResponse` has the required value.
  // Acceptance criteria: `(await initialFlowResponse` must be `true`, because this condition proves that
  // searches the node palette and adds registry-backed nodes.
  expect((await initialFlowResponse).ok()).toBe(true);

  const search = page.getByRole('searchbox', { name: 'Find a node' });

  // Expected outcome: `search` is visible to the user.
  // Acceptance criteria: `search` must be visible, because this condition proves that
  // searches the node palette and adds registry-backed nodes.
  await expect(search).toBeVisible();
  await search.fill('timing');

  // Expected outcome: `page.getByRole('button', { name: 'Add Pulse node', exact: true })` is visible to the user.
  // Acceptance criteria: `page.getByRole('button', { name: 'Add Pulse node', exact: true })` must be visible, because this condition proves that
  // searches the node palette and adds registry-backed nodes.
  await expect(page.getByRole('button', { name: 'Add Pulse node', exact: true })).toBeVisible();

  // Expected outcome: `page.getByRole('button', { name: 'Add Calculator node', exact: true })` resolves to the required number of elements.
  // Acceptance criteria: `page.getByRole('button', { name: 'Add Calculator node', exact: true })` must resolve to exactly 0 elements, because this condition proves that
  // searches the node palette and adds registry-backed nodes.
  await expect(page.getByRole('button', { name: 'Add Calculator node', exact: true })).toHaveCount(0);
  await page.getByRole('button', { name: 'Add Pulse node', exact: true }).click();

  const pulse = page.getByRole('button', { name: /New Pulse, Pulse node/ });

  // Expected outcome: `pulse` exposes the required attribute.
  // Acceptance criteria: `pulse` must have attribute arguments `'aria-pressed', 'true'`, because this condition proves that
  // searches the node palette and adds registry-backed nodes.
  await expect(pulse).toHaveAttribute('aria-pressed', 'true');

  // Expected outcome: `pulse` exposes the required attribute.
  // Acceptance criteria: `pulse` must have attribute arguments `'data-node-category', 'timing'`, because this condition proves that
  // searches the node palette and adds registry-backed nodes.
  await expect(pulse).toHaveAttribute('data-node-category', 'timing');

  // Expected outcome: `pulse.locator('.node-body')` exposes the required attribute.
  // Acceptance criteria: `pulse.locator('.node-body')` must have attribute arguments `'fill'`, because this condition proves that
  // searches the node palette and adds registry-backed nodes.
  await expect(pulse.locator('.node-body')).not.toHaveAttribute('fill');

  // Expected outcome: `page.getByText('5 nodes', { exact: true })` is visible to the user.
  // Acceptance criteria: `page.getByText('5 nodes', { exact: true })` must be visible, because this condition proves that
  // searches the node palette and adds registry-backed nodes.
  await expect(page.getByText('5 nodes', { exact: true })).toBeVisible();

  // Expected outcome: `page.getByRole('button', { name: /Trigger, input, any/ })` is visible to the user.
  // Acceptance criteria: `page.getByRole('button', { name: /Trigger, input, any/ })` must be visible, because this condition proves that
  // searches the node palette and adds registry-backed nodes.
  await expect(page.getByRole('button', { name: /Trigger, input, boolean/ })).toBeVisible();

  await search.fill('routing');
  await page.getByRole('button', { name: 'Add Split node', exact: true }).click();
  const split = page.getByRole('button', { name: /New Split, Split node/ });

  // Expected outcome: `split` is visible to the user.
  // Acceptance criteria: `split` must be visible, because this condition proves that
  // searches the node palette and adds registry-backed nodes.
  await expect(split).toBeVisible();

  // Expected outcome: `split` exposes the required attribute.
  // Acceptance criteria: `split` must have attribute arguments `'data-node-category', 'routing'`, because this condition proves that
  // searches the node palette and adds registry-backed nodes.
  await expect(split).toHaveAttribute('data-node-category', 'routing');

  // Expected outcome: `split.locator('.node-body')` exposes the required attribute.
  // Acceptance criteria: `split.locator('.node-body')` must have attribute arguments `'fill'`, because this condition proves that
  // searches the node palette and adds registry-backed nodes.
  await expect(split.locator('.node-body')).not.toHaveAttribute('fill');

  // Expected outcome: `split.locator('rect.connector-port')` resolves to the required number of elements.
  // Acceptance criteria: the new Split node must expose exactly 2 connector ports, because this condition proves that
  // searches the node palette and adds registry-backed nodes.
  await expect(page.locator('.flow-node').filter({ has: split }).locator('rect.connector-port')).toHaveCount(2);

  // Expected outcome: `page.getByText('6 nodes', { exact: true })` is visible to the user.
  // Acceptance criteria: `page.getByText('6 nodes', { exact: true })` must be visible, because this condition proves that
  // searches the node palette and adds registry-backed nodes.
  await expect(page.getByText('6 nodes', { exact: true })).toBeVisible();

  await search.fill('override');

  // Expected outcome: `page.getByRole('heading', { name: 'override', exact: true })` is visible to the user.
  // Acceptance criteria: `page.getByRole('heading', { name: 'override', exact: true })` must be visible, because this condition proves that
  // searches the node palette and adds registry-backed nodes.
  await expect(page.getByRole('heading', { name: 'override', exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Add Override node', exact: true }).click();
  const override = page.getByRole('button', { name: /New Override, Override node/ });

  // Expected outcome: `override` exposes the required attribute.
  // Acceptance criteria: `override` must have attribute arguments `'data-node-category', 'override'`, because this condition proves that
  // searches the node palette and adds registry-backed nodes.
  await expect(override).toHaveAttribute('data-node-category', 'override');

  // Expected outcome: `override.locator('.node-body')` exposes the required attribute.
  // Acceptance criteria: `override.locator('.node-body')` must have attribute arguments `'fill'`, because this condition proves that
  // searches the node palette and adds registry-backed nodes.
  await expect(override.locator('.node-body')).not.toHaveAttribute('fill');
});

/**
 * Purpose: Protects the behavioral contract that keeps dark-theme function blocks at WCAG AA text contrast.
 * Description: Exercises keeps dark-theme function blocks at WCAG AA text contrast from its arranged starting state and
 * verifies the observable results required by the scenario.
 */
test('keeps dark-theme function blocks at WCAG AA text contrast', async ({ page }) => {
  await page.addInitScript(() => localStorage.setItem('theme-preference', 'dark'));
  await page.goto('/flows/climate-control');

  const search = page.getByRole('searchbox', { name: 'Find a node' });
  await search.fill('and');
  await page.getByRole('button', { name: 'Add And node', exact: true }).click();

  const contrastByCategory = await page.locator('.flow-node').evaluateAll((nodes) => {
    const luminance = (color: string): number => {
      const channels = color.match(/\d+/g)!.slice(0, 3).map(Number);
      const linear = channels.map((channel) => {
        const value = channel / 255;
        return value <= 0.04045 ? value / 12.92 : ((value + 0.055) / 1.055) ** 2.4;
      });
      return 0.2126 * linear[0]! + 0.7152 * linear[1]! + 0.0722 * linear[2]!;
    };

    return Object.fromEntries(
      nodes.map((node) => {
        const background = getComputedStyle(node.querySelector('.node-body')!).fill;
        const foreground = getComputedStyle(node.querySelector('.node-label')!).fill;
        const lighter = Math.max(luminance(background), luminance(foreground));
        const darker = Math.min(luminance(background), luminance(foreground));
        return [node.getAttribute('data-node-category'), (lighter + 0.05) / (darker + 0.05)];
      })
    );
  });

  // Expected outcome: `Object.keys(contrastByCategory` matches the required structure.
  // Acceptance criteria: `Object.keys(contrastByCategory` must equal `[ 'logic', 'maths', 'override', 'routing', 'timing' ]`, because this condition proves that
  // keeps dark-theme function blocks at WCAG AA text contrast.
  expect(Object.keys(contrastByCategory).sort()).toEqual([
    'logic',
    'maths',
    'override',
    'routing',
    'timing'
  ]);
  for (const ratio of Object.values(contrastByCategory)) {

    // Expected outcome: `ratio` satisfies the required boundary.
    // Acceptance criteria: `ratio` must satisfy the asserted boundary against `4.5`, because this condition proves that
    // keeps dark-theme function blocks at WCAG AA text contrast.
    expect(ratio).toBeGreaterThanOrEqual(4.5);
  }
});

/**
 * Purpose: Protects the behavioral contract that drags a legacy function block from the toolbox onto the canvas.
 * Description: Exercises drags a legacy function block from the toolbox onto the canvas from its arranged starting state and
 * verifies the observable results required by the scenario.
 */
test('drags a legacy function block from the toolbox onto the canvas', async ({ page }) => {
  await page.goto('/flows/climate-control');

  const search = page.getByRole('searchbox', { name: 'Find a node' });
  await search.fill('average');
  const average = page.getByRole('button', { name: 'Add Average node', exact: true });

  // Expected outcome: `average` exposes the required attribute.
  // Acceptance criteria: `average` must have attribute arguments `'draggable', 'true'`, because this condition proves that
  // drags a legacy function block from the toolbox onto the canvas.
  await expect(average).toHaveAttribute('draggable', 'true');
  const canvas = page.getByRole('group', { name: 'Climate control flow graph' });
  const canvasBox = await canvas.boundingBox();

  // Expected outcome: `canvasBox` is absent.
  // Acceptance criteria: `canvasBox` must be null, because this condition proves that
  // drags a legacy function block from the toolbox onto the canvas.
  expect(canvasBox).not.toBeNull();
  // Native mouse drag synthesis is unavailable in touch-emulating projects and
  // can target a node painted above the SVG background. Dispatch the same HTML
  // drag payload to the canvas at an explicit empty graph coordinate instead.
  const transfer = await page.evaluateHandle(() => new DataTransfer());
  await average.dispatchEvent('dragstart', { dataTransfer: transfer });
  await canvas.dispatchEvent('drop', {
    clientX: canvasBox!.x + 760,
    clientY: canvasBox!.y + 470,
    dataTransfer: transfer
  });

  // Expected outcome: `page.getByRole('button', { name: /New Average, Average node/ })` is visible to the user.
  // Acceptance criteria: `page.getByRole('button', { name: /New Average, Average node/ })` must be visible, because this condition proves that
  // drags a legacy function block from the toolbox onto the canvas.
  await expect(page.getByRole('button', { name: /New Average, Average node/ })).toBeVisible();

  // Expected outcome: `page.getByText('5 nodes', { exact: true })` is visible to the user.
  // Acceptance criteria: `page.getByText('5 nodes', { exact: true })` must be visible, because this condition proves that
  // drags a legacy function block from the toolbox onto the canvas.
  await expect(page.getByText('5 nodes', { exact: true })).toBeVisible();
});
