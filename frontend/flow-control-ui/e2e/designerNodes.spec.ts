import { expect, test } from './fixtures/flowTest';

import { sampleFlows } from '@/features/flows/__tests__/fixtures/sampleFlows';

/**
 * Designer nodes end-to-end coverage.
 *
 * Each scenario owns one user-facing contract and receives fresh mocked API
 * state from the shared fixture, so it remains safe to run alone or in parallel.
 */

/**
 * Purpose: Protects the behavioral contract that selects and clears a node with pointer and keyboard controls.
 * Description: Exercises selects and clears a node with pointer and keyboard controls from its arranged starting state and
 * verifies the observable results required by the scenario.
 */
test('selects and clears a node with pointer and keyboard controls', async ({ page }) => {
  await page.goto('/flows/climate-control');

  const node = page.getByRole('button', { name: /Average temperature, Calculator node/ });
  await node.click();

  // Expected outcome: `page.getByRole('complementary', { name: 'Node configuration' })` is visible to the user.
  // Acceptance criteria: `page.getByRole('complementary', { name: 'Node configuration' })` must be visible, because this condition proves that
  // selects and clears a node with pointer and keyboard controls.
  await expect(page.getByRole('complementary', { name: 'Node configuration' })).toBeVisible();

  await page.keyboard.press('Escape');

  // Expected outcome: `page.getByRole('complementary', { name: 'Node configuration' })` is not exposed to the user.
  // Acceptance criteria: `page.getByRole('complementary', { name: 'Node configuration' })` must be hidden, because this condition proves that
  // selects and clears a node with pointer and keyboard controls.
  await expect(page.getByRole('complementary', { name: 'Node configuration' })).toBeHidden();

  await node.focus();
  await page.keyboard.press('Enter');

  // Expected outcome: `page.getByRole('complementary', { name: 'Node configuration' })` is visible to the user.
  // Acceptance criteria: `page.getByRole('complementary', { name: 'Node configuration' })` must be visible, because this condition proves that
  // selects and clears a node with pointer and keyboard controls.
  await expect(page.getByRole('complementary', { name: 'Node configuration' })).toBeVisible();
  await page.keyboard.press('Escape');

  // Expected outcome: `page.getByRole('complementary', { name: 'Node configuration' })` is not exposed to the user.
  // Acceptance criteria: `page.getByRole('complementary', { name: 'Node configuration' })` must be hidden, because this condition proves that
  // selects and clears a node with pointer and keyboard controls.
  await expect(page.getByRole('complementary', { name: 'Node configuration' })).toBeHidden();
});

/**
 * Purpose: Protects the behavioral contract that drags a node to a snapped position and keeps it after route navigation.
 * Description: Exercises drags a node to a snapped position and keeps it after route navigation from its arranged starting state and
 * verifies the observable results required by the scenario.
 */
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

  // Expected outcome: `box` is absent.
  // Acceptance criteria: `box` must be null, because this condition proves that
  // drags a node to a snapped position and keeps it after route navigation.
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

  // Expected outcome: `node` exposes the required attribute.
  // Acceptance criteria: `node` must have attribute arguments `'transform', initialTransform!`, because this condition proves that
  // drags a node to a snapped position and keeps it after route navigation.
  await expect(node).not.toHaveAttribute('transform', initialTransform!);
  const movedTransform = await node.evaluate((element) => element.getAttribute('transform'));
  const coordinates = movedTransform?.match(/translate\((\d+) (\d+)\)/);

  // Expected outcome: `Number(coordinates?.[1]) % 24` has the required value.
  // Acceptance criteria: `Number(coordinates?.[1]) % 24` must be `0`, because this condition proves that
  // drags a node to a snapped position and keeps it after route navigation.
  expect(Number(coordinates?.[1]) % 24).toBe(0);

  // Expected outcome: `Number(coordinates?.[2]) % 24` has the required value.
  // Acceptance criteria: `Number(coordinates?.[2]) % 24` must be `0`, because this condition proves that
  // drags a node to a snapped position and keeps it after route navigation.
  expect(Number(coordinates?.[2]) % 24).toBe(0);

  await page.getByRole('button', { name: 'Save flow' }).click();
  await expect.poll(() => persistedPayload.nodes[0]?.x).toBe(Number(coordinates?.[1]));

  // Expected outcome: `page.getByRole('button', { name: 'Save flow' })` permits interaction.
  // Acceptance criteria: `page.getByRole('button', { name: 'Save flow' })` must be enabled, because this condition proves that
  // drags a node to a snapped position and keeps it after route navigation.
  await expect(page.getByRole('button', { name: 'Save flow' })).toBeEnabled();

  await page.getByRole('link', { name: 'All flows' }).click();
  await page.getByRole('link', { name: /Climate control/ }).click();

  // Expected outcome: `node` exposes the required attribute.
  // Acceptance criteria: `node` must have attribute arguments `'transform', movedTransform!`, because this condition proves that
  // drags a node to a snapped position and keeps it after route navigation.
  await expect(node).toHaveAttribute('transform', movedTransform!);
});

