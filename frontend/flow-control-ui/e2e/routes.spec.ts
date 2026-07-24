import { expect, test } from '@playwright/test';

import { sampleFlows } from '@/features/flows/__tests__/fixtures/sampleFlows';

test.beforeEach(async ({ page }) => {
  await page.route('**/api/flows', async (route) => {
    if (route.request().method() === 'POST') {
      const { name } = route.request().postDataJSON() as { name: string };
      const id = name.toLocaleLowerCase().replaceAll(/[^a-z0-9]+/g, '-').replaceAll(/^-|-$/g, '');
      await route.fulfill({
        json: {
          id,
          name,
          description: '',
          status: 'draft',
          updatedAt: '2026-07-13T12:00:00+10:00',
          nodes: [],
          connections: []
        }
      });
      return;
    }
    await route.fulfill({ json: sampleFlows });
  });
  await page.route('**/api/flows/*', async (route) => {
    const flowId = decodeURIComponent(new URL(route.request().url()).pathname.split('/').at(-1) ?? '');
    const flow = sampleFlows.find(({ id }) => id === flowId);
    if (!flow) {
      await route.fulfill({ status: 404, json: { message: 'not found' } });
      return;
    }
    if (route.request().method() === 'DELETE') {
      await route.fulfill({ status: 204 });
      return;
    }
    if (route.request().method() === 'PUT') {
      await route.fulfill({ json: route.request().postDataJSON() });
      return;
    }
    await route.fulfill({ json: flow });
  });
  await page.route('**/api/flows/*/runtime', async (route) => {
    const flowId = decodeURIComponent(new URL(route.request().url()).pathname.split('/').at(-2) ?? '');
    await route.fulfill({
      json: {
        flowId,
        state: 'stopped',
        updatedAt: '2026-07-14T08:00:00+10:00',
        nodes: {}
      }
    });
  });
});

test('confirms deployment and announces successful and failed runtime updates', async ({ page }) => {
  let deployShouldFail = false;
  await page.route('**/api/flows/climate-control/deploy', async (route) => {
    if (deployShouldFail) {
      await route.fulfill({ status: 503, json: { message: 'startup failed' } });
      return;
    }
    await route.fulfill({
      json: {
        flowId: 'climate-control',
        state: 'running',
        updatedAt: '2026-07-14T08:01:00+10:00',
        nodes: {
          'temperature-average': {
            state: 'running',
            value: '22.4 C',
            updatedAt: '2026-07-14T08:01:00+10:00'
          }
        }
      }
    });
  });

  await page.goto('/flows/climate-control');
  await expect(page.getByRole('status', { name: 'Runtime state: stopped' })).toBeVisible();
  await page.getByRole('button', { name: 'Deploy flow' }).click();
  await expect(page.getByRole('alertdialog', { name: 'Deploy this flow?' })).toBeVisible();
  await page.getByRole('button', { name: 'Deploy now' }).click();

  await expect(page.getByRole('status', { name: 'Runtime state: running' })).toBeVisible();
  await expect(
    page.getByRole('button', { name: /Average temperature, Calculator node, running/ })
  ).toBeVisible();
  await expect(
    page.getByRole('button', { name: /Average temperature, Calculator node, running, 22.4 C/ })
  ).toBeVisible();
  const runtimeNode = page.locator('[data-node-id="temperature-average"]');
  await expect(runtimeNode.locator('.node-status')).toContainText('22.4 C');
  await expect(runtimeNode.locator('.node-marker')).toHaveCount(3);
  await expect(runtimeNode.locator('rect.connector-port')).toHaveCount(2);

  deployShouldFail = true;
  await page.getByRole('button', { name: 'Deploy flow' }).click();
  await page.getByRole('button', { name: 'Deploy now' }).click();
  await expect(page.getByRole('alert')).toContainText('status 503');
  await expect(page.getByRole('status', { name: 'Runtime state: running' })).toBeVisible();
});

