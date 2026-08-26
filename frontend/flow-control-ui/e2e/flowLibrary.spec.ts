import {
  expect,
  flowsCollectionPattern,
  pagedFlows,
  test,
  useMutableFlowsApi
} from './fixtures/flowTest';

import { sampleFlows } from '@/features/flows/__tests__/fixtures/sampleFlows';
import type { FlowDefinition } from '@/features/flows/types';

/**
 * Flow library end-to-end coverage.
 *
 * Each scenario owns one user-facing contract and receives fresh mocked API
 * state from the shared fixture, so it remains safe to run alone or in parallel.
 */

/**
 * Purpose: Protects the behavioral contract that opens the flow library and navigates to a designer.
 * Description: Exercises opens the flow library and navigates to a designer from its arranged starting state and
 * verifies the observable results required by the scenario.
 */
test('opens the flow library and navigates to a designer', async ({ page }) => {
  await page.goto('/flows');

  // Expected outcome: `page.getByRole('heading', { name: 'Flows' })` is visible to the user.
  // Acceptance criteria: `page.getByRole('heading', { name: 'Flows' })` must be visible, because this condition proves that
  // opens the flow library and navigates to a designer.
  await expect(page.getByRole('heading', { name: 'Flows' })).toBeVisible();
  await page.getByRole('link', { name: /Climate control/ }).click();

  // Expected outcome: Navigation reaches the required route.
  // Acceptance criteria: the page URL must match `/\/flows\/climate-control\/design$/`, because this condition proves that
  // opens the flow library and navigates to a designer.
  await expect(page).toHaveURL(/\/flows\/climate-control\/design$/);

  // Expected outcome: `page.getByRole('heading', { name: 'Climate control' })` is visible to the user.
  // Acceptance criteria: `page.getByRole('heading', { name: 'Climate control' })` must be visible, because this condition proves that
  // opens the flow library and navigates to a designer.
  await expect(page.getByRole('heading', { name: 'Climate control' })).toBeVisible();

  // Expected outcome: `page.getByRole('group', { name: 'Climate control flow graph' })` is visible to the user.
  // Acceptance criteria: `page.getByRole('group', { name: 'Climate control flow graph' })` must be visible, because this condition proves that
  // opens the flow library and navigates to a designer.
  await expect(page.getByRole('group', { name: 'Climate control flow graph' })).toBeVisible();
});

/**
 * Purpose: Protects the behavioral contract that shows flow-library loading, empty, error, and retry states.
 * Description: Exercises shows flow-library loading, empty, error, and retry states from its arranged starting state and
 * verifies the observable results required by the scenario.
 */
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

  // Expected outcome: `page.locator('.request-status')` displays the required text.
  // Acceptance criteria: `page.locator('.request-status')` must display `'Loading flows…'`, because this condition proves that
  // shows flow-library loading, empty, error, and retry states.
  await expect(page.locator('.request-status')).toHaveText('Loading flows…');
  releaseEmpty();
  const emptyTable = page.getByRole('table', { name: 'Flows' });

  // Expected outcome: `emptyTable` is visible to the user.
  // Acceptance criteria: `emptyTable` must be visible, because this condition proves that
  // shows flow-library loading, empty, error, and retry states.
  await expect(emptyTable).toBeVisible();

  // Expected outcome: `emptyTable.getByRole('row')` resolves to the required number of elements.
  // Acceptance criteria: `emptyTable.getByRole('row')` must resolve to exactly 3 elements, because this condition proves that
  // shows flow-library loading, empty, error, and retry states.
  // The 3 rows are, header row, empty table state row displaying 'No results found' and footer status row.
  await expect(emptyTable.getByRole('row')).toHaveCount(3);

  // Expected outcome: the table body contains one empty-state row displaying the required message.
  // Acceptance criteria: the table body row must display `No results found.`, proving that no flow data rows are displayed.
  await expect(emptyTable.locator('tbody').getByRole('row')).toHaveText('No results found.');

  // Expected outcome: the table footer contains one status row displaying the required result count.
  // Acceptance criteria: the table footer row must display `0 total results`, proving that the empty result count is displayed.
  await expect(emptyTable.locator('tfoot').getByRole('row')).toHaveText('0 total results');

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

  // Expected outcome: `page.getByRole('alert')` displays the required content.
  // Acceptance criteria: `page.getByRole('alert')` must contain the text `'offline'`, because this condition proves that
  // shows flow-library loading, empty, error, and retry states.
  await expect(page.getByRole('alert')).toContainText('offline');
  shouldFail = false;
  await page.getByRole('button', { name: 'Retry' }).click();

  // Expected outcome: `page.getByRole('link', { name: /Climate control/ })` is visible to the user.
  // Acceptance criteria: `page.getByRole('link', { name: /Climate control/ })` must be visible, because this condition proves that
  // shows flow-library loading, empty, error, and retry states.
  await expect(page.getByRole('link', { name: /Climate control/ })).toBeVisible();
});