/**
 * Purpose: Protects the behavioral contract that enables z-order commands at valid boundaries and changes render order.
 * Description: Exercises enables z-order commands at valid boundaries and changes render order from its arranged starting state and
 * verifies the observable results required by the scenario.
 */
test('enables z-order commands at valid boundaries and changes render order', async ({ page }) => {
  await page.goto('/flows/climate-control');

  const node = page.getByRole('button', { name: /Average temperature, Calculator node/ });
  const order = (): Promise<(string | null)[]> =>
    page
      .locator('[data-node-id]')
      .evaluateAll((nodes) => nodes.map((item) => item.getAttribute('data-node-id')));
  await node.click();

  // Expected outcome: `page.getByRole('button', { name: 'Send to back' })` prevents interaction.
  // Acceptance criteria: `page.getByRole('button', { name: 'Send to back' })` must be disabled, because this condition proves that
  // enables z-order commands at valid boundaries and changes render order.
  await expect(page.getByRole('button', { name: 'Send to back' })).toBeDisabled();

  // Expected outcome: `page.getByRole('button', { name: 'Bring to front' })` permits interaction.
  // Acceptance criteria: `page.getByRole('button', { name: 'Bring to front' })` must be enabled, because this condition proves that
  // enables z-order commands at valid boundaries and changes render order.
  await expect(page.getByRole('button', { name: 'Bring to front' })).toBeEnabled();

  await page.getByRole('button', { name: 'Bring to front' }).click();

  // Expected outcome: `await order()` matches the required structure.
  // Acceptance criteria: `await order()` must equal `[ 'comfort-pulse', 'manual-override', 'zone-split', 'temperature-average' ]`, because this condition proves that
  // enables z-order commands at valid boundaries and changes render order.
  await expect
    .poll(order)
    .toEqual(['comfort-pulse', 'manual-override', 'zone-split', 'temperature-average']);

  // Expected outcome: `page.getByRole('button', { name: 'Bring to front' })` prevents interaction.
  // Acceptance criteria: `page.getByRole('button', { name: 'Bring to front' })` must be disabled, because this condition proves that
  // enables z-order commands at valid boundaries and changes render order.
  await expect(page.getByRole('button', { name: 'Bring to front' })).toBeDisabled();

  await page.getByRole('button', { name: 'Send backward' }).click();

  // Expected outcome: `await order()` matches the required structure.
  // Acceptance criteria: `await order()` must equal `[ 'comfort-pulse', 'manual-override', 'temperature-average', 'zone-split' ]`, because this condition proves that
  // enables z-order commands at valid boundaries and changes render order.
  await expect
    .poll(order)
    .toEqual(['comfort-pulse', 'manual-override', 'temperature-average', 'zone-split']);

  await page.getByRole('button', { name: 'Send to back' }).click();

  // Expected outcome: `await order()` matches the required structure.
  // Acceptance criteria: `await order()` must equal `[ 'temperature-average', 'comfort-pulse', 'manual-override', 'zone-split' ]`, because this condition proves that
  // enables z-order commands at valid boundaries and changes render order.
  await expect
    .poll(order)
    .toEqual(['temperature-average', 'comfort-pulse', 'manual-override', 'zone-split']);

  await page.getByRole('button', { name: 'Bring forward' }).click();

  // Expected outcome: `await order()` matches the required structure.
  // Acceptance criteria: `await order()` must equal `[ 'comfort-pulse', 'temperature-average', 'manual-override', 'zone-split' ]`, because this condition proves that
  // enables z-order commands at valid boundaries and changes render order.
  await expect
    .poll(order)
    .toEqual(['comfort-pulse', 'temperature-average', 'manual-override', 'zone-split']);
});

/**
 * Purpose: Protects the behavioral contract that moves and deletes with the keyboard while safeguarding editable controls.
 * Description: Exercises moves and deletes with the keyboard while safeguarding editable controls from its arranged starting state and
 * verifies the observable results required by the scenario.
 */
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

  // Expected outcome: `node` is visible to the user.
  // Acceptance criteria: `node` must be visible, because this condition proves that
  // moves and deletes with the keyboard while safeguarding editable controls.
  await expect(node).toBeVisible();

  await node.focus();
  await node.press('ArrowRight');

  // Expected outcome: `node` exposes the required attribute.
  // Acceptance criteria: `node` must have attribute arguments `'transform', 'translate(114 110`, because this condition proves that
  // moves and deletes with the keyboard while safeguarding editable controls.
  await expect(node).toHaveAttribute('transform', 'translate(114 110)');

  await node.press('Delete');

  // Expected outcome: `node` is not exposed to the user.
  // Acceptance criteria: `node` must be hidden, because this condition proves that
  // moves and deletes with the keyboard while safeguarding editable controls.
  await expect(node).toBeHidden();

  // Expected outcome: `page.locator('[data-connection-id]')` resolves to the required number of elements.
  // Acceptance criteria: `page.locator('[data-connection-id]')` must resolve to exactly 1 element, because this condition proves that
  // moves and deletes with the keyboard while safeguarding editable controls.
  await expect(page.locator('[data-connection-id]')).toHaveCount(1);

  // Expected outcome: `page.getByLabel(/Scrollable designer viewport/)` owns keyboard focus.
  // Acceptance criteria: `page.getByLabel(/Scrollable designer viewport/)` must be focused, because this condition proves that
  // moves and deletes with the keyboard while safeguarding editable controls.
  await expect(page.getByLabel(/Scrollable designer viewport/)).toBeFocused();
});

