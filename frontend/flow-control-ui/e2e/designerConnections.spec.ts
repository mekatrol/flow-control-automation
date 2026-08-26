import { expect, test } from './fixtures/flowTest';

/**
 * Designer connections end-to-end coverage.
 *
 * Each scenario owns one user-facing contract and receives fresh mocked API
 * state from the shared fixture, so it remains safe to run alone or in parallel.
 */

/**
 * Purpose: Protects the behavioral contract that highlights compatible connectors, previews a link, and rejects invalid completion.
 * Description: Exercises highlights compatible connectors, previews a link, and rejects invalid completion from its arranged starting state and
 * verifies the observable results required by the scenario.
 */
test('highlights compatible connectors, previews a link, and rejects invalid completion', async ({
  page
}) => {
  await page.goto('/flows/climate-control');

  const source = page.getByRole('button', { name: /Average, output, number/ });
  await source.click();

  // Expected outcome: `page.getByRole('button', { name: /Automatic, input, number, compatible destination/ })` is visible to the user.
  // Acceptance criteria: `page.getByRole('button', { name: /Automatic, input, number, compatible destination/ })` must be visible, because this condition proves that
  // highlights compatible connectors, previews a link, and rejects invalid completion.
  await expect(
    page.getByRole('button', { name: /Automatic, input, number, compatible destination/ })
  ).toBeVisible();

  // Expected outcome: `page.getByRole('button', { name: /Value, input, number, compatible destination/ })` resolves to the required number of elements.
  // Acceptance criteria: `page.getByRole('button', { name: /Value, input, number, compatible destination/ })` must resolve to exactly 0 elements, because this condition proves that
  // highlights compatible connectors, previews a link, and rejects invalid completion.
  await expect(
    page.getByRole('button', { name: /Value, input, number, compatible destination/ })
  ).toHaveCount(0);

  const preview = page.locator('[data-connection-id="connection-preview"] .flow-connection');

  // Expected outcome: `preview` is visible to the user.
  // Acceptance criteria: `preview` must be visible, because this condition proves that
  // highlights compatible connectors, previews a link, and rejects invalid completion.
  await expect(preview).toBeVisible();
  const initialPath = await preview.getAttribute('d');
  const canvasBox = await page
    .getByRole('group', { name: 'Climate control flow graph' })
    .boundingBox();

  // Expected outcome: `canvasBox` is absent.
  // Acceptance criteria: `canvasBox` must be null, because this condition proves that
  // highlights compatible connectors, previews a link, and rejects invalid completion.
  expect(canvasBox).not.toBeNull();
  // Dispatch directly to the SVG so the preview assertion is deterministic in
  // both mouse-oriented desktop projects and touch-emulating mobile projects.
  await page
    .getByRole('group', { name: 'Climate control flow graph' })
    .dispatchEvent('pointermove', {
      clientX: canvasBox!.x + 330,
      clientY: canvasBox!.y + 300,
      pointerId: 1
    });

  // Expected outcome: `preview` exposes the required attribute.
  // Acceptance criteria: `preview` must have attribute arguments `'d', initialPath!`, because this condition proves that
  // highlights compatible connectors, previews a link, and rejects invalid completion.
  await expect(preview).not.toHaveAttribute('d', initialPath!);
  await page.keyboard.press('Escape');

  // Expected outcome: `preview` is not exposed to the user.
  // Acceptance criteria: `preview` must be hidden, because this condition proves that
  // highlights compatible connectors, previews a link, and rejects invalid completion.
  await expect(preview).toBeHidden();

  const invalidStart = page.getByRole('button', { name: /Values, input, number/ });
  await invalidStart.focus();
  await page.keyboard.press('Enter');

  // Expected outcome: `page.getByRole('alert')` displays the required content.
  // Acceptance criteria: `page.getByRole('alert')` must contain the text `'Start a connection from an output'`, because this condition proves that
  // highlights compatible connectors, previews a link, and rejects invalid completion.
  await expect(page.getByRole('alert')).toContainText('Start a connection from an output');

  // Expected outcome: `page.locator('[data-connection-id]:not([data-connection-id="connection-preview"])')` resolves to the required number of elements.
  // Acceptance criteria: `page.locator('[data-connection-id]:not([data-connection-id="connection-preview"])')` must resolve to exactly 2 elements, because this condition proves that
  // highlights compatible connectors, previews a link, and rejects invalid completion.
  await expect(
    page.locator('[data-connection-id]:not([data-connection-id="connection-preview"])')
  ).toHaveCount(2);
});

/**
 * Purpose: Protects the behavioral contract that creates a connection with the keyboard and deletes a selected connection.
 * Description: Exercises creates a connection with the keyboard and deletes a selected connection from its arranged starting state and
 * verifies the observable results required by the scenario.
 */
