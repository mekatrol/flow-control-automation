import { expect, test } from './fixtures/flowTest';

const flow = {
  id: 'portable-debug',
  revision: 1,
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
  id: 'server-session',
  flowId: flow.id,
  revision,
  lifecycle: state,
  mode: 'debugger',
  leaseRemainingMilliseconds: 0,
  capabilities: {
    canRun: true, canPause: true, canStop: true, canRestart: true,
    canStepTick: true, canStepNode: true, canStepInstruction: true,
    canUseBreakpoints: true, canRunTo: true, canEditInputs: false,
    canAdvanceVirtualTime: false, canInjectFaults: false, canResetIo: false,
    canEnableLiveOutputs: false, locksFlowEditing: true
  },
  presentation: { modeLabel: 'Debugger', hostLabel: 'Server', isSimulated: false, usesPhysicalIo: false },
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
  await page.route('**/api/flows/portable-debug/execution-contexts', async (route) => {
    const body = route.request().postDataJSON() as { targetId: string; expectedRevision: number };
    expect(body.targetId).toBe('server');
    revision = body.expectedRevision;
    await route.fulfill({ status: 201, json: session(revision) });
  });
  await page.route(
    '**/api/execution-contexts/server-session/step-instruction',
    (route) => route.fulfill({ json: session(revision, 'paused') })
  );
  await page.route('**/api/execution-contexts/server-session/stop', (route) =>
    route.fulfill({ json: session(revision, 'stopped') })
  );

  await page.goto('/flows/portable-debug');
  await page.getByRole('link', { name: 'Debug' }).click();
  await expect(page).toHaveURL(/\/flows\/portable-debug\/debugger$/);
  await page.reload();
  await expect(page.getByRole('link', { name: 'Debug' })).toHaveAttribute('aria-current', 'page');
  await expect(page.getByLabel('Debug target')).toHaveValue('server');
  await page.getByRole('button', { name: 'Create context' }).click();
  await page.getByRole('button', { name: 'Step instruction' }).click();

  await expect(page.getByLabel('Paused execution frame')).toContainText('Node constant-1');
  await expect(page.locator('[data-node-id="constant-1"]')).toHaveClass(/current/);
  await page.getByRole('button', { name: 'Stop' }).click();
});
