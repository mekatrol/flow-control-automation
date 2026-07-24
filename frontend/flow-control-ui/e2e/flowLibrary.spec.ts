import {
  expect,
  flowsCollectionPattern,
  pagedFlows,
  test,
  useMutableFlowsApi
} from './fixtures/flowTest';

import { sampleFlows } from '@/features/flows/__tests__/fixtures/sampleFlows';

/**
 * Flow library end-to-end coverage.
 *
 * Each scenario owns one user-facing contract and receives fresh mocked API
 * state from the shared fixture, so it remains safe to run alone or in parallel.
 */

test('opens the flow library and navigates to a designer', async ({ page }) => {
  await page.goto('/flows');

  await expect(page.getByRole('heading', { name: 'Flows' })).toBeVisible();
  await page.getByRole('link', { name: /Climate control/ }).click();

  await expect(page).toHaveURL(/\/flows\/climate-control$/);
  await expect(page.getByRole('heading', { name: 'Climate control' })).toBeVisible();
  await expect(page.getByRole('group', { name: 'Climate control flow graph' })).toBeVisible();
});

test('shows flow-library loading, empty, error, and retry states', async ({ page }) => {
  await page.unroute(flowsCollectionPattern);
  let releaseEmpty!: () => void;
  const emptyReady = new Promise<void>((resolve) => {
    releaseEmpty = resolve;
  });
  await page.route(flowsCollectionPattern, async (route) => {
    await emptyReady;
    await route.fulfill({ json: pagedFlows([], route.request().url()) });
  });

  await page.goto('/flows');
  await expect(page.locator('.request-status')).toHaveText('Loading flows…');
  releaseEmpty();
  await expect(page.getByRole('heading', { name: 'No flows yet' })).toBeVisible();

  await page.unroute(flowsCollectionPattern);
  let shouldFail = true;
  await page.route(flowsCollectionPattern, async (route) => {
    if (shouldFail) {
      await route.fulfill({ status: 503, json: { message: 'offline' } });
      return;
    }
    await route.fulfill({ json: pagedFlows(sampleFlows, route.request().url()) });
  });
  await page.reload();
  await expect(page.getByRole('alert')).toContainText('offline');
  shouldFail = false;
  await page.getByRole('button', { name: 'Retry' }).click();
  await expect(page.getByRole('link', { name: /Climate control/ })).toBeVisible();
});

test('creates a flow and opens its designer', async ({ page }) => {
  // Arrange: start from the flow library backed by an isolated mutable API.
  await useMutableFlowsApi(page);
  await page.goto('/flows');

  // Assert the initial form contract before entering valid data. Whitespace is
  // deliberately checked because it must not enable a meaningless create.
  const newFlowName = page.getByRole('textbox', { name: 'New flow name' });
  const newFlowButton = page.getByRole('button', { name: 'New flow' });
  await expect(newFlowName).toHaveAttribute('placeholder', 'Enter new flow name');
  await expect(newFlowName).toHaveAttribute('autocomplete', 'off');
  await expect(newFlowButton).toBeDisabled();
  await newFlowName.fill('   ');
  await expect(newFlowButton).toBeDisabled();

  // Act: submit a valid name through the same controls a user operates.
  await newFlowName.fill('New automation');
  await expect(newFlowButton).toBeEnabled();
  await newFlowButton.click();

  // Assert: creation opens the new flow immediately so it is ready to edit.
  await expect(page).toHaveURL(/\/flows\/new-automation$/);
  await expect(page.getByRole('heading', { name: 'New automation' })).toBeVisible();
  await expect(page.getByRole('group', { name: 'New automation flow graph' })).toBeVisible();
});

