import { expect, test } from './fixtures/flowTest';

const flow = {
  id: 'simulator-lifecycle',
  name: 'Simulator lifecycle',
  description: 'Draft simulation.',
  status: 'draft',
  disabled: false,
  updatedAt: '2026-08-14T10:00:00+10:00',
  interface: { schemaVersion: 1, inputs: [], outputs: [] },
  nodes: [
    {
      id: 'constant-1',
      kind: 'digitalConstant',
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
  sessionId: 'simulator-session',
  flowId: flow.id,
  sourceRevision: revision,
  sourceDigest: 'sha256-draft',
  lifecycleState: state,
  leaseRemainingMilliseconds: 900000,
  breakpoints: [],
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
  await page.route('**/api/flows/simulator-lifecycle/simulator-sessions', async (route) => {
    starts += 1;
    revision = (route.request().postDataJSON() as { source: { revision: number } }).source.revision;
    await route.fulfill({ status: 201, json: session('ready', revision) });
  });
  await page.route(
    '**/api/flows/simulator-lifecycle/simulator-sessions/simulator-session/run',
    (route) => route.fulfill({ json: session('running', revision, 1) })
  );
  await page.route(
    '**/api/flows/simulator-lifecycle/simulator-sessions/simulator-session',
    (route) => route.fulfill({ status: 204 })
  );

  await page.goto('/flows/simulator-lifecycle');
  await page.getByRole('link', { name: 'Simulate' }).click();
  await expect(page).toHaveURL(/\/flows\/simulator-lifecycle\/simulator$/);
  await page.reload();
  await expect(page.getByRole('link', { name: 'Simulate' })).toHaveAttribute(
    'aria-current',
    'page'
  );
  await page.getByRole('button', { name: 'Start simulation' }).focus();
  await page.keyboard.press('Enter');
  await expect(
    page.getByRole('status', { name: undefined }).filter({ hasText: 'running' })
  ).toBeVisible();
  expect(starts).toBe(1);
  await page.getByRole('button', { name: 'Stop simulation' }).focus();
  await page.keyboard.press('Enter');
  await expect(
    page.getByLabel('Simulation controls').getByRole('status').filter({ hasText: 'stopped' })
  ).toBeVisible();
  await page.getByRole('link', { name: 'All flows' }).click();
  await expect(page).toHaveURL(/\/flows$/);
  await page.close({ runBeforeUnload: false });
});