test('announces runtime errors and clears stale node values after disconnect', async ({ page }) => {
  let connected = true;
  await page.route('**/api/flows/climate-control/runtime', async (route) => {
    if (!connected) {
      await route.fulfill({ status: 503 });
      return;
    }
    await route.fulfill({
      json: {
        flowId: 'climate-control',
        state: 'error',
        updatedAt: '2026-07-14T08:02:00+10:00',
        nodes: {
          'temperature-average': {
            state: 'error',
            value: 'Sensor unavailable',
            updatedAt: '2026-07-14T08:02:00+10:00'
          }
        }
      }
    });
  });

  await page.goto('/flows/climate-control');
  await expect(page.getByRole('status', { name: 'Runtime state: error' })).toBeVisible();
  await expect(
    page.getByRole('button', {
      name: /Average temperature, Calculator node, error, Sensor unavailable/
    })
  ).toBeVisible();

  connected = false;
  await page.getByRole('button', { name: 'Refresh runtime' }).click();
  await expect(page.getByRole('alert')).toContainText('status 503');
  await expect(page.getByRole('status', { name: 'Runtime state: disconnected' })).toBeVisible();
  await expect(page.getByRole('button', { name: /Sensor unavailable/ })).toHaveCount(0);
});

test('opens the flow library and navigates to a designer', async ({ page }) => {
  await page.goto('/flows');

  await expect(page.getByRole('heading', { name: 'Flows' })).toBeVisible();
  await page.getByRole('link', { name: /Climate control/ }).click();

  await expect(page).toHaveURL(/\/flows\/climate-control$/);
  await expect(page.getByRole('heading', { name: 'Climate control' })).toBeVisible();
  await expect(page.getByRole('group', { name: 'Climate control flow graph' })).toBeVisible();
});

test('supports bypass navigation and modal use with only the keyboard', async ({ page }) => {
  await page.goto('/flows');

  await page.keyboard.press('Tab');
  const skipLink = page.getByRole('link', { name: 'Skip to main content' });
  await expect(skipLink).toBeFocused();
  await page.keyboard.press('Enter');
  await expect(page.locator('#main-content')).toBeFocused();

  await page.goto('/flows/climate-control');
  const deployButton = page.getByRole('button', { name: 'Deploy flow' });
  await deployButton.focus();
  await page.keyboard.press('Enter');

  const dialog = page.getByRole('alertdialog', { name: 'Deploy this flow?' });
  const cancelButton = dialog.getByRole('button', { name: 'Cancel' });
  const confirmButton = dialog.getByRole('button', { name: 'Deploy now' });
  await expect(cancelButton).toBeFocused();

  await page.keyboard.press('Shift+Tab');
  await expect(confirmButton).toBeFocused();
  await page.keyboard.press('Tab');
  await expect(cancelButton).toBeFocused();

  await page.keyboard.press('Escape');
  await expect(dialog).toBeHidden();
  await expect(deployButton).toBeFocused();

  const graph = page.getByRole('group', { name: 'Climate control flow graph' });
  await expect(graph.getByRole('button', { name: /Average temperature/ })).toBeVisible();
  await expect(graph.getByRole('button', { name: /Values, input, number/ })).toBeVisible();
});

test('shows flow-library loading, empty, error, and retry states', async ({ page }) => {
  await page.unroute('**/api/flows');
  let releaseEmpty!: () => void;
  const emptyReady = new Promise<void>((resolve) => {
    releaseEmpty = resolve;
  });
  await page.route('**/api/flows', async (route) => {
    await emptyReady;
    await route.fulfill({ json: [] });
  });

  await page.goto('/flows');
  await expect(page.locator('.request-status')).toHaveText('Loading flows…');
  releaseEmpty();
  await expect(page.getByRole('heading', { name: 'No flows yet' })).toBeVisible();

  await page.unroute('**/api/flows');
  let shouldFail = true;
  await page.route('**/api/flows', async (route) => {
    if (shouldFail) {
      await route.fulfill({ status: 503, json: { message: 'offline' } });
      return;
    }
    await route.fulfill({ json: sampleFlows });
  });
  await page.reload();
  await expect(page.getByRole('alert')).toContainText('offline');
  shouldFail = false;
  await page.getByRole('button', { name: 'Retry' }).click();
  await expect(page.getByRole('link', { name: /Climate control/ })).toBeVisible();
});

