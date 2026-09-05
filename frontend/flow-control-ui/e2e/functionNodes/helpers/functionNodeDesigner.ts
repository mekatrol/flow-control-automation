import { expect, type Locator, type Page } from '@playwright/test';

type PointNodeLabel = 'Analog Input' | 'Analog Output' | 'Digital Input' | 'Digital Output';
export type NodeConfigurationValue = boolean | number | string;

const nodeGroup = (page: Page, nodeId: string): Locator =>
  page.locator(`[data-node-id="${nodeId}"]`);

export const moveNode = async (
  page: Page,
  nodeId: string,
  position: { x: number; y: number }
): Promise<void> => {
  const node = nodeGroup(page, nodeId);
  const selector = node.locator('.node-selector');
  const transform = await selector.getAttribute('transform');
  const coordinates = transform?.match(/^translate\((\d+) (\d+)\)$/);
  const box = await selector.boundingBox();
  expect(coordinates, `Node ${nodeId} must expose designer coordinates.`).not.toBeNull();
  expect(box, `Node ${nodeId} must be visible before it can be arranged.`).not.toBeNull();

  const currentX = Number(coordinates![1]);
  const currentY = Number(coordinates![2]);
  if (currentX === position.x && currentY === position.y) return;

  const pointerId = 17;
  const startX = box!.x + box!.width / 2;
  const startY = box!.y + box!.height / 2;
  const graph = page.getByRole('group', { name: /flow graph$/ });
  const graphBox = await graph.boundingBox();
  const viewBox = (await graph.getAttribute('viewBox'))?.split(' ').map(Number);
  expect(graphBox, 'The designer graph must be visible before arranging nodes.').not.toBeNull();
  expect(viewBox, 'The designer graph must expose its logical view box.').toHaveLength(4);
  const clientScaleX = graphBox!.width / viewBox![2]!;
  const clientScaleY = graphBox!.height / viewBox![3]!;
  await selector.dispatchEvent('pointerdown', {
    button: 0,
    clientX: startX,
    clientY: startY,
    pointerId
  });
  await graph.dispatchEvent('pointermove', {
    clientX: startX + (position.x - currentX) * clientScaleX,
    clientY: startY + (position.y - currentY) * clientScaleY,
    pointerId
  });
  await graph.dispatchEvent('pointerup', { pointerId });
  await expect(selector).toHaveAttribute('transform', `translate(${position.x} ${position.y})`);
};

export const createFlow = async (page: Page, name: string): Promise<string> => {
  await page.goto('/flows');
  await page.getByRole('button', { name: 'Add a new flow' }).click();
  const dialog = page.getByRole('dialog', { name: 'Create new flow' });
  await dialog.getByRole('textbox', { name: 'Flow name' }).fill(name);
  await dialog.getByRole('button', { name: 'Create flow' }).click();
  await expect(page).toHaveURL(/\/flows\/[^/]+\/design$/);
  return new URL(page.url()).pathname.split('/').at(-2)!;
};

export const addNode = async (page: Page, nodeLabel: string): Promise<string> => {
  const search = page.getByRole('searchbox', { name: 'Find a function' });
  await search.fill(nodeLabel);
  await page.getByRole('button', { name: `Add ${nodeLabel} node`, exact: true }).click();
  const selected = page.locator('.flow-node.selected');
  await expect(selected).toBeVisible();
  return (await selected.getAttribute('data-node-id'))!;
};

export const configureSelectedNode = async (
  page: Page,
  configuration: Record<string, NodeConfigurationValue>
): Promise<void> => {
  const panel = page.getByRole('complementary', { name: 'Node configuration' });
  for (const [label, value] of Object.entries(configuration)) {
    if (typeof value === 'boolean') {
      const checkbox = panel.getByRole('checkbox', { name: label });
      if ((await checkbox.isChecked()) !== value) await checkbox.click();
    } else if (typeof value === 'number') {
      await panel.getByRole('spinbutton', { name: label }).fill(String(value));
    } else {
      const control = panel
        .getByText(label, { exact: true })
        .locator('..')
        .locator('input, select');
      if ((await control.evaluate((element) => element.tagName)) === 'SELECT')
        await control.selectOption(value);
      else {
        await control.fill(value);
        await control.blur();
        await expect(control).toHaveValue(value);
      }
    }
  }
};

export const addVirtualPointNode = async (
  page: Page,
  nodeLabel: PointNodeLabel,
  pointId: string
): Promise<string> => {
  const analog = nodeLabel.startsWith('Analog');
  const virtualNodeId = await addNode(page, analog ? 'Analog Virtual' : 'Digital Virtual');
  await configureSelectedNode(page, { 'Virtual point key': pointId });
  return virtualNodeId;
};

export const connectNodes = async (
  page: Page,
  source: { nodeId: string; connector: string },
  destination: { nodeId: string; connector: string }
): Promise<void> => {
  const sourceConnector = nodeGroup(page, source.nodeId).getByRole('button', {
    name: new RegExp(`^${source.connector}, output,`)
  });
  const destinationConnector = nodeGroup(page, destination.nodeId).getByRole('button', {
    name: new RegExp(`^${destination.connector}, input,`)
  });
  const connectionCount = await page.locator('[data-connection-id]').count();
  await sourceConnector.focus();
  await sourceConnector.press('Enter');
  await destinationConnector.focus();
  await destinationConnector.press('Enter');
  await expect(page.locator('[data-connection-id]')).toHaveCount(connectionCount + 1);
};

export const saveFlow = async (page: Page, flowId: string): Promise<void> => {
  const [response] = await Promise.all([
    page.waitForResponse(
      (response) =>
        response.request().method() === 'PUT' &&
        new URL(response.url()).pathname === `/api/flows/${flowId}`,
      { timeout: 10_000 }
    ),
    page.getByRole('button', { name: 'Save flow' }).click()
  ]);
  expect(response.ok(), await response.text()).toBeTruthy();
  await expect(page.getByText('Unsaved changes', { exact: true })).toBeHidden();
};
