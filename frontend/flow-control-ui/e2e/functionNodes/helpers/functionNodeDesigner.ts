import { expect, type Locator, type Page } from '@playwright/test';

type PointNodeKind = 'Analog Input' | 'Analog Output' | 'Digital Input' | 'Digital Output';

const nodeGroup = (page: Page, nodeId: string): Locator =>
  page.locator(`[data-node-id="${nodeId}"]`);

export const createFlow = async (page: Page, name: string): Promise<string> => {
  await page.goto('/flows');
  await page.getByRole('button', { name: 'Add a new flow' }).click();
  const dialog = page.getByRole('dialog', { name: 'Create new flow' });
  await dialog.getByRole('textbox', { name: 'Flow name' }).fill(name);
  await dialog.getByRole('button', { name: 'Create flow' }).click();
  await expect(page).toHaveURL(/\/flows\/[^/]+\/design$/);
  return new URL(page.url()).pathname.split('/').at(-2)!;
};

export const addNode = async (page: Page, kind: string): Promise<string> => {
  const search = page.getByRole('searchbox', { name: 'Find a node' });
  await search.fill(kind);
  await page.getByRole('button', { name: `Add ${kind} node`, exact: true }).click();
  const selected = page.locator('.flow-node.selected');
  await expect(selected).toBeVisible();
  return (await selected.getAttribute('data-node-id'))!;
};

export const addVirtualPointNode = async (
  page: Page,
  kind: PointNodeKind,
  pointId: string
): Promise<string> => {
  const nodeId = await addNode(page, kind);
  await page.getByRole('button', { name: 'Create new virtual point' }).click();
  const form = page.getByRole('group', { name: 'Create virtual point' });
  await form.getByRole('textbox', { name: 'Point ID' }).fill(pointId);
  await form.getByRole('button', { name: 'Create', exact: true }).click();
  await expect(
    page.getByRole('complementary', { name: 'Node configuration' }).locator('input[list]')
  ).toHaveValue(pointId);
  return nodeId;
};

export const connectNodes = async (
  page: Page,
  source: { nodeId: string; connector: string },
  destination: { nodeId: string; connector: string }
): Promise<void> => {
  const sourceConnector = nodeGroup(page, source.nodeId).getByRole('button', {
    name: new RegExp(`^${source.connector}, output,`)
  });
  const destinationConnector = nodeGroup(page, destination.nodeId).getByRole(
    'button',
    { name: new RegExp(`^${destination.connector}, input,`) }
  );
  const connectionCount = await page.locator('[data-connection-id]').count();
  await sourceConnector.focus();
  await sourceConnector.press('Enter');
  await destinationConnector.focus();
  await destinationConnector.press('Enter');
  await expect(page.locator('[data-connection-id]')).toHaveCount(connectionCount + 1);
};

export const saveFlow = async (page: Page, flowId: string): Promise<void> => {
  const saved = page.waitForResponse(
    (response) =>
      response.request().method() === 'PUT' &&
      new URL(response.url()).pathname === `/api/flows/${flowId}` &&
      response.ok()
  );
  await page.getByRole('button', { name: 'Save flow' }).click();
  await saved;
  await expect(page.getByText('Unsaved changes', { exact: true })).toBeHidden();
};