test('creates, opens, renames, and deletes a flow through the API', async ({ page }) => {
  await page.unroute('**/api/flows');
  await page.unroute('**/api/flows/*');
  let serverFlows = structuredClone(sampleFlows);
  await page.route('**/api/flows', async (route) => {
    if (route.request().method() === 'POST') {
      const { name } = route.request().postDataJSON() as { name: string };
      const created = {
        id: 'new-automation',
        name,
        description: '',
        status: 'draft' as const,
        updatedAt: '2026-07-13T12:00:00+10:00',
        nodes: [],
        connections: []
      };
      serverFlows.push(created);
      await route.fulfill({ json: created });
      return;
    }
    await route.fulfill({ json: serverFlows });
  });
  await page.route('**/api/flows/*', async (route) => {
    const id = new URL(route.request().url()).pathname.split('/').at(-1);
    const index = serverFlows.findIndex((flow) => flow.id === id);
    if (index < 0) {
      await route.fulfill({ status: 404 });
      return;
    }
    if (route.request().method() === 'PUT') {
      serverFlows[index] = route.request().postDataJSON();
      await route.fulfill({ json: serverFlows[index] });
      return;
    }
    if (route.request().method() === 'DELETE') {
      serverFlows = serverFlows.filter((flow) => flow.id !== id);
      await route.fulfill({ status: 204 });
      return;
    }
    await route.fulfill({ json: serverFlows[index] });
  });

  await page.goto('/flows');
  await page.getByRole('textbox', { name: 'New flow name' }).fill('New automation');
  await page.getByRole('button', { name: 'New flow' }).click();
  await expect(page.getByRole('link', { name: /New automation/ })).toBeVisible();

  await page.getByRole('button', { name: 'Rename' }).last().click();
  await page.getByRole('textbox', { name: 'Rename New automation' }).fill('Renamed automation');
  await page.getByRole('button', { name: 'Save name' }).click();
  await page.getByRole('link', { name: /Renamed automation/ }).click();
  await expect(page.getByRole('heading', { name: 'Renamed automation' })).toBeVisible();
  await expect(page.getByText('0 nodes', { exact: true })).toBeVisible();

  await page.getByRole('link', { name: 'All flows' }).click();
  const renamedCard = page.getByRole('article').filter({ hasText: 'Renamed automation' });
  await renamedCard.getByRole('button', { name: 'Delete' }).click();
  await renamedCard.getByRole('button', { name: 'Confirm delete' }).click();
  await expect(page.getByRole('link', { name: /Renamed automation/ })).toHaveCount(0);
});