test('renames a flow and opens the renamed designer', async ({ page }) => {
  // Arrange: use a fresh server state so this test has no dependency on create.
  await useMutableFlowsApi(page);
  await page.goto('/flows');

  // Act: rename the existing climate flow from its table row.
  const climateRow = page.getByRole('row').filter({ hasText: 'Climate control' });
  await climateRow.getByRole('button', { name: 'Rename' }).click();
  await page.getByRole('textbox', { name: 'Rename Climate control' }).fill('Renamed climate');
  await page.getByRole('button', { name: 'Save name' }).click();

  // Assert both the updated library label and the detail route backed by the
  // persisted response. The graph count proves the rename retained flow data.
  await page.getByRole('link', { name: /Renamed climate/ }).click();
  await expect(page.getByRole('heading', { name: 'Renamed climate' })).toBeVisible();
  await expect(page.getByText('4 nodes', { exact: true })).toBeVisible();
});

test('deletes a flow only after explicit confirmation', async ({ page }) => {
  // Arrange: locate one known server-backed row in a clean flow library.
  await useMutableFlowsApi(page);
  await page.goto('/flows');
  const climateRow = page.getByRole('row').filter({ hasText: 'Climate control' });

  // Act: request deletion, then use the destructive confirmation control.
  await climateRow.getByRole('button', { name: 'Delete' }).click();
  await climateRow.getByRole('button', { name: 'Confirm delete' }).click();

  // Assert: the card disappears only after the API accepts the DELETE request.
  await expect(page.getByRole('link', { name: /Climate control/ })).toHaveCount(0);
});

test('filters, sorts, and paginates the semantic flow table', async ({ page }) => {
  await page.unroute(flowsCollectionPattern);
  const manyFlows = Array.from({ length: 25 }, (_, index) => ({
    ...structuredClone(sampleFlows[0]!),
    id: `flow-${index + 1}`,
    name: `Flow ${String(index + 1).padStart(2, '0')}`,
    status: index % 2 === 0 ? 'deployed' as const : 'draft' as const
  }));
  await page.route(flowsCollectionPattern, async (route) => {
    await route.fulfill({ json: pagedFlows(manyFlows, route.request().url()) });
  });

  await page.goto('/flows');
  const table = page.getByRole('table', { name: 'Flows' });
  await expect(table).toBeVisible();
  await expect(table.getByRole('columnheader', { name: /Name/ })).toHaveAttribute(
    'aria-sort',
    'ascending'
  );
  await expect(table.getByRole('row')).toHaveCount(11);

  const nameFilter = page.getByRole('searchbox', { name: 'Filter by name' });
  await nameFilter.fill('No matching flow');
  await expect(page.getByText('No flows match the selected filters.')).toBeVisible();
  await expect(
    page.getByRole('button', { name: 'Deployment status: All' })
  ).toBeVisible();
  await nameFilter.fill('');

  const sortButton = page.getByRole('button', { name: /Name, sorted ascending/ });
  await expect(sortButton.locator('.button-icon')).toHaveCount(1);
  expect(await sortButton.locator('.button-icon').evaluate((icon) => icon.getBoundingClientRect().width)).toBe(18);
  await sortButton.click();
  await expect(table.getByRole('row').nth(1)).toContainText('Flow 25');

  await nameFilter.fill('Flow 2');
  await expect(page).toHaveURL(/filter=Flow(?:%20|\+)2/);
  await expect(table.getByRole('row')).toHaveCount(7);
  await expect(page.getByText('1–6 of 6')).toBeVisible();

  await nameFilter.fill('Flow');
  await page.getByLabel('Items per page').selectOption('20');
  const nextPageButton = page.getByRole('button', { name: 'Next page' });
  await expect(nextPageButton.locator('.button-icon')).toHaveCount(1);
  expect(await nextPageButton.locator('.button-icon').evaluate((icon) => icon.getBoundingClientRect().width)).toBe(18);
  await nextPageButton.click();
  await expect(page).toHaveURL(/page=2/);
  await expect(page).toHaveURL(/pageSize=20/);
  await expect(page).toHaveURL(/filter=Flow/);
  await expect(page.getByText('21–25 of 25')).toBeVisible();

  const statusDropdown = page.getByRole('button', {
    name: 'Deployment status: All'
  });
  await statusDropdown.click();
  await page.getByRole('checkbox', { name: 'Draft' }).uncheck();
  await page.getByRole('button', { name: 'Deployment status: Deployed' }).click();
  await expect(page).toHaveURL(/status=deployed/);
  await expect(page).not.toHaveURL(/page=2/);
  await expect(page.getByText('1–13 of 13')).toBeVisible();
  await expect(table.getByRole('row')).toHaveCount(14);

  await page.getByLabel('Items per page').selectOption('10');
  await page.getByRole('button', { name: 'Next page' }).click();
  await expect(page).toHaveURL(/status=deployed/);
  await expect(page.getByText('11–13 of 13')).toBeVisible();

  await page.getByRole('button', { name: 'Deployment status: Deployed' }).click();
  await page.getByRole('checkbox', { name: 'All' }).check();
  await expect(page).toHaveURL(/status=deployed/);
  await expect(page).toHaveURL(/status=draft/);
  await expect(page.getByText('1–10 of 25')).toBeVisible();
});