/**
 * Purpose: Protects the behavioral contract that creates a flow and opens its designer.
 * Description: Exercises creates a flow and opens its designer from its arranged starting state and
 * verifies the observable results required by the scenario.
 */
test('creates a flow and opens its designer', async ({ page }) => {
  // Arrange: start from the flow library backed by an isolated mutable API.
  await useMutableFlowsApi(page);
  await page.goto('/flows');

  // Assert the initial form contract before entering valid data. Whitespace is
  // deliberately checked because it must not enable a meaningless create.
  const newFlowName = page.getByRole('textbox', { name: 'New flow name' });
  const newFlowButton = page.getByRole('button', { name: 'New flow', exact: true }); // Exact match as there are multiple buttons that match the substring

  // Expected outcome: `newFlowName` exposes the required attribute.
  // Acceptance criteria: `newFlowName` must have attribute arguments `'placeholder', 'Enter new flow name'`, because this condition proves that
  // creates a flow and opens its designer.
  await expect(newFlowName).toHaveAttribute('placeholder', 'Enter new flow name');

  // Expected outcome: `newFlowName` exposes the required attribute.
  // Acceptance criteria: `newFlowName` must have attribute arguments `'autocomplete', 'off'`, because this condition proves that
  // creates a flow and opens its designer.
  await expect(newFlowName).toHaveAttribute('autocomplete', 'off');

  // Expected outcome: `newFlowButton` prevents interaction.
  // Acceptance criteria: `newFlowButton` must be disabled, because this condition proves that
  // creates a flow and opens its designer.
  await expect(newFlowButton).toBeDisabled();
  await newFlowName.fill('   ');

  // Expected outcome: `newFlowButton` prevents interaction.
  // Acceptance criteria: `newFlowButton` must be disabled, because this condition proves that
  // creates a flow and opens its designer.
  await expect(newFlowButton).toBeDisabled();

  // Act: submit a valid name through the same controls a user operates.
  await newFlowName.fill('New automation');

  // Expected outcome: `newFlowButton` permits interaction.
  // Acceptance criteria: `newFlowButton` must be enabled, because this condition proves that
  // creates a flow and opens its designer.
  await expect(newFlowButton).toBeEnabled();
  await newFlowButton.click();

  // Assert: creation opens the new flow immediately so it is ready to edit.

  // Expected outcome: Navigation reaches the required route.
  // Acceptance criteria: the page URL must match `/\/flows\/new-automation$/`, because this condition proves that
  // creates a flow and opens its designer.
  await expect(page).toHaveURL(/\/flows\/new-automation\/design$/);

  // Expected outcome: `page.getByRole('heading', { name: 'New automation' })` is visible to the user.
  // Acceptance criteria: `page.getByRole('heading', { name: 'New automation' })` must be visible, because this condition proves that
  // creates a flow and opens its designer.
  await expect(page.getByRole('heading', { name: 'New automation' })).toBeVisible();

  // Expected outcome: `page.getByRole('group', { name: 'New automation flow graph' })` is visible to the user.
  // Acceptance criteria: `page.getByRole('group', { name: 'New automation flow graph' })` must be visible, because this condition proves that
  // creates a flow and opens its designer.
  await expect(page.getByRole('group', { name: 'New automation flow graph' })).toBeVisible();
});

/**
 * Purpose: Protects the behavioral contract that renames a flow and opens the renamed designer.
 * Description: Exercises renames a flow and opens the renamed designer from its arranged starting state and
 * verifies the observable results required by the scenario.
 */
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

  // Expected outcome: `page.getByRole('heading', { name: 'Renamed climate' })` is visible to the user.
  // Acceptance criteria: `page.getByRole('heading', { name: 'Renamed climate' })` must be visible, because this condition proves that
  // renames a flow and opens the renamed designer.
  await expect(page.getByRole('heading', { name: 'Renamed climate' })).toBeVisible();

  // Expected outcome: `page.getByText('4 nodes', { exact: true })` is visible to the user.
  // Acceptance criteria: `page.getByText('4 nodes', { exact: true })` must be visible, because this condition proves that
  // renames a flow and opens the renamed designer.
  await expect(page.getByText('4 nodes', { exact: true })).toBeVisible();
});