test('opens a flow designer directly', async ({ page }) => {
  await page.goto('/flows/climate-control');

  await expect(page.getByRole('heading', { name: 'Climate control' })).toBeVisible();
  await expect(page.getByText('4 nodes', { exact: true })).toBeVisible();
  await expect(page.getByText('2 connections', { exact: true })).toBeVisible();
  await expect(page.locator('[data-connection-id]')).toHaveCount(2);
  await expect(page.getByRole('button', { name: /Average temperature, Calculator node, draft/ })).toBeVisible();
  await expect(page.getByRole('button', { name: /Comfort pulse, Pulse node, draft/ })).toBeVisible();
  await expect(page.getByRole('button', { name: /Manual override, Override node, draft/ })).toBeVisible();
  await expect(page.getByRole('button', { name: /Zone outputs, Split node, draft/ })).toBeVisible();
  await expect(page.getByRole('button', { name: /Values, input, number/ })).toBeVisible();

  const viewport = page.getByLabel(/Scrollable designer viewport/);
  await viewport.focus();
  await expect(viewport).toBeFocused();

  const pageHasVerticalOverflow = await page.evaluate(
    () => document.documentElement.scrollHeight > document.documentElement.clientHeight
  );
  expect(pageHasVerticalOverflow).toBe(false);

  const toolbox = page.getByRole('complementary', { name: 'Function block toolbox' });
  const toolboxScroll = await toolbox.evaluate((element) => {
    element.scrollTop = element.scrollHeight;
    return {
      canScroll: element.scrollHeight > element.clientHeight,
      scrollTop: element.scrollTop,
      windowScrollY: window.scrollY
    };
  });
  expect(toolboxScroll.canScroll).toBe(true);
  expect(toolboxScroll.scrollTop).toBeGreaterThan(0);
  expect(toolboxScroll.windowScrollY).toBe(0);

  const initialWidth = await page.getByRole('group', { name: 'Climate control flow graph' }).evaluate((element) =>
    element.getBoundingClientRect().width
  );
  await page.getByRole('button', { name: 'Zoom in' }).click();
  await expect(page.getByText('125%', { exact: true })).toBeVisible();
  await expect
    .poll(() =>
      page.getByRole('group', { name: 'Climate control flow graph' }).evaluate((element) =>
        element.getBoundingClientRect().width
      )
    )
    .toBeGreaterThan(initialWidth);

  const canReachWholeGraph = await viewport.evaluate(
    (element) => element.scrollWidth >= element.clientWidth && element.scrollHeight >= element.clientHeight
  );
  expect(canReachWholeGraph).toBe(true);
});

test('renders a validated mocked API payload and rejects an invalid one visibly', async ({ page }) => {
  await page.unroute('**/api/flows/*');
  const payload = structuredClone(sampleFlows[0]!);
  payload.nodes[0]!.label = 'Temperature from API';
  (payload.nodes[2] as unknown as Record<string, unknown>).color = '#64a7ff';
  await page.route('**/api/flows/climate-control', (route) => route.fulfill({ json: payload }));

  await page.goto('/flows/climate-control');
  await expect(page.getByRole('button', { name: /Temperature from API, Calculator node/ })).toBeVisible();
  await expect(page.locator('[data-node-id="manual-override"] .node-body')).toHaveAttribute(
    'fill',
    '#65d6ad'
  );

  await page.unroute('**/api/flows/climate-control');
  const invalidPayload = structuredClone(payload);
  invalidPayload.connections[0]!.end.nodeId = 'missing-node';
  await page.route('**/api/flows/climate-control', (route) => route.fulfill({ json: invalidPayload }));
  await page.reload();

  await expect(page.getByRole('alert')).toContainText('invalid flow');
  await expect(page.getByText('Flow not found', { exact: true })).toBeVisible();
  await expect(page.getByRole('group', { name: /flow graph/ })).toHaveCount(0);
});

test('saves an unchanged mocked flow without losing graph data', async ({ page }) => {
  await page.unroute('**/api/flows/*');
  const payload = structuredClone(sampleFlows[0]!);
  let savedPayload: unknown;
  await page.route('**/api/flows/climate-control', async (route) => {
    if (route.request().method() === 'PUT') {
      savedPayload = route.request().postDataJSON();
      await route.fulfill({ json: savedPayload });
      return;
    }
    await route.fulfill({ json: payload });
  });

  await page.goto('/flows/climate-control');
  await expect(page.locator('.request-status')).toBeHidden();
  await page.getByRole('button', { name: 'Save flow' }).click();

  await expect.poll(() => savedPayload).toEqual(payload);
  await expect(page.getByRole('button', { name: 'Save flow' })).toBeEnabled();
});