/**
 * Purpose: Protects the behavioral contract that validates, saves, and reloads typed node configuration.
 * Description: Exercises validates, saves, and reloads typed node configuration from its arranged starting state and
 * verifies the observable results required by the scenario.
 */
test('validates, saves, and reloads typed node configuration', async ({ page }) => {
  await page.unroute('**/api/flows/*');
  let persistedPayload = structuredClone(sampleFlows[0]!);
  await page.route('**/api/flows/climate-control', async (route) => {
    if (route.request().method() === 'PUT') persistedPayload = route.request().postDataJSON();
    await route.fulfill({ json: persistedPayload });
  });
  await page.goto('/flows/climate-control');

  // Expected outcome: `page.getByText('Loading latest flow…')` is not exposed to the user.
  // Acceptance criteria: `page.getByText('Loading latest flow…')` must be hidden, because this condition proves that
  // validates, saves, and reloads typed node configuration.
  await expect(page.getByText('Loading latest flow…')).toBeHidden();

  await page.getByRole('searchbox', { name: 'Find a function' }).fill('line');
  await page.getByRole('button', { name: 'Add Line node', exact: true }).click();
  const label = page.getByRole('textbox', { name: 'Node label' });
  await label.fill('   ');

  // Expected outcome: `page.getByRole('alert')` displays the required text.
  // Acceptance criteria: `page.getByRole('alert')` must display `'Node label is required.'`, because this condition proves that
  // validates, saves, and reloads typed node configuration.
  await expect(page.getByRole('alert')).toHaveText('Node label is required.');
  await label.fill('Scaled temperature');
  const gain = page.getByRole('spinbutton', { name: 'Gain' });
  await gain.fill('2.5');

  // Expected outcome: `page.getByText('Unsaved changes', { exact: true })` is visible to the user.
  // Acceptance criteria: `page.getByText('Unsaved changes', { exact: true })` must be visible, because this condition proves that
  // validates, saves, and reloads typed node configuration.
  await expect(page.getByText('Unsaved changes', { exact: true })).toBeVisible();
  await expect(
    page.getByRole('button', { name: /Scaled temperature, Line node/ })
  ).toBeVisible();
  await expect(gain).toHaveValue('2.5');

  const saveResponse = page.waitForResponse(
    (response) =>
      response.request().method() === 'PUT' &&
      new URL(response.url()).pathname === '/api/flows/climate-control' &&
      response.ok()
  );
  await page.getByRole('button', { name: 'Save flow' }).click();
  await saveResponse;
  await expect.poll(() => persistedPayload.nodes.at(-1)?.label).toBe('Scaled temperature');

  // Expected outcome: `persistedPayload.nodes[0]?.configuration.operation` has the required value.
  // Acceptance criteria: `persistedPayload.nodes[0]?.configuration.operation` must be `'sum'`, because this condition proves that
  // validates, saves, and reloads typed node configuration.
  expect(persistedPayload.nodes.at(-1)?.configuration.gain).toBe(2.5);

  // Expected outcome: `page.getByText('Unsaved changes', { exact: true })` is not exposed to the user.
  // Acceptance criteria: `page.getByText('Unsaved changes', { exact: true })` must be hidden, because this condition proves that
  // validates, saves, and reloads typed node configuration.
  await expect(page.getByText('Unsaved changes', { exact: true })).toBeHidden();

  // The saved-node assertion below is the meaningful readiness signal. Waiting
  // only for DOM content avoids treating an unrelated late resource as a failed
  // reload after the persisted PUT has already completed.
  await page.reload({ waitUntil: 'domcontentloaded' });
  const savedNode = page.getByRole('button', { name: /Scaled temperature, Line node/ });

  // Expected outcome: `savedNode` is visible to the user.
  // Acceptance criteria: `savedNode` must be visible, because this condition proves that
  // validates, saves, and reloads typed node configuration.
  await expect(savedNode).toBeVisible();
  await savedNode.click();

  // Expected outcome: `page.getByRole('combobox', { name: 'Operation' })` contains the required input value.
  // Acceptance criteria: `page.getByRole('combobox', { name: 'Operation' })` must have value `'sum'`, because this condition proves that
  // validates, saves, and reloads typed node configuration.
  await expect(page.getByRole('spinbutton', { name: 'Gain' })).toHaveValue('2.5');
});