/**
 * Purpose: Protects the behavioral contract that deletes a flow only after explicit confirmation.
 * Description: Exercises deletes a flow only after explicit confirmation from its arranged starting state and
 * verifies the observable results required by the scenario.
 */
test('deletes a flow only after explicit confirmation', async ({ page }) => {
  // Arrange: locate one known server-backed row in a clean flow library.
  await useMutableFlowsApi(page);
  await page.goto('/flows');
  const climateRow = page.getByRole('row').filter({ hasText: 'Climate control' });

  // Act: request deletion, then use the destructive confirmation control.
  await climateRow.getByRole('button', { name: 'Delete' }).click();
  await climateRow.getByRole('button', { name: 'Confirm delete' }).click();

  // Assert: the card disappears only after the API accepts the DELETE request.

  // Expected outcome: `page.getByRole('link', { name: /Climate control/ })` resolves to the required number of elements.
  // Acceptance criteria: `page.getByRole('link', { name: /Climate control/ })` must resolve to exactly 0 elements, because this condition proves that
  // deletes a flow only after explicit confirmation.
  await expect(page.getByRole('link', { name: /Climate control/ })).toHaveCount(0);
});

/**
 * Purpose: Protects the behavioral contract that enables and disables a flow from the table with matching text and icons.
 * Description: Exercises enables and disables a flow from the table with matching text and icons from its arranged starting state and
 * verifies the observable results required by the scenario.
 */
test('enables and disables a flow from the table with matching text and icons', async ({
  page
}) => {
  let disabled = false;
  const responseFlow = (): FlowDefinition => ({
    ...structuredClone(sampleFlows[1]!),
    disabled
  });
  await page.route('**/api/flows/garden-irrigation/disable', async (route) => {
    disabled = true;
    await route.fulfill({ json: responseFlow() });
  });
  await page.route('**/api/flows/garden-irrigation/enable', async (route) => {
    disabled = false;
    await route.fulfill({ json: responseFlow() });
  });
  await page.goto('/flows');

  const gardenRow = page.getByRole('row').filter({ hasText: 'Garden irrigation' });
  const disableButton = gardenRow.getByRole('button', { name: 'Disable' });

  // Expected outcome: `disableButton` is visible to the user.
  // Acceptance criteria: `disableButton` must be visible, because this condition proves that
  // enables and disables a flow from the table with matching text and icons.
  await expect(disableButton).toBeVisible();
  const disableMask = await disableButton
    .locator('.button-icon')
    .evaluate((icon) => getComputedStyle(icon).maskImage);

  // Expected outcome: `disableMask` has the required value.
  // Acceptance criteria: `disableMask` must be `'none'`, because this condition proves that
  // enables and disables a flow from the table with matching text and icons.
  expect(disableMask).not.toBe('none');
  await disableButton.click();

  const enableButton = gardenRow.getByRole('button', { name: 'Enable' });

  // Expected outcome: `enableButton` is visible to the user.
  // Acceptance criteria: `enableButton` must be visible, because this condition proves that
  // enables and disables a flow from the table with matching text and icons.
  await expect(enableButton).toBeVisible();

  // Expected outcome: `gardenRow.getByText('deployed · disabled', { exact: true })` is visible to the user.
  // Acceptance criteria: `gardenRow.getByText('deployed · disabled', { exact: true })` must be visible, because this condition proves that
  // enables and disables a flow from the table with matching text and icons.
  await expect(gardenRow.getByText('deployed · disabled', { exact: true })).toBeVisible();
  const enableMask = await enableButton
    .locator('.button-icon')
    .evaluate((icon) => getComputedStyle(icon).maskImage);

  // Expected outcome: `enableMask` has the required value.
  // Acceptance criteria: `enableMask` must be `'none'`, because this condition proves that
  // enables and disables a flow from the table with matching text and icons.
  expect(enableMask).not.toBe('none');

  // Expected outcome: `enableMask` has the required value.
  // Acceptance criteria: `enableMask` must be `disableMask`, because this condition proves that
  // enables and disables a flow from the table with matching text and icons.
  expect(enableMask).not.toBe(disableMask);
  await enableButton.click();

  // Expected outcome: `gardenRow.getByRole('button', { name: 'Disable' })` is visible to the user.
  // Acceptance criteria: `gardenRow.getByRole('button', { name: 'Disable' })` must be visible, because this condition proves that
  // enables and disables a flow from the table with matching text and icons.
  await expect(gardenRow.getByRole('button', { name: 'Disable' })).toBeVisible();

  // Expected outcome: `gardenRow.getByText('deployed', { exact: true })` is visible to the user.
  // Acceptance criteria: `gardenRow.getByText('deployed', { exact: true })` must be visible, because this condition proves that
  // enables and disables a flow from the table with matching text and icons.
  await expect(gardenRow.getByText('deployed', { exact: true })).toBeVisible();
});