test('keeps the newest route response during rapid navigation', async ({ page }) => {
  await page.unroute('**/api/flows/*');
  let releaseClimate!: () => void;
  const climateReady = new Promise<void>((resolve) => {
    releaseClimate = resolve;
  });
  await page.route('**/api/flows/*', async (route) => {
    const id = new URL(route.request().url()).pathname.split('/').at(-1);
    if (id === 'climate-control') await climateReady;
    const flow = sampleFlows.find((candidate) => candidate.id === id);
    await route.fulfill({ status: flow ? 200 : 404, json: flow ?? {} });
  });

  await page.goto('/flows/climate-control');
  await expect(page.getByText('Loading latest flow…')).toBeVisible();
  await page.getByRole('link', { name: 'Flows', exact: true }).click();
  await page.getByRole('link', { name: /Garden irrigation/ }).click();
  await expect(page.getByRole('heading', { name: 'Garden irrigation' })).toBeVisible();
  releaseClimate();
  await expect(page.getByRole('heading', { name: 'Garden irrigation' })).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Climate control' })).toHaveCount(0);
});

test('recovers from a failed save without losing edits', async ({ page }) => {
  await page.unroute('**/api/flows/*');
  let persistedPayload = structuredClone(sampleFlows[0]!);
  let failNextSave = true;
  let releaseFailedSave!: () => void;
  const failedSaveReady = new Promise<void>((resolve) => {
    releaseFailedSave = resolve;
  });
  await page.route('**/api/flows/climate-control', async (route) => {
    if (route.request().method() === 'PUT') {
      if (failNextSave) {
        failNextSave = false;
        await failedSaveReady;
        await route.fulfill({ status: 503, json: { message: 'try again' } });
        return;
      }
      persistedPayload = route.request().postDataJSON();
    }
    await route.fulfill({ json: persistedPayload });
  });

  await page.goto('/flows/climate-control');
  await page.getByRole('button', { name: /Average temperature, Calculator node/ }).click();
  await page.getByRole('textbox', { name: 'Node label' }).fill('Retry-safe average');
  await page.getByRole('button', { name: 'Save flow' }).click();

  await expect(page.getByRole('button', { name: 'Saving…' })).toBeDisabled();
  releaseFailedSave();
  await expect(page.getByRole('alert')).toContainText('try again');
  await expect(page.getByRole('button', { name: /Retry-safe average, Calculator node/ })).toBeVisible();
  await expect(page.getByText('Unsaved changes', { exact: true })).toBeVisible();

  await page.getByRole('button', { name: 'Save flow' }).click();
  await expect.poll(() => persistedPayload.nodes[0]?.label).toBe('Retry-safe average');
  await expect(page.getByText('Unsaved changes', { exact: true })).toBeHidden();
  await page.reload();
  await expect(page.getByRole('button', { name: /Retry-safe average, Calculator node/ })).toBeVisible();
});

test('announces deployed node state independently of colour', async ({ page }) => {
  await page.goto('/flows/garden-irrigation');

  await expect(page.getByRole('button', { name: /Watering pulse, Pulse node, deployed/ })).toBeVisible();
});

test('selects and clears a node with pointer and keyboard controls', async ({ page }) => {
  await page.goto('/flows/climate-control');

  const node = page.getByRole('button', { name: /Average temperature, Calculator node/ });
  await node.click();
  await expect(page.getByText('Selected: temperature-average')).toBeVisible();

  await page.locator('[data-canvas-background]').click({ position: { x: 20, y: 20 } });
  await expect(page.getByText('Selected: temperature-average')).toBeHidden();

  await node.focus();
  await page.keyboard.press('Enter');
  await expect(page.getByText('Selected: temperature-average')).toBeVisible();
  await page.keyboard.press('Escape');
  await expect(page.getByText('Selected: temperature-average')).toBeHidden();
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
  expect(await order()).toEqual(['comfort-pulse', 'manual-override', 'zone-split', 'temperature-average']);
  await expect(page.getByRole('button', { name: 'Bring to front' })).toBeDisabled();

  await page.getByRole('button', { name: 'Send backward' }).click();
  expect(await order()).toEqual(['comfort-pulse', 'manual-override', 'temperature-average', 'zone-split']);

  await page.getByRole('button', { name: 'Send to back' }).click();
  expect(await order()).toEqual(['temperature-average', 'comfort-pulse', 'manual-override', 'zone-split']);

  await page.getByRole('button', { name: 'Bring forward' }).click();
  expect(await order()).toEqual(['comfort-pulse', 'temperature-average', 'manual-override', 'zone-split']);
});

