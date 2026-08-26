import { expect, test } from '@playwright/test';

import { pagedFlows } from './fixtures/flowTest';

import type { FlowDefinition, FlowNode } from '@/features/flows/types';

const emptyFlow = (): FlowDefinition => ({
  id: 'critical-journey',
  name: 'Critical journey',
  description: '',
  status: 'draft',
  disabled: false,
  updatedAt: '2026-07-14T09:00:00+10:00',
  nodes: [],
  connections: []
});

/**
 * Purpose: Protects the behavioral contract that creates, edits, saves, deploys, and reloads a flow as one critical journey.
 * Description: Exercises creates, edits, saves, deploys, and reloads a flow as one critical journey from its arranged starting state and
 * verifies the observable results required by the scenario.
 */
test('creates, edits, saves, deploys, and reloads a flow as one critical journey', async ({
  page
}) => {
  let savedFlow: FlowDefinition | undefined;
  let runtimeState = 'stopped';

  await page.route(
    (url) => url.pathname.startsWith('/api/'),
    async (route) => {
      const request = route.request();
      const path = new URL(request.url()).pathname;
      if (path.endsWith('/runtime')) {
        await route.fulfill({
          json: {
            flowId: 'critical-journey',
            state: runtimeState,
            updatedAt: new Date().toISOString(),
            nodes: {}
          }
        });
        return;
      }
      if (path.endsWith('/deploy')) {
        runtimeState = 'running';
        await route.fulfill({
          json: {
            flowId: 'critical-journey',
            state: runtimeState,
            updatedAt: new Date().toISOString(),
            nodes: {}
          }
        });
        return;
      }
      if (path === '/api/flows' && request.method() === 'GET') {
        await route.fulfill({
          json: pagedFlows(savedFlow ? [savedFlow] : [], request.url())
        });
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
    }
  );

  await page.goto('/flows');
  await page.getByRole('textbox', { name: 'New flow name' }).fill('Critical journey');
  await page.getByRole('button', { name: 'New flow', exact: true }).click();

  // Expected outcome: `page.getByRole('heading', { name: 'Critical journey' })` is visible to the user.
  // Acceptance criteria: `page.getByRole('heading', { name: 'Critical journey' })` must be visible, because this condition proves that
  // creates, edits, saves, deploys, and reloads a flow as one critical journey.
  await expect(page.getByRole('heading', { name: 'Critical journey' })).toBeVisible();

  await page.getByRole('button', { name: 'Calculator', exact: true }).click();
  await page.getByRole('textbox', { name: 'Node label' }).fill('Verified calculation');
  await page.getByRole('button', { name: 'Save flow' }).click();

  // Expected outcome: the "Unsaved changes" status is not rendered.
  // Acceptance criteria: the element must not exist in the DOM after the flow has been saved, deployed, and reloaded.
  await expect(page.getByText('Unsaved changes', { exact: true })).toHaveCount(0);

  await page.getByRole('button', { name: 'Deploy flow' }).click();
  await page.getByRole('button', { name: 'Deploy now' }).click();

  // Expected outcome: `page.getByRole('status', { name: 'Runtime state: running' })` is visible to the user.
  // Acceptance criteria: `page.getByRole('status', { name: 'Runtime state: running' })` must be visible, because this condition proves that
  // creates, edits, saves, deploys, and reloads a flow as one critical journey.
  await expect(page.getByRole('status', { name: 'Runtime state: running' })).toBeVisible();

  await page.reload();

  // Expected outcome: `page.getByRole('button', { name: /Verified calculation, Calculator node/ })` is visible to the user.
  // Acceptance criteria: `page.getByRole('button', { name: /Verified calculation, Calculator node/ })` must be visible, because this condition proves that
  // creates, edits, saves, deploys, and reloads a flow as one critical journey.
  await expect(
    page.getByRole('button', { name: /Verified calculation, Calculator node/ })
  ).toBeVisible();

  expect(savedFlow?.nodes[0]?.configuration).toEqual({});
});

/**
 * Purpose: Protects the behavioral contract that renders a large validated graph without dropping nodes or connections.
 * Description: Exercises renders a large validated graph without dropping nodes or connections from its arranged starting state and
 * verifies the observable results required by the scenario.
 */
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
    disabled: false,
    updatedAt: '2026-07-14T09:00:00+10:00',
    nodes,
    connections: nodes.slice(1).map((node, index) => ({
      id: `connection-${index}`,
      start: { nodeId: nodes[index]!.id, connectorId: 'output' },
      end: { nodeId: node.id, connectorId: 'input' }
    }))
  };

  await page.route(
    (url) => url.pathname.startsWith('/api/'),
    async (route) => {
      const path = new URL(route.request().url()).pathname;
      await route.fulfill(
        path.endsWith('/runtime')
          ? { json: { flowId: flow.id, state: 'stopped', updatedAt: flow.updatedAt, nodes: {} } }
          : { json: flow }
      );
    }
  );

  await page.goto('/flows/large-graph');

  // Expected outcome: `page.locator('[data-node-id]')` resolves to the required number of elements.
  // Acceptance criteria: `page.locator('[data-node-id]')` must resolve to exactly 120 elements, because this condition proves that
  // renders a large validated graph without dropping nodes or connections.
  await expect(page.locator('[data-node-id]')).toHaveCount(120);

  // Expected outcome: `page.locator('[data-connection-id]')` resolves to the required number of elements.
  // Acceptance criteria: `page.locator('[data-connection-id]')` must resolve to exactly 119 elements, because this condition proves that
  // renders a large validated graph without dropping nodes or connections.
  await expect(page.locator('[data-connection-id]')).toHaveCount(119);
});