/**
 * Purpose: Protects the behavioral contract that filters, sorts, and paginates the semantic flow table.
 * Description: Exercises filters, sorts, and paginates the semantic flow table from its arranged starting state and
 * verifies the observable results required by the scenario.
 */
test('filters, sorts, and paginates the semantic flow table', async ({ page }) => {
  test.setTimeout(60_000);
  await page.unroute(flowsCollectionPattern);
  const manyFlows = Array.from({ length: 25 }, (_, index) => ({
    ...structuredClone(sampleFlows[0]!),
    id: `flow-${index + 1}`,
    name: `Flow ${String(index + 1).padStart(2, '0')}`,
    status: index % 2 === 0 ? ('deployed' as const) : ('draft' as const)
  }));
  await page.route(flowsCollectionPattern, async (route) => {
    await route.fulfill({ json: pagedFlows(manyFlows, route.request().url()) });
  });

  await page.goto('/flows');
  const table = page.getByRole('table', { name: 'Flows' });
  const applyFilterButton = page.getByRole('button', { name: 'Apply filter' });

  // Expected outcome: The shared filter exposes a stable automation hook on a semantic search form.
  // Acceptance criteria: The search landmark has data-automation "flows-filter" because every page filter must use the reusable AppFilter contract.
  await expect(page.getByRole('search')).toHaveAttribute('data-automation', 'flows.filter');

  // Expected outcome: The apply action remains on one line at the current viewport.
  // Acceptance criteria: The button text uses nowrap because wrapping made catalogue filters taller than the approved flows layout.
  expect(
    await applyFilterButton
      .locator('.button-text')
      .evaluate((element) => getComputedStyle(element).whiteSpace)
  ).toBe('nowrap');

  // Expected outcome: `table` is visible to the user.
  // Acceptance criteria: `table` must be visible, because this condition proves that
  // filters, sorts, and paginates the semantic flow table.
  await expect(table).toBeVisible();

  // Expected outcome: `table.getByRole('columnheader', { name: /Name/ })` exposes the required attribute.
  // Acceptance criteria: `table.getByRole('columnheader', { name: /Name/ })` must have attribute arguments `'aria-sort', 'ascending'`, because this condition proves that
  // filters, sorts, and paginates the semantic flow table.
  await expect(table.getByRole('columnheader', { name: /Name/ })).toHaveAttribute(
    'aria-sort',
    'ascending'
  );

  // Expected outcome: `table.getByRole('row')` resolves to the required number of elements.
  // Acceptance criteria: `table.getByRole('row')` must resolve to exactly 11 elements, because this condition proves that
  // filters, sorts, and paginates the semantic flow table.
  await expect(table.getByRole('row')).toHaveCount(11);

  const nameFilter = page.getByRole('searchbox', { name: 'Filter by name' });
  await nameFilter.fill('No matching flow');
  await applyFilterButton.click();

  // Expected outcome: `page.getByText('No flows match the selected filters.')` is visible to the user.
  // Acceptance criteria: `page.getByText('No flows match the selected filters.')` must be visible, because this condition proves that
  // filters, sorts, and paginates the semantic flow table.
  await expect(page.getByText('No flows match the selected filters.')).toBeVisible();

  // Expected outcome: `table` is visible to the user.
  // Acceptance criteria: `table` must be visible, because this condition proves that
  // filters, sorts, and paginates the semantic flow table.
  await expect(table).toBeVisible();

  // Expected outcome: `table.getByRole('row')` resolves to the required number of elements.
  // Acceptance criteria: `table.getByRole('row')` must resolve to exactly 1 elements, because this condition proves that
  // filters, sorts, and paginates the semantic flow table.
  await expect(table.getByRole('row')).toHaveCount(1);

  // Expected outcome: `page.getByRole('button', { name: 'Deployment status: All' })` is visible to the user.
  // Acceptance criteria: `page.getByRole('button', { name: 'Deployment status: All' })` must be visible, because this condition proves that
  // filters, sorts, and paginates the semantic flow table.
  await expect(page.getByRole('button', { name: 'Deployment status: All' })).toBeVisible();
  await nameFilter.fill('');
  await applyFilterButton.click();
  // Wait for the debounced clear-filter request to finish before changing the
  // sort. Without this user-visible checkpoint, Firefox can click while the
  // previous list refresh is still settling and the response wait becomes racy.

  // Expected outcome: `table.getByRole('row')` resolves to the required number of elements.
  // Acceptance criteria: `table.getByRole('row')` must resolve to exactly 11 elements, because this condition proves that
  // filters, sorts, and paginates the semantic flow table.
  await expect(table.getByRole('row')).toHaveCount(11);

  const sortButton = page.getByRole('button', { name: /Name, sorted ascending/ });

  // Expected outcome: `sortButton.locator('.button-icon')` resolves to the required number of elements.
  // Acceptance criteria: `sortButton.locator('.button-icon')` must resolve to exactly 1 elements, because this condition proves that
  // filters, sorts, and paginates the semantic flow table.
  await expect(sortButton.locator('.button-icon')).toHaveCount(1);

  // Expected outcome: `await sortButton.locator('.button-icon'` has the required value.
  // Acceptance criteria: `await sortButton.locator('.button-icon'` must be `18`, because this condition proves that
  // filters, sorts, and paginates the semantic flow table.
  expect(
    await sortButton.locator('.button-icon').evaluate((icon) => icon.getBoundingClientRect().width)
  ).toBe(18);
  const descendingResponse = page.waitForResponse((response) => {
    const url = new URL(response.url());
    return url.pathname === '/api/flows' && url.searchParams.get('sort') === 'descending';
  });
  await sortButton.click();
  await descendingResponse;

  // Expected outcome: `table.getByRole('row'` displays the required content.
  // Acceptance criteria: `table.getByRole('row'` must contain the text `'Flow 25'`, because this condition proves that
  // filters, sorts, and paginates the semantic flow table.
  await expect(table.getByRole('row').nth(1)).toContainText('Flow 25');

  await nameFilter.fill('Flow 2');
  await applyFilterButton.click();

  // Expected outcome: Navigation reaches the required route.
  // Acceptance criteria: the page URL must match `/filter=Flow(?:%20|\+`, because this condition proves that
  // filters, sorts, and paginates the semantic flow table.
  await expect(page).toHaveURL(/filter=Flow(?:%20|\+)2/);

  // Expected outcome: `table.getByRole('row')` resolves to the required number of elements.
  // Acceptance criteria: `table.getByRole('row')` must resolve to exactly 7 elements, because this condition proves that
  // filters, sorts, and paginates the semantic flow table.
  await expect(table.getByRole('row')).toHaveCount(7);

  // Expected outcome: `page.getByText('1–6 of 6')` is visible to the user.
  // Acceptance criteria: `page.getByText('1–6 of 6')` must be visible, because this condition proves that
  // filters, sorts, and paginates the semantic flow table.
  await expect(page.getByText('1–6 of 6')).toBeVisible();

  await nameFilter.fill('Flow');
  await applyFilterButton.click();
  await page.getByLabel('Items per page').selectOption('20');
  await expect(page).toHaveURL(/pageSize=20/);
  await expect(page.getByText('1–20 of 25')).toBeVisible();
  const nextPageButton = page.getByRole('button', { name: 'Next page' });

  // Expected outcome: `nextPageButton.locator('.button-icon')` resolves to the required number of elements.
  // Acceptance criteria: `nextPageButton.locator('.button-icon')` must resolve to exactly 1 elements, because this condition proves that
  // filters, sorts, and paginates the semantic flow table.
  await expect(nextPageButton.locator('.button-icon')).toHaveCount(1);

  // Expected outcome: `await nextPageButton .locator('.button-icon'` has the required value.
  // Acceptance criteria: `await nextPageButton .locator('.button-icon'` must be `18`, because this condition proves that
  // filters, sorts, and paginates the semantic flow table.
  expect(
    await nextPageButton
      .locator('.button-icon')
      .evaluate((icon) => icon.getBoundingClientRect().width)
  ).toBe(18);
  await nextPageButton.click();

  // Expected outcome: Navigation reaches the required route.
  // Acceptance criteria: the page URL must match `/page=2/`, because this condition proves that
  // filters, sorts, and paginates the semantic flow table.
  await expect(page).toHaveURL(/page=2/);

  // Expected outcome: Navigation reaches the required route.
  // Acceptance criteria: the page URL must match `/pageSize=20/`, because this condition proves that
  // filters, sorts, and paginates the semantic flow table.
  await expect(page).toHaveURL(/pageSize=20/);

  // Expected outcome: Navigation reaches the required route.
  // Acceptance criteria: the page URL must match `/filter=Flow/`, because this condition proves that
  // filters, sorts, and paginates the semantic flow table.
  await expect(page).toHaveURL(/filter=Flow/);

  // Expected outcome: `page.getByText('21–25 of 25')` is visible to the user.
  // Acceptance criteria: `page.getByText('21–25 of 25')` must be visible, because this condition proves that
  // filters, sorts, and paginates the semantic flow table.
  await expect(page.getByText('21–25 of 25')).toBeVisible();

  const statusDropdown = page.getByRole('button', {
    name: 'Deployment status: All'
  });
  await expect(statusDropdown).toHaveAttribute('aria-expanded', 'false');
  await statusDropdown.focus();
  await page.keyboard.press('Enter');
  await expect(statusDropdown).toHaveAttribute('aria-expanded', 'true');
  // Keep the filter controls interactive while the previous debounced list
  // request settles. FlowListView deliberately retains this DOM during refresh.
  const draftStatus = page.getByRole('checkbox', { name: 'Draft' });
  await expect(draftStatus).toBeVisible();
  // Send the keyboard action through the locator so a concurrent Firefox
  // repaint cannot move document focus between focus() and page.keyboard.
  await draftStatus.press('Space');
  await expect(draftStatus).not.toBeChecked();
  const deployedStatusDropdown = page.getByRole('button', {
    name: 'Deployment status: Deployed'
  });

  // Expected outcome: `deployedStatusDropdown` is visible to the user.
  // Acceptance criteria: `deployedStatusDropdown` must be visible, because this condition proves that
  // filters, sorts, and paginates the semantic flow table.
  await expect(deployedStatusDropdown).toBeVisible();
  await page.keyboard.press('Escape');
  await expect(deployedStatusDropdown).toHaveAttribute('aria-expanded', 'false');
  await applyFilterButton.click();

  // Expected outcome: Navigation reaches the required route.
  // Acceptance criteria: the page URL must match `/status=deployed/`, because this condition proves that
  // filters, sorts, and paginates the semantic flow table.
  await expect(page).toHaveURL(/status=deployed/);

  // Expected outcome: Navigation reaches the required route.
  // Acceptance criteria: the page URL must match `/page=2/`, because this condition proves that
  // filters, sorts, and paginates the semantic flow table.
  await expect(page).not.toHaveURL(/page=2/);

  // Expected outcome: `page.getByText('1–13 of 13')` is visible to the user.
  // Acceptance criteria: `page.getByText('1–13 of 13')` must be visible, because this condition proves that
  // filters, sorts, and paginates the semantic flow table.
  await expect(page.getByText('1–13 of 13')).toBeVisible();

  // Expected outcome: `table.getByRole('row')` resolves to the required number of elements.
  // Acceptance criteria: `table.getByRole('row')` must resolve to exactly 14 elements, because this condition proves that
  // filters, sorts, and paginates the semantic flow table.
  await expect(table.getByRole('row')).toHaveCount(14);

  await page.getByLabel('Items per page').selectOption('10');
  await expect(page.getByText('1–10 of 13')).toBeVisible();
  await page.getByRole('button', { name: 'Next page' }).click();

  // Expected outcome: Navigation reaches the required route.
  // Acceptance criteria: the page URL must match `/status=deployed/`, because this condition proves that
  // filters, sorts, and paginates the semantic flow table.
  await expect(page).toHaveURL(/status=deployed/);

  // Expected outcome: `page.getByText('11–13 of 13')` is visible to the user.
  // Acceptance criteria: `page.getByText('11–13 of 13')` must be visible, because this condition proves that
  // filters, sorts, and paginates the semantic flow table.
  await expect(page.getByText('11–13 of 13')).toBeVisible();

  await deployedStatusDropdown.focus();
  await page.keyboard.press('Enter');
  await expect(deployedStatusDropdown).toHaveAttribute('aria-expanded', 'true');
  const allStatuses = page.getByRole('checkbox', { name: 'All' });
  await allStatuses.focus();
  await page.keyboard.press('Space');
  await expect(allStatuses).toBeChecked();
  await expect(page.getByRole('checkbox', { name: 'Draft' })).toBeChecked();
  await expect(page.getByRole('checkbox', { name: 'Deployed' })).toBeChecked();
  // Dismiss the menu without targeting the toggle while its accessible name is
  // changing from "Deployed" to "All". Firefox can otherwise resolve the click
  // against the stale state and leave the menu covering the Apply action.
  await page.keyboard.press('Escape');
  await expect(allStatuses).toBeHidden();
  const allStatusesResponse = page.waitForResponse((response) => {
    const url = new URL(response.url());
    return (
      url.pathname === '/api/flows' &&
      url.searchParams.getAll('status').includes('draft') &&
      url.searchParams.getAll('status').includes('deployed') &&
      url.searchParams.get('page') === '1'
    );
  });
  await applyFilterButton.click();
  await allStatusesResponse;

  // Expected outcome: Navigation reaches the required route.
  // Acceptance criteria: the page URL must match `/status=deployed/`, because this condition proves that
  // filters, sorts, and paginates the semantic flow table.
  await expect(page).toHaveURL(/status=deployed/);

  // Expected outcome: Navigation reaches the required route.
  // Acceptance criteria: the page URL must match `/status=draft/`, because this condition proves that
  // filters, sorts, and paginates the semantic flow table.
  await expect(page).toHaveURL(/status=draft/);

  // Expected outcome: `page.getByText('1–10 of 25')` is visible to the user.
  // Acceptance criteria: `page.getByText('1–10 of 25')` must be visible, because this condition proves that
  // filters, sorts, and paginates the semantic flow table.
  await expect(page.getByText('1–10 of 25')).toBeVisible();
});

