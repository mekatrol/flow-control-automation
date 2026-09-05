import { expect, test } from './fixtures/flowTest';

const flow = {
  id: 'portable-debug',
  name: 'Portable debug',
  description: 'Runs entirely on the server VM.',
  status: 'draft',
  disabled: false,
  updatedAt: '2026-08-13T10:00:00+10:00',
  interface: { schemaVersion: 1, inputs: [], outputs: [] },
  nodes: [
    {
      id: 'constant-1',
      nodeType: 'digitalConstant',
      label: 'Enabled',
      x: 100,
      y: 100,
      zOrder: 0,
      connectors: [
        { id: 'output', label: 'Value', direction: 'output', dataType: 'boolean', side: 'right' }
      ],
      configuration: { value: true }
    }
  ],
  connections: []
};

const session = (revision: number, state = 'ready'): Record<string, unknown> => ({
  debugSessionId: 'server-session',
  flowId: flow.id,
  revision,
  lifecycleState: state,
  mode: 'manual',
  tickNumber: 0,
  leaseRemainingMilliseconds: 0,
  lastReasonCode: 0,
  lastReason: 'ok',
  lastReasonPath: '',
  affectedOutputPoints: [],
  liveOutputEnabled: false,
  host: 'server',
  capabilities: {
    stepTick: true,
    stepNode: true,
    stepInstruction: true,
    continue: true,
    pause: true,
    runTo: true,
    maximumBreakpoints: 32,
    maximumInspectableSlots: 256
  },
  breakpoints: [],
  inspection:
    state === 'paused'
      ? {
          instructionPointer: 1,
          isAtCommit: true,
          nodeId: 'constant-1',
          slots: [{ type: 'boolean', value: true }],
          currentState: [],
          stagedNextState: [],
          proposedOutputs: []
        }
      : undefined
});

test('loads and steps a server debug session without a controller', async ({ page }) => {
  let revision = 1;
  await page.route('**/api/flows/portable-debug', (route) => route.fulfill({ json: flow }));
  await page.route('**/api/flows/portable-debug/debug-sessions', async (route) => {
    const body = route.request().postDataJSON() as { host: string; source: { revision: number } };
    expect(body.host).toBe('server');
    revision = body.source.revision;
    await route.fulfill({ status: 201, json: session(revision) });
  });
  await page.route(
    '**/api/flows/portable-debug/debug-sessions/server-session/step-instruction',
    (route) => route.fulfill({ json: session(revision, 'paused') })
  );
  await page.route('**/api/flows/portable-debug/debug-sessions/server-session/stop', (route) =>
    route.fulfill({ status: 204 })
  );

  await page.goto('/flows/portable-debug');
  await page.getByRole('link', { name: 'Debug' }).click();
  await expect(page).toHaveURL(/\/flows\/portable-debug\/debugger$/);
  await page.reload();
  await expect(page.getByRole('link', { name: 'Debug' })).toHaveAttribute('aria-current', 'page');
  await expect(page.getByLabel('Debug target')).toHaveValue('server');
  await page.getByRole('button', { name: 'Load' }).click();
  await page.getByRole('button', { name: 'Step instruction' }).click();

  await expect(page.getByLabel('Paused execution frame')).toContainText('Node constant-1');
  await expect(page.locator('[data-node-id="constant-1"]')).toHaveClass(/current/);
  await page.getByRole('button', { name: 'Stop' }).click();
});