test('uses the shared button contract for visible and icon-only actions', async ({ page }) => {
  // Arrange: render the library because it contains enabled, disabled,
  // text-labelled, and icon-only examples of the shared button component.
  await page.goto('/flows');

  // Assert every native button opts into the shared visual/interaction contract.
  const renderedButtons = page.locator('button');
  await expect(renderedButtons.first()).toBeVisible();
  expect(await renderedButtons.count()).toBeGreaterThan(0);
  expect(
    await renderedButtons.evaluateAll((buttons) =>
      buttons.every((button) => button.hasAttribute('data-app-button'))
    )
  ).toBe(true);

  const newFlowButton = page.getByRole('button', { name: 'New flow' });
  await expect(newFlowButton.locator('.button-text')).toHaveText('New flow');
  await expect(newFlowButton).not.toHaveAttribute('aria-label');

  // Assert disabled and enabled states remain visually distinguishable without
  // coupling this check to the separate flow-creation behaviour.
  const disabledBackground = await newFlowButton.evaluate(
    (button) => getComputedStyle(button).backgroundColor
  );
  await expect(newFlowButton).toHaveCSS('border-style', 'dashed');
  await page.getByRole('textbox', { name: 'New flow name' }).fill('New automation');
  await expect(newFlowButton).not.toHaveCSS('background-color', disabledBackground);
  await expect(newFlowButton).toHaveCSS('border-style', 'solid');

  // Act: expose the inline rename controls, which are deliberately icon-only.
  await page.getByRole('button', { name: 'Rename' }).first().click();
  const iconOnlyButtons = [
    ['Save name', page.getByRole('button', { name: 'Save name' })],
    ['Cancel', page.getByRole('button', { name: 'Cancel' })]
  ] as const;

  for (const [label, button] of iconOnlyButtons) {
    await expect(button).toHaveAttribute('aria-label', label);
    await expect(button.locator('.button-text')).toHaveCount(0);
    await expect(button.locator('.button-icon')).toHaveCount(1);
    await expect
      .poll(() =>
        button.locator('.button-icon').evaluate((icon) => getComputedStyle(icon).maskImage)
      )
      .not.toBe('none');

    // Assert masked icons inherit the button's current foreground colour. This
    // protects contrast in hover, focus, disabled, and themed button states.
    const colors = await button.evaluate((element) => {
      const iconElement = element.querySelector<HTMLElement>('.button-icon')!;
      return [getComputedStyle(element).color, getComputedStyle(iconElement).backgroundColor];
    });
    expect(colors[1]).toBe(colors[0]);
  }
});

test('shows a useful message for an unknown flow', async ({ page }) => {
  await page.goto('/flows/not-a-flow');

  await expect(page.getByText('Flow not found', { exact: true })).toBeVisible();
  await expect(
    page.getByRole('heading', { name: 'There is no flow named “not-a-flow”.' })
  ).toBeVisible();
  await page.getByRole('link', { name: 'Return to flows' }).click();
  await expect(page).toHaveURL(/\/flows$/);
});