/**
 * Purpose: Protects the behavioral contract that uses the shared button contract for visible and icon-only actions.
 * Description: Exercises uses the shared button contract for visible and icon-only actions from its arranged starting state and
 * verifies the observable results required by the scenario.
 */
test('uses the shared button contract for visible and icon-only actions', async ({ page }) => {
  // Arrange: render the library because it contains enabled, disabled,
  // text-labelled, and icon-only examples of the shared button component.
  await page.goto('/flows');

  // Assert every native button opts into the shared visual/interaction contract.
  const renderedButtons = page.locator('button');

  // Expected outcome: `renderedButtons.first()` is visible to the user.
  // Acceptance criteria: `renderedButtons.first()` must be visible, because this condition proves that
  // uses the shared button contract for visible and icon-only actions.
  await expect(renderedButtons.first()).toBeVisible();

  // Expected outcome: `await renderedButtons.count()` satisfies the required boundary.
  // Acceptance criteria: `await renderedButtons.count()` must satisfy the asserted boundary against `0`, because this condition proves that
  // uses the shared button contract for visible and icon-only actions.
  expect(await renderedButtons.count()).toBeGreaterThan(0);

  // Expected outcome: `await renderedButtons.evaluateAll((buttons) => buttons.every((button) => button.hasAttribute('data-a` has the required value.
  // Acceptance criteria: `await renderedButtons.evaluateAll((buttons) => buttons.every((button) => button.hasAttribute('data-a` must be `true`, because this condition proves that
  // uses the shared button contract for visible and icon-only actions.
  expect(
    await renderedButtons.evaluateAll((buttons) =>
      buttons.every((button) => button.hasAttribute('data-app-button'))
    )
  ).toBe(true);

  const newFlowButton = page.getByRole('button', { name: 'New flow' });

  // Expected outcome: `newFlowButton.locator('.button-text')` displays the required text.
  // Acceptance criteria: `newFlowButton.locator('.button-text')` must display `'New flow'`, because this condition proves that
  // uses the shared button contract for visible and icon-only actions.
  await expect(newFlowButton.locator('.button-text')).toHaveText('New flow');

  // Expected outcome: `newFlowButton` exposes the required attribute.
  // Acceptance criteria: `newFlowButton` must have attribute arguments `'aria-label'`, because this condition proves that
  // uses the shared button contract for visible and icon-only actions.
  await expect(newFlowButton).not.toHaveAttribute('aria-label');

  // Assert disabled and enabled states remain visually distinguishable without
  // coupling this check to the separate flow-creation behaviour.
  const disabledBackground = await newFlowButton.evaluate(
    (button) => getComputedStyle(button).backgroundColor
  );

  // Expected outcome: `newFlowButton` uses the required rendered style.
  // Acceptance criteria: `newFlowButton` must have CSS arguments `'border-style', 'dashed'`, because this condition proves that
  // uses the shared button contract for visible and icon-only actions.
  await expect(newFlowButton).toHaveCSS('border-style', 'dashed');
  await page.getByRole('textbox', { name: 'New flow name' }).fill('New automation');

  // Expected outcome: `newFlowButton` uses the required rendered style.
  // Acceptance criteria: `newFlowButton` must have CSS arguments `'background-color', disabledBackground`, because this condition proves that
  // uses the shared button contract for visible and icon-only actions.
  await expect(newFlowButton).not.toHaveCSS('background-color', disabledBackground);

  // Expected outcome: `newFlowButton` uses the required rendered style.
  // Acceptance criteria: `newFlowButton` must have CSS arguments `'border-style', 'solid'`, because this condition proves that
  // uses the shared button contract for visible and icon-only actions.
  await expect(newFlowButton).toHaveCSS('border-style', 'solid');

  // Act: expose the inline rename controls, which are deliberately icon-only.
  await page.getByRole('button', { name: 'Rename' }).first().click();
  const iconOnlyButtons = [
    ['Save name', page.getByRole('button', { name: 'Save name' })],
    ['Cancel', page.getByRole('button', { name: 'Cancel' })]
  ] as const;

  for (const [label, button] of iconOnlyButtons) {
    // Expected outcome: `button` exposes the required attribute.
    // Acceptance criteria: `button` must have attribute arguments `'aria-label', label`, because this condition proves that
    // uses the shared button contract for visible and icon-only actions.
    await expect(button).toHaveAttribute('aria-label', label);

    // Expected outcome: `button.locator('.button-text')` resolves to the required number of elements.
    // Acceptance criteria: `button.locator('.button-text')` must resolve to exactly 0 elements, because this condition proves that
    // uses the shared button contract for visible and icon-only actions.
    await expect(button.locator('.button-text')).toHaveCount(0);

    // Expected outcome: `button.locator('.button-icon')` resolves to the required number of elements.
    // Acceptance criteria: `button.locator('.button-icon')` must resolve to exactly 1 elements, because this condition proves that
    // uses the shared button contract for visible and icon-only actions.
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

    // Expected outcome: `colors[1]` has the required value.
    // Acceptance criteria: `colors[1]` must be `colors[0]`, because this condition proves that
    // uses the shared button contract for visible and icon-only actions.
    expect(colors[1]).toBe(colors[0]);
  }
});

