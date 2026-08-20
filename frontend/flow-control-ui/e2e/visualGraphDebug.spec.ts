import { expect, test } from './fixtures/flowTest';

const flow = {
  id: 'visual-debug',
  name: 'Visual debug',
  description: 'Connector inspection.',
  status: 'draft',
  disabled: false,
  updatedAt: '2026-08-14T10:00:00+10:00',
  interface: { schemaVersion: 1, inputs: [], outputs: [] },
  connections: [],
  nodes: [
    {
      id: 'constant',
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
  ]
};
const session = (
  revision: number,
  paused = false,
  breakpoints: unknown[] = []
): Record<string, unknown> => ({
  debugSessionId: 'session',
  flowId: flow.id,
  revision,
  lifecycleState: paused ? 'paused' : 'ready',
  mode: 'manual',
  tickNumber: 1,
  leaseRemainingMilliseconds: 0,
  lastReasonCode: 0,
  lastReason: 'ok',
  lastReasonPath: '',
  affectedOutputPoints: [],
  liveOutputEnabled: false,
  host: 'server',
  executionOrder: ['constant'],
  breakpoints,
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
  snapshot: {
    debugSessionId: 'session',
    flowId: flow.id,
    revision,
    lifecycleState: 'ready',
    mode: 'manual',
    tickNumber: 1,
    sampledAtMs: 1,
    completedAtMs: 2,
    executionDurationUs: 1,
    inputValidity: [],
    nodes: [
      {
        nodeId: 'constant',
        state: 'evaluated',
        quality: 'good',
        typedValue: { dataType: 'boolean', value: true, number: null, quality: 'good' }
      }
    ],
    proposedOutputs: [],
    overrunCount: 0,
    evaluationFailureCount: 0,
    lastReasonCode: 0,
    lastReason: '',
    lastReasonPath: ''
  },
  inspection: paused
    ? {
        instructionPointer: 0,
        isAtCommit: false,
        nodeId: 'constant',
        slots: [{ type: 'boolean', value: false, quality: 'good' }],
        currentState: [],
        stagedNextState: [],
        proposedOutputs: [],
        nodeValues: { constant: { type: 'boolean', value: false, quality: 'good' } }
      }
    : undefined
});

test('shows connector frame values and keyboard-accessible breakpoint positions', async ({
  page
}) => {
  let revision = 1;
  const after = { nodeId: 'constant', position: 'after' };
  await page.route('**/api/flows/visual-debug', (route) => route.fulfill({ json: flow }));
  await page.route('**/api/flows/visual-debug/debug-sessions', async (route) => {
    revision = (route.request().postDataJSON() as { source: { revision: number } }).source.revision;
    await route.fulfill({ status: 201, json: session(revision) });
  });
  await page.route('**/api/flows/visual-debug/debug-sessions/session/breakpoints', (route) =>
    route.fulfill({ json: session(revision, false, [after]) })
  );
  await page.route('**/api/flows/visual-debug/debug-sessions/session/step-instruction', (route) =>
    route.fulfill({ json: session(revision, true, [after]) })
  );

  await page.goto('/flows/visual-debug');
  await page.getByRole('link', { name: 'Debug' }).click();
  await page.getByRole('button', { name: 'Load' }).click();
  const constantNode = page.getByRole('button', { name: /Enabled, Digital Constant node/ });
  await constantNode.focus();
  await page.keyboard.press('Enter');
  await page.getByRole('button', { name: 'Breakpoint after' }).click();
  await expect(page.getByLabel('Execution and breakpoint summary')).toContainText('after constant');
  await page.getByRole('button', { name: 'Step instruction' }).click();

  // Expected outcome: The connector exposes the uncommitted frame value and quality without relying on colour.
  // Acceptance criteria: Visible text contains false, good, and paused-frame, proving the paused value is distinct from the committed snapshot.
  await expect(page.locator('[data-node-id="constant"] .connector-value')).toContainText(
    'false · good · paused-frame'
  );
  // Expected outcome: The after-node breakpoint remains textually identifiable on the graph and in the summary.
  // Acceptance criteria: Marker A and `after constant` are both visible, covering graphical and non-graphical users.
  await expect(page.locator('[data-node-id="constant"] .breakpoint-marker')).toHaveText('A');
});
