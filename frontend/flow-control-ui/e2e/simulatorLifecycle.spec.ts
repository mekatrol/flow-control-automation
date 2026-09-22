import { expect, test } from './fixtures/flowTest';

const flow = {
  id: 'simulator-lifecycle',
  revision: 1,
  name: 'Simulator lifecycle',
  description: 'Draft simulation.',
  status: 'draft',
  disabled: false,
  updatedAt: '2026-08-14T10:00:00+10:00',
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
        { id: 'value', label: 'Value', direction: 'output', dataType: 'boolean', side: 'right' }
      ],
      configuration: { value: true }
    }
  ],
  connections: []
};
const session = (state: string, revision: number, tick = 0): Record<string, unknown> => ({
  id: 'simulator-session',
  flowId: flow.id,
  revision,
  mode: 'simulator',
  lifecycle: state,
  leaseRemainingMilliseconds: 900000,
  breakpoints: [],
  capabilities: {
    canRun: true,
    canPause: true,
    canStop: true,
    canRestart: true,
    canStepTick: true,
    canStepNode: true,
    canStepInstruction: true,
    canUseBreakpoints: true,
    canRunTo: true,
    canEditInputs: true,
    canAdvanceVirtualTime: true,
    canInjectFaults: true,
    canResetIo: true,
    canEnableLiveOutputs: false,
    locksFlowEditing: false
  },
  presentation: {
    modeLabel: 'Simulator',
    hostLabel: 'Server',
    isSimulated: true,
    usesPhysicalIo: false
  },
  snapshot: tick
    ? {
        debugSessionId: 'simulator-session',
        flowId: flow.id,
        revision,
        lifecycleState: state,
        mode: 'manual',
        tickNumber: tick,
        sampledAtMs: 1,
        completedAtMs: 2,
        executionDurationUs: 1,
        inputValidity: [],
        nodes: [],
        proposedOutputs: [],
        overrunCount: 0,
        evaluationFailureCount: 0,
        lastReasonCode: 0,
        lastReason: '',
        lastReasonPath: ''
      }
    : undefined
});

test('starts and stops a draft simulation with keyboard-operable controls', async ({ page }) => {
  let starts = 0;
  let revision = 1;
  await page.route('**/api/flows/simulator-lifecycle', (route) => route.fulfill({ json: flow }));
  await page.route('**/api/flows/simulator-lifecycle/execution-contexts', async (route) => {
    starts += 1;
    revision = (route.request().postDataJSON() as { expectedRevision: number }).expectedRevision;
    await route.fulfill({ status: 201, json: session('ready', revision) });
  });
  await page.route(
    '**/api/execution-contexts/simulator-session/run',
    (route) => route.fulfill({ json: session('running', revision, 1) })
  );
  await page.route(
    '**/api/execution-contexts/simulator-session/stop',
    (route) => route.fulfill({ json: session('stopped', revision, 1) })
  );

  await page.goto('/flows/simulator-lifecycle');
  await page.getByRole('link', { name: 'Simulate' }).click();
  await expect(page).toHaveURL(/\/flows\/simulator-lifecycle\/simulator$/);
  await page.reload();
  await expect(page.getByRole('link', { name: 'Simulate' })).toHaveAttribute(
    'aria-current',
    'page'
  );
  await page.getByRole('button', { name: 'Create context' }).focus();
  await page.keyboard.press('Enter');
  await page.getByRole('button', { name: 'Run', exact: true }).click();
  await expect(
    page.getByRole('status', { name: undefined }).filter({ hasText: 'running' })
  ).toBeVisible();
  expect(starts).toBe(1);
  await page.getByRole('button', { name: 'Stop', exact: true }).focus();
  await page.keyboard.press('Enter');
  await expect(
    page.getByLabel('Flow debugging').getByRole('status').filter({ hasText: 'stopped' })
  ).toBeVisible();
  await page.getByRole('link', { name: 'All flows' }).click();
  await expect(page).toHaveURL(/\/flows$/);
  await page.close({ runBeforeUnload: false });
});
