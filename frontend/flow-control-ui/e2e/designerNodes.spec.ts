import { expect, test } from './fixtures/flowTest';

import { sampleFlows } from '@/features/flows/__tests__/fixtures/sampleFlows';

/**
 * Designer nodes end-to-end coverage.
 *
 * Each scenario owns one user-facing contract and receives fresh mocked API
 * state from the shared fixture, so it remains safe to run alone or in parallel.
 */

test('selects and clears a node with pointer and keyboard controls', async ({ page }) => {
  await page.goto('/flows/climate-control');

  const node = page.getByRole('button', { name: /Average temperature, Calculator node/ });
  await node.click();
  await expect(page.getByRole('complementary', { name: 'Node configuration' })).toBeVisible();

  await page.locator('[data-canvas-background]').click({ position: { x: 20, y: 20 } });
  await expect(page.getByRole('complementary', { name: 'Node configuration' })).toBeHidden();

  await node.focus();
  await page.keyboard.press('Enter');
  await expect(page.getByRole('complementary', { name: 'Node configuration' })).toBeVisible();
  await page.keyboard.press('Escape');
  await expect(page.getByRole('complementary', { name: 'Node configuration' })).toBeHidden();
});

test('drags a node to a snapped position and keeps it after route navigation', async ({ page }) => {
  await page.unroute('**/api/flows/*');
  let persistedPayload = structuredClone(sampleFlows[0]!);
  await page.route('**/api/flows/climate-control', async (route) => {
    if (route.request().method() === 'PUT') {
      persistedPayload = route.request().postDataJSON();
    }
    await route.fulfill({ json: persistedPayload });
  });
  await page.goto('/flows/climate-control');

  const node = page.getByRole('button', { name: /Average temperature, Calculator node/ });
  const initialTransform = await node.getAttribute('transform');
  const box = await node.boundingBox();
  expect(box).not.toBeNull();

  const canvas = page.getByRole('group', { name: 'Climate control flow graph' });
  // Pointer events exercise the component's actual input contract and work in
  // both mouse and touch-emulating projects; Playwright's mouse is intentionally
  // suppressed by mobile browser contexts.
  await node.dispatchEvent('pointerdown', {
    button: 0,
    clientX: box!.x + 80,
    clientY: box!.y + 30,
    pointerId: 7
  });
  await canvas.dispatchEvent('pointermove', {
    clientX: box!.x + 170,
    clientY: box!.y + 110,
    pointerId: 7
  });
  await canvas.dispatchEvent('pointerup', { pointerId: 7 });

  await expect(node).not.toHaveAttribute('transform', initialTransform!);
  const movedTransform = await node.evaluate((element) => element.getAttribute('transform'));
  const coordinates = movedTransform?.match(/translate\((\d+) (\d+)\)/);
  expect(Number(coordinates?.[1]) % 24).toBe(0);
  expect(Number(coordinates?.[2]) % 24).toBe(0);

  await page.getByRole('button', { name: 'Save flow' }).click();
  await expect.poll(() => persistedPayload.nodes[0]?.x).toBe(Number(coordinates?.[1]));
  await expect(page.getByRole('button', { name: 'Save flow' })).toBeEnabled();

  await page.getByRole('link', { name: 'All flows' }).click();
  await page.getByRole('link', { name: /Climate control/ }).click();
  await expect(node).toHaveAttribute('transform', movedTransform!);
});

test('enables z-order commands at valid boundaries and changes render order', async ({ page }) => {
  await page.goto('/flows/climate-control');

  const node = page.getByRole('button', { name: /Average temperature, Calculator node/ });
  const order = (): Promise<(string | null)[]> =>
    page
      .locator('[data-node-id]')
      .evaluateAll((nodes) => nodes.map((item) => item.getAttribute('data-node-id')));
  await node.click();

  await expect(page.getByRole('button', { name: 'Send to back' })).toBeDisabled();
  await expect(page.getByRole('button', { name: 'Bring to front' })).toBeEnabled();

  await page.getByRole('button', { name: 'Bring to front' }).click();
  expect(await order()).toEqual([
    'comfort-pulse',
    'manual-override',
    'zone-split',
    'temperature-average'
  ]);
  await expect(page.getByRole('button', { name: 'Bring to front' })).toBeDisabled();

  await page.getByRole('button', { name: 'Send backward' }).click();
  expect(await order()).toEqual([
    'comfort-pulse',
    'manual-override',
    'temperature-average',
    'zone-split'
  ]);

  await page.getByRole('button', { name: 'Send to back' }).click();
  expect(await order()).toEqual([
    'temperature-average',
    'comfort-pulse',
    'manual-override',
    'zone-split'
  ]);

  await page.getByRole('button', { name: 'Bring forward' }).click();
  expect(await order()).toEqual([
    'comfort-pulse',
    'temperature-average',
    'manual-override',
    'zone-split'
  ]);
});

test('moves and deletes with the keyboard while safeguarding editable controls', async ({
  page
}) => {
  await page.goto('/flows/climate-control');

  const node = page.getByRole('button', { name: /Average temperature, Calculator node/ });
  await node.focus();
  await page.keyboard.press('Enter');

  const gridToggle = page.getByLabel('Snap to grid');
  await gridToggle.focus();
  await page.keyboard.press('Delete');
  await expect(node).toBeVisible();

  await node.focus();
  await page.keyboard.press('ArrowRight');
  await expect(node).toHaveAttribute('transform', 'translate(114 110)');

  await page.keyboard.press('Delete');
  await expect(node).toBeHidden();
  await expect(page.locator('[data-connection-id]')).toHaveCount(1);
  await expect(page.getByLabel(/Scrollable designer viewport/)).toBeFocused();
});

test('validates, saves, and reloads typed node configuration', async ({ page }) => {
  await page.unroute('**/api/flows/*');
  let persistedPayload = structuredClone(sampleFlows[0]!);
  await page.route('**/api/flows/climate-control', async (route) => {
    if (route.request().method() === 'PUT') persistedPayload = route.request().postDataJSON();
    await route.fulfill({ json: persistedPayload });
  });
  await page.goto('/flows/climate-control');
  await expect(page.getByText('Loading latest flow…')).toBeHidden();

  await page.getByRole('button', { name: /Average temperature, Calculator node/ }).click();
  const label = page.getByRole('textbox', { name: 'Node label' });
  await label.fill('   ');
  await expect(page.getByRole('alert')).toHaveText('Node label is required.');
  await label.fill('Whole house average');
  await page.getByRole('combobox', { name: 'Operation' }).selectOption('sum');
  await expect(page.getByText('Unsaved changes', { exact: true })).toBeVisible();

  await page.getByRole('button', { name: 'Save flow' }).click();
  await expect.poll(() => persistedPayload.nodes[0]?.label).toBe('Whole house average');
  expect(persistedPayload.nodes[0]?.configuration.operation).toBe('sum');
  await expect(page.getByText('Unsaved changes', { exact: true })).toBeHidden();

  await page.reload();
  const savedNode = page.getByRole('button', { name: /Whole house average, Calculator node/ });
  await expect(savedNode).toBeVisible();
  await savedNode.click();
  await expect(page.getByRole('combobox', { name: 'Operation' })).toHaveValue('sum');
});
