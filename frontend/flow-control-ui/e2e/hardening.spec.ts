import { expect, test } from '@playwright/test';

import type { FlowDefinition, FlowNode } from '@/features/flows/types';

const emptyFlow = (): FlowDefinition => ({
  id: 'critical-journey',
  name: 'Critical journey',
  description: '',
  status: 'draft',
  updatedAt: '2026-07-14T09:00:00+10:00',
  nodes: [],
  connections: []
});

test('creates, edits, saves, deploys, and reloads a flow as one critical journey', async ({
  page
}) => {
  let savedFlow: FlowDefinition | undefined;
  let runtimeState = 'stopped';

  await page.route((url) => url.pathname.startsWith('/api/'), async (route) => {
    const request = route.request();
    const path = new URL(request.url()).pathname;
    if (path.endsWith('/runtime')) {
      await route.fulfill({
        json: { flowId: 'critical-journey', state: runtimeState, updatedAt: new Date().toISOString(), nodes: {} }
      });
      return;
    }
    if (path.endsWith('/deploy')) {
      runtimeState = 'running';
      await route.fulfill({
        json: { flowId: 'critical-journey', state: runtimeState, updatedAt: new Date().toISOString(), nodes: {} }
      });
      return;
    }
    if (path === '/api/flows' && request.method() === 'GET') {
      await route.fulfill({ json: savedFlow ? [savedFlow] : [] });
      return;
    }
    if (path === '/api/flows' && request.method() === 'POST') {
      savedFlow = emptyFlow();
      await route.fulfill({ json: savedFlow });
      return;
    }
    if (request.method() === 'PUT') {
      savedFlow = request.postDataJSON() as FlowDefinition;
      await route.fulfill({ json: savedFlow });
      return;
    }
    await route.fulfill({ json: savedFlow ?? emptyFlow() });
  });

  await page.goto('/flows');
  await page.getByRole('textbox', { name: 'New flow name' }).fill('Critical journey');
  await page.getByRole('button', { name: 'New flow' }).click();
  await page.getByRole('link', { name: /Critical journey/ }).click();

  await page.getByRole('button', { name: 'Add Calculator node' }).click();
  await page.getByRole('textbox', { name: 'Node label' }).fill('Verified calculation');
  await page.getByRole('combobox', { name: 'Operation' }).selectOption('sum');
  await page.getByRole('button', { name: 'Save flow' }).click();
  await expect(page.getByText('Unsaved changes')).toBeHidden();

  await page.getByRole('button', { name: 'Deploy flow' }).click();
  await page.getByRole('button', { name: 'Deploy now' }).click();
  await expect(page.getByRole('status', { name: 'Runtime state: running' })).toBeVisible();

  await page.reload();
  await expect(page.getByRole('button', { name: /Verified calculation, Calculator node/ })).toBeVisible();
  expect(savedFlow?.nodes[0]?.configuration.operation).toBe('sum');
});

test('renders a large validated graph without dropping nodes or connections', async ({ page }) => {
  const nodes: FlowNode[] = Array.from({ length: 120 }, (_, index) => ({
    id: `node-${index}`,
    kind: 'calculator',
    label: `Calculation ${index}`,
    x: 20 + (index % 5) * 230,
    y: 20 + (index % 8) * 65,
    zOrder: index,
    connectors: [
      { id: 'input', label: 'Values', direction: 'input', dataType: 'number', side: 'left' },
      { id: 'output', label: 'Result', direction: 'output', dataType: 'number', side: 'right' }
    ],
    configuration: { operation: 'sum' }
  }));
  const flow: FlowDefinition = {
    id: 'large-graph',
    name: 'Large graph',
    description: 'Render hardening fixture',
    status: 'draft',
    updatedAt: '2026-07-14T09:00:00+10:00',
    nodes,
    connections: nodes.slice(1).map((node, index) => ({
      id: `connection-${index}`,
      start: { nodeId: nodes[index]!.id, connectorId: 'output' },
      end: { nodeId: node.id, connectorId: 'input' }
    }))
  };

  await page.route((url) => url.pathname.startsWith('/api/'), async (route) => {
    const path = new URL(route.request().url()).pathname;
    await route.fulfill(
      path.endsWith('/runtime')
        ? { json: { flowId: flow.id, state: 'stopped', updatedAt: flow.updatedAt, nodes: {} } }
        : { json: flow }
    );
  });

  await page.goto('/flows/large-graph');
  await expect(page.locator('[data-node-id]')).toHaveCount(120);
  await expect(page.locator('[data-connection-id]')).toHaveCount(119);
});