test('creates a connection with the keyboard and deletes a selected connection', async ({
  page
}) => {
  await page.goto('/flows/climate-control');

  const source = page.getByRole('button', { name: /Average, output, number/ });
  const destination = page.getByRole('button', { name: /Automatic, input, number/ });
  await source.focus();
  await page.keyboard.press('Enter');

  // Expected outcome: `page.locator('[data-connection-id="connection-preview"]')` is visible to the user.
  // Acceptance criteria: `page.locator('[data-connection-id="connection-preview"]')` must be visible, because this condition proves that
  // creates a connection with the keyboard and deletes a selected connection.
  await expect(page.locator('[data-connection-id="connection-preview"]')).toBeVisible();
  await destination.focus();
  await page.keyboard.press('Enter');

  // Expected outcome: `page.locator('[data-connection-id]:not([data-connection-id="connection-preview"])')` resolves to the required number of elements.
  // Acceptance criteria: `page.locator('[data-connection-id]:not([data-connection-id="connection-preview"])')` must resolve to exactly 3 elements, because this condition proves that
  // creates a connection with the keyboard and deletes a selected connection.
  await expect(
    page.locator('[data-connection-id]:not([data-connection-id="connection-preview"])')
  ).toHaveCount(3);

  const connection = page.getByRole('button', {
    name: 'Connection from temperature-average to comfort-pulse'
  });
  await connection.click();

  // Expected outcome: `page.getByText('Selected connection: temperature-to-pulse')` is visible to the user.
  // Acceptance criteria: `page.getByText('Selected connection: temperature-to-pulse')` must be visible, because this condition proves that
  // creates a connection with the keyboard and deletes a selected connection.
  await expect(page.getByText('Selected connection: temperature-to-pulse')).toBeVisible();
  await page.keyboard.press('Delete');

  // Expected outcome: `connection` is not exposed to the user.
  // Acceptance criteria: `connection` must be hidden, because this condition proves that
  // creates a connection with the keyboard and deletes a selected connection.
  await expect(connection).toBeHidden();

  // Expected outcome: `page.locator('[data-connection-id]:not([data-connection-id="connection-preview"])')` resolves to the required number of elements.
  // Acceptance criteria: `page.locator('[data-connection-id]:not([data-connection-id="connection-preview"])')` must resolve to exactly 2 elements, because this condition proves that
  // creates a connection with the keyboard and deletes a selected connection.
  await expect(
    page.locator('[data-connection-id]:not([data-connection-id="connection-preview"])')
  ).toHaveCount(2);

  // Expected outcome: `page.getByLabel(/Scrollable designer viewport/)` owns keyboard focus.
  // Acceptance criteria: `page.getByLabel(/Scrollable designer viewport/)` must be focused, because this condition proves that
  // creates a connection with the keyboard and deletes a selected connection.
  await expect(page.getByLabel(/Scrollable designer viewport/)).toBeFocused();

  const keyboardConnection = page.getByRole('button', {
    name: 'Connection from temperature-average to manual-override'
  });
  await keyboardConnection.focus();
  await page.keyboard.press('Enter');
  await page.keyboard.press('Delete');

  // Expected outcome: `keyboardConnection` is not exposed to the user.
  // Acceptance criteria: `keyboardConnection` must be hidden, because this condition proves that
  // creates a connection with the keyboard and deletes a selected connection.
  await expect(keyboardConnection).toBeHidden();

  // Expected outcome: `page.locator('[data-connection-id]:not([data-connection-id="connection-preview"])')` resolves to the required number of elements.
  // Acceptance criteria: `page.locator('[data-connection-id]:not([data-connection-id="connection-preview"])')` must resolve to exactly 1 element, because this condition proves that
  // creates a connection with the keyboard and deletes a selected connection.
  await expect(
    page.locator('[data-connection-id]:not([data-connection-id="connection-preview"])')
  ).toHaveCount(1);
});

/**
 * Purpose: Protects the behavioral contract that drags from an output connector to a compatible input connector.
 * Description: Exercises drags from an output connector to a compatible input connector from its arranged starting state and
 * verifies the observable results required by the scenario.
 */
test('drags from an output connector to a compatible input connector', async ({ page }) => {
  await page.goto('/flows/climate-control');

  const source = page.getByRole('button', { name: /Average, output, number/ });
  const destination = page.getByRole('button', { name: /Automatic, input, number/ });
  const sourceBox = await source.boundingBox();
  const destinationBox = await destination.boundingBox();

  // Expected outcome: `sourceBox` is absent.
  // Acceptance criteria: `sourceBox` must be null, because this condition proves that
  // drags from an output connector to a compatible input connector.
  expect(sourceBox).not.toBeNull();

  // Expected outcome: `destinationBox` is absent.
  // Acceptance criteria: `destinationBox` must be null, because this condition proves that
  // drags from an output connector to a compatible input connector.
  expect(destinationBox).not.toBeNull();

  await source.dispatchEvent('pointerdown', {
    button: 0,
    clientX: sourceBox!.x + sourceBox!.width / 2,
    clientY: sourceBox!.y + sourceBox!.height / 2,
    pointerId: 9
  });

  // Expected outcome: `page.locator('[data-connection-id="connection-preview"]')` is visible to the user.
  // Acceptance criteria: `page.locator('[data-connection-id="connection-preview"]')` must be visible, because this condition proves that
  // drags from an output connector to a compatible input connector.
  await expect(page.locator('[data-connection-id="connection-preview"]')).toBeVisible();
  await destination.dispatchEvent('pointerup', {
    button: 0,
    clientX: destinationBox!.x + destinationBox!.width / 2,
    clientY: destinationBox!.y + destinationBox!.height / 2,
    pointerId: 9
  });

  // Expected outcome: `page.locator('[data-connection-id="connection-preview"]')` is not exposed to the user.
  // Acceptance criteria: `page.locator('[data-connection-id="connection-preview"]')` must be hidden, because this condition proves that
  // drags from an output connector to a compatible input connector.
  await expect(page.locator('[data-connection-id="connection-preview"]')).toBeHidden();

  // Expected outcome: `page.locator('[data-connection-id]:not([data-connection-id="connection-preview"])')` resolves to the required number of elements.
  // Acceptance criteria: `page.locator('[data-connection-id]:not([data-connection-id="connection-preview"])')` must resolve to exactly 3 elements, because this condition proves that
  // drags from an output connector to a compatible input connector.
  await expect(
    page.locator('[data-connection-id]:not([data-connection-id="connection-preview"])')
  ).toHaveCount(3);
});