/**
 * Purpose: Protects the behavioral contract that shows a useful message for an unknown flow.
 * Description: Exercises shows a useful message for an unknown flow from its arranged starting state and
 * verifies the observable results required by the scenario.
 */
test('shows a useful message for an unknown flow', async ({ page }) => {
  await page.goto('/flows/not-a-flow');

  // Expected outcome: `page.getByText('Flow not found', { exact: true })` is visible to the user.
  // Acceptance criteria: `page.getByText('Flow not found', { exact: true })` must be visible, because this condition proves that
  // shows a useful message for an unknown flow.
  await expect(page.getByText('Flow not found', { exact: true })).toBeVisible();

  // Expected outcome: `page.getByRole('heading', { name: 'There is no flow named “not-a-flow”.' })` is visible to the user.
  // Acceptance criteria: `page.getByRole('heading', { name: 'There is no flow named “not-a-flow”.' })` must be visible, because this condition proves that
  // shows a useful message for an unknown flow.
  await expect(
    page.getByRole('heading', { name: 'There is no flow named “not-a-flow”.' })
  ).toBeVisible();
  await page.getByRole('link', { name: 'Return to flows' }).click();

  // Expected outcome: Navigation reaches the required route.
  // Acceptance criteria: the page URL must match `/\/flows$/`, because this condition proves that
  // shows a useful message for an unknown flow.
  await expect(page).toHaveURL(/\/flows$/);
});
