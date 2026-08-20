import { expect, test } from './fixtures/flowTest';

const flow = {
  id: 'simulator-io',
  name: 'Simulator I/O',
  description: 'Typed interface simulation.',
  status: 'draft',
  disabled: false,
  updatedAt: '2026-08-14T10:00:00+10:00',
  connections: [],
  interface: {
    schemaVersion: 1,
    inputs: [
      {
        id: 'temperature',
        name: 'Temperature',
        dataType: 'number',
        units: '°C',
        defaultValue: 12.5,
        required: true
      }
    ],
    outputs: [{ id: 'result', name: 'Result', dataType: 'number', units: '°C' }]
  },
  nodes: [
    {
      id: 'input',
      kind: 'flowInput',
      label: 'Temperature',
      x: 100,
      y: 100,
      zOrder: 0,
      configuration: { interfaceId: 'temperature' },
      connectors: [
        {
          id: 'value',
          label: 'Temperature',
          direction: 'output',
          dataType: 'number',
          side: 'right'
        }
      ]
    }
  ]
};
const value = (number: number): Record<string, unknown> => ({
  type: 'number',
  boolean: false,
  number,
  quality: 'good'
});
const session = (number: number, revision: number): Record<string, unknown> => ({
  sessionId: 'session',
  flowId: flow.id,
  sourceRevision: revision,
  sourceDigest: 'digest',
  lifecycleState: 'ready',
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
  io: {
    emulatorId: 'io',
    flowId: flow.id,
    controllerTemplateId: 'server',
    lifecycleState: 'ready',
    virtualTimeMilliseconds: 0,
    scanNumber: 1,
    activeFault: null,
    inputs: [{ pointId: 'temperature', isInterface: true, typedValue: value(number) }],
    outputHistory: [
      {
        scanNumber: 1,
        timestampMilliseconds: 0,
        outputId: 'result',
        proposedValue: value(number),
        effectiveValue: value(number),
        quality: 'good',
        units: '°C',
        lastChangeScan: 1,
        isInterface: true,
        arbitrationOwner: 'emulator',
        priority: 16
      }
    ]
  }
});

test('applies numeric interface inputs and presents committed shadow output metadata', async ({
  page
}) => {
  let revision = 1;
  await page.route('**/api/flows/simulator-io', (route) => route.fulfill({ json: flow }));
  await page.route('**/api/flows/simulator-io/simulator-sessions', async (route) => {
    revision = (route.request().postDataJSON() as { source: { revision: number } }).source.revision;
    await route.fulfill({ status: 201, json: session(12.5, revision) });
  });
  await page.route(
    '**/api/flows/simulator-io/simulator-sessions/session/apply-and-step',
    async (route) => {
      await route.fulfill({ json: session(21.5, revision) });
    }
  );

  await page.goto('/flows/simulator-io');
  await page.getByRole('link', { name: 'Simulate' }).click();
  await page.getByRole('button', { name: 'Start simulation' }).click();
  await page.getByRole('spinbutton', { name: 'Value' }).fill('21.5');
  const applyRequest = page.waitForRequest(
    (request) =>
      new URL(request.url()).pathname ===
      '/api/flows/simulator-io/simulator-sessions/session/apply-and-step'
  );
  await page.getByRole('button', { name: 'Apply inputs and run one scan' }).click();
  const applied = (await applyRequest).postDataJSON();

  // Expected outcome: The request uses the stable interface ID and a typed finite number.
  // Acceptance criteria: The payload contains `temperature` and 21.5, proving the UI did not submit a label or Boolean coercion.
  expect(applied).toMatchObject({
    inputs: [{ inputId: 'temperature', typedValue: { type: 'number', number: 21.5 } }]
  });
  await expect(page.getByRole('region', { name: 'Latest outputs' })).toContainText('21.5 °C');
});