test('moves and deletes with the keyboard while safeguarding editable controls', async ({ page }) => {
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

test('highlights compatible connectors, previews a link, and rejects invalid completion', async ({ page }) => {
  await page.goto('/flows/climate-control');

  const source = page.getByRole('button', { name: /Average, output, number/ });
  await source.click();
  await expect(page.getByRole('button', { name: /Automatic, input, number, compatible destination/ })).toBeVisible();
  await expect(page.getByRole('button', { name: /Value, input, number, compatible destination/ })).toHaveCount(0);

  const preview = page.locator('[data-connection-id="connection-preview"] .flow-connection');
  await expect(preview).toBeVisible();
  const initialPath = await preview.getAttribute('d');
  const canvasBox = await page.getByRole('group', { name: 'Climate control flow graph' }).boundingBox();
  expect(canvasBox).not.toBeNull();
  // Dispatch directly to the SVG so the preview assertion is deterministic in
  // both mouse-oriented desktop projects and touch-emulating mobile projects.
  await page.getByRole('group', { name: 'Climate control flow graph' }).dispatchEvent('pointermove', {
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

  await expect(page.locator('[data-connection-id]:not([data-connection-id="connection-preview"])')).toHaveCount(2);
});

test('creates a connection with the keyboard and deletes a selected connection', async ({ page }) => {
  await page.goto('/flows/climate-control');

  const source = page.getByRole('button', { name: /Average, output, number/ });
  const destination = page.getByRole('button', { name: /Automatic, input, number/ });
  await source.focus();
  await page.keyboard.press('Enter');
  await expect(page.locator('[data-connection-id="connection-preview"]')).toBeVisible();
  await destination.focus();
  await page.keyboard.press('Enter');
  await expect(page.locator('[data-connection-id]:not([data-connection-id="connection-preview"])')).toHaveCount(3);

  const connection = page.getByRole('button', { name: 'Connection from temperature-average to comfort-pulse' });
  await connection.click();
  await expect(page.getByText('Selected connection: temperature-to-pulse')).toBeVisible();
  await page.keyboard.press('Delete');

  await expect(connection).toBeHidden();
  await expect(page.locator('[data-connection-id]:not([data-connection-id="connection-preview"])')).toHaveCount(2);
  await expect(page.getByLabel(/Scrollable designer viewport/)).toBeFocused();

  const keyboardConnection = page.getByRole('button', {
    name: 'Connection from temperature-average to manual-override'
  });
  await keyboardConnection.focus();
  await page.keyboard.press('Enter');
  await page.keyboard.press('Delete');
  await expect(keyboardConnection).toBeHidden();
  await expect(page.locator('[data-connection-id]:not([data-connection-id="connection-preview"])')).toHaveCount(1);
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

test('searches the node palette and adds registry-backed nodes', async ({ page }) => {
  await page.goto('/flows/climate-control');

  const search = page.getByRole('searchbox', { name: 'Find a node' });
  await search.fill('timing');
  await expect(page.getByRole('button', { name: 'Add Pulse node' })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Add Calculator node' })).toHaveCount(0);
  await page.getByRole('button', { name: 'Add Pulse node' }).click();

  const pulse = page.getByRole('button', { name: /New Pulse, Pulse node/ });
  await expect(pulse).toHaveAttribute('aria-pressed', 'true');
  await expect(pulse.locator('.node-body')).toHaveAttribute('fill', '#a879d8');
  await expect(page.getByText('5 nodes', { exact: true })).toBeVisible();
  await expect(page.getByRole('button', { name: /Trigger, input, any/ })).toBeVisible();

  await search.fill('routing');
  await page.getByRole('button', { name: 'Add Split node' }).click();
  const split = page.getByRole('button', { name: /New Split, Split node/ });
  await expect(split).toBeVisible();
  await expect(split.locator('.node-body')).toHaveAttribute('fill', '#f5b942');
  await expect(split.locator('rect.connector-port')).toHaveCount(3);
  await expect(page.getByText('6 nodes', { exact: true })).toBeVisible();

  await search.fill('override');
  await expect(page.getByRole('heading', { name: 'override', exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Add Override node' }).click();
  const override = page.getByRole('button', { name: /New Override, Override node/ });
  await expect(override.locator('.node-body')).toHaveAttribute('fill', '#65d6ad');
});

test('drags a legacy function block from the toolbox onto the canvas', async ({ page }) => {
  await page.goto('/flows/climate-control');

  const search = page.getByRole('searchbox', { name: 'Find a node' });
  await search.fill('average');
  const average = page.getByRole('button', { name: 'Add Average node' });
  await expect(average).toHaveAttribute('draggable', 'true');
  const canvas = page.getByRole('group', { name: 'Climate control flow graph' });
  const canvasBox = await canvas.boundingBox();
  expect(canvasBox).not.toBeNull();
  // Native mouse drag synthesis is unavailable in touch-emulating projects and
  // can target a node painted above the SVG background. Dispatch the same HTML
  // drag payload to the canvas at an explicit empty graph coordinate instead.
  const transfer = await page.evaluateHandle(() => new DataTransfer());
  await average.dispatchEvent('dragstart', { dataTransfer: transfer });
  await canvas.dispatchEvent('drop', {
    clientX: canvasBox!.x + 760,
    clientY: canvasBox!.y + 470,
    dataTransfer: transfer
  });

  await expect(page.getByRole('button', { name: /New Average, Average node/ })).toBeVisible();
  await expect(page.getByText('5 nodes', { exact: true })).toBeVisible();
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

test('protects dirty navigation and supports explicit discard', async ({ page }) => {
  await page.goto('/flows/climate-control');
  await expect(page.getByText('Loading latest flow…')).toBeHidden();

  const node = page.getByRole('button', { name: /Average temperature, Calculator node/ });
  await node.focus();
  await page.keyboard.press('Enter');
  await page.keyboard.press('ArrowRight');
  await expect(page.getByText('Unsaved changes', { exact: true })).toBeVisible();

  await page.getByRole('link', { name: 'All flows' }).click();
  await expect(page.getByRole('alertdialog', { name: 'Discard unsaved changes?' })).toBeVisible();
  await expect(page).toHaveURL(/\/flows\/climate-control$/);
  await page.getByRole('button', { name: 'Keep editing' }).click();
  await expect(page.getByRole('alertdialog')).toBeHidden();

  await page.getByRole('link', { name: 'All flows' }).click();
  await page.getByRole('button', { name: 'Discard changes' }).click();
  await expect(page).toHaveURL(/\/flows$/);
  await page.getByRole('link', { name: /Climate control/ }).click();
  await expect(node).toHaveAttribute('transform', 'translate(90 110)');
});

test('shows a useful message for an unknown flow', async ({ page }) => {
  await page.goto('/flows/not-a-flow');

  await expect(page.getByText('Flow not found', { exact: true })).toBeVisible();
  await expect(page.getByRole('heading', { name: 'There is no flow named “not-a-flow”.' })).toBeVisible();
  await page.getByRole('link', { name: 'Return to flows' }).click();
  await expect(page).toHaveURL(/\/flows$/);
});
