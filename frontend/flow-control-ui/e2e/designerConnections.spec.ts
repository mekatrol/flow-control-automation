import { expect, test } from './fixtures/flowTest';

/**
 * Designer connections end-to-end coverage.
 *
 * Each scenario owns one user-facing contract and receives fresh mocked API
 * state from the shared fixture, so it remains safe to run alone or in parallel.
 */

test('highlights compatible connectors, previews a link, and rejects invalid completion', async ({
  page
}) => {
  await page.goto('/flows/climate-control');

  const source = page.getByRole('button', { name: /Average, output, number/ });
  await source.click();
  await expect(
    page.getByRole('button', { name: /Automatic, input, number, compatible destination/ })
  ).toBeVisible();
  await expect(
    page.getByRole('button', { name: /Value, input, number, compatible destination/ })
  ).toHaveCount(0);

  const preview = page.locator('[data-connection-id="connection-preview"] .flow-connection');
  await expect(preview).toBeVisible();
  const initialPath = await preview.getAttribute('d');
  const canvasBox = await page
    .getByRole('group', { name: 'Climate control flow graph' })
    .boundingBox();
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
  await expect(preview).not.toHaveAttribute('d', initialPath!);
  await page.keyboard.press('Escape');
  await expect(preview).toBeHidden();

  const invalidStart = page.getByRole('button', { name: /Values, input, number/ });
  await invalidStart.focus();
  await page.keyboard.press('Enter');
  await expect(page.getByRole('alert')).toContainText('Start a connection from an output');

  await expect(
    page.locator('[data-connection-id]:not([data-connection-id="connection-preview"])')
  ).toHaveCount(2);
});

test('creates a connection with the keyboard and deletes a selected connection', async ({
  page
}) => {
  await page.goto('/flows/climate-control');

  const source = page.getByRole('button', { name: /Average, output, number/ });
  const destination = page.getByRole('button', { name: /Automatic, input, number/ });
  await source.focus();
  await page.keyboard.press('Enter');
  await expect(page.locator('[data-connection-id="connection-preview"]')).toBeVisible();
  await destination.focus();
  await page.keyboard.press('Enter');
  await expect(
    page.locator('[data-connection-id]:not([data-connection-id="connection-preview"])')
  ).toHaveCount(3);

  const connection = page.getByRole('button', {
    name: 'Connection from temperature-average to comfort-pulse'
  });
  await connection.click();
  await expect(page.getByText('Selected connection: temperature-to-pulse')).toBeVisible();
  await page.keyboard.press('Delete');

  await expect(connection).toBeHidden();
  await expect(
    page.locator('[data-connection-id]:not([data-connection-id="connection-preview"])')
  ).toHaveCount(2);
  await expect(page.getByLabel(/Scrollable designer viewport/)).toBeFocused();

  const keyboardConnection = page.getByRole('button', {
    name: 'Connection from temperature-average to manual-override'
  });
  await keyboardConnection.focus();
  await page.keyboard.press('Enter');
  await page.keyboard.press('Delete');
  await expect(keyboardConnection).toBeHidden();
  await expect(
    page.locator('[data-connection-id]:not([data-connection-id="connection-preview"])')
  ).toHaveCount(1);
});

test('drags from an output connector to a compatible input connector', async ({ page }) => {
  await page.goto('/flows/climate-control');

  const source = page.getByRole('button', { name: /Average, output, number/ });
  const destination = page.getByRole('button', { name: /Automatic, input, number/ });
  const sourceBox = await source.boundingBox();
  const destinationBox = await destination.boundingBox();
  expect(sourceBox).not.toBeNull();
  expect(destinationBox).not.toBeNull();

  await source.dispatchEvent('pointerdown', {
    button: 0,
    clientX: sourceBox!.x + sourceBox!.width / 2,
    clientY: sourceBox!.y + sourceBox!.height / 2,
    pointerId: 9
  });
  await expect(page.locator('[data-connection-id="connection-preview"]')).toBeVisible();
  await destination.dispatchEvent('pointerup', {
    button: 0,
    clientX: destinationBox!.x + destinationBox!.width / 2,
    clientY: destinationBox!.y + destinationBox!.height / 2,
    pointerId: 9
  });

  await expect(page.locator('[data-connection-id="connection-preview"]')).toBeHidden();
  await expect(
    page.locator('[data-connection-id]:not([data-connection-id="connection-preview"])')
  ).toHaveCount(3);
});
