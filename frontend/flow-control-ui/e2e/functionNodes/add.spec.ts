import { expect, test } from './helpers/functionNodeTest';

import {
  addNode,
  addVirtualPointNode,
  connectNodes,
  createFlow,
  moveNode,
  saveFlow
} from './helpers/functionNodeDesigner';
import {
  applyAnalogInputs,
  expectAnalogOutput,
  startSimulation,
  stopSimulation
} from './helpers/functionNodeSimulator';

test('Add sums virtual analog inputs and publishes the virtual output', async ({
  page
}, testInfo) => {
  test.setTimeout(60_000);
  const suffix = `${testInfo.workerIndex}-${Date.now().toString(36)}`;
  const flowId = await createFlow(page, `E2E Add ${suffix}`);
  const inputA = `add-a-${suffix}`;
  const inputB = `add-b-${suffix}`;
  const output = `add-result-${suffix}`;

  const inputANode = await addVirtualPointNode(page, 'Analog Input', inputA);
  await moveNode(page, inputANode, { x: 24, y: 72 });
  const inputBNode = await addVirtualPointNode(page, 'Analog Input', inputB);
  await moveNode(page, inputBNode, { x: 24, y: 216 });
  const addNodeId = await addNode(page, 'Add');
  await moveNode(page, addNodeId, { x: 264, y: 144 });
  const outputNode = await addVirtualPointNode(page, 'Analog Output', output);
  await moveNode(page, outputNode, { x: 480, y: 144 });

  await connectNodes(
    page,
    { nodeId: inputANode, connector: 'Value' },
    { nodeId: addNodeId, connector: 'A' }
  );
  await connectNodes(
    page,
    { nodeId: inputBNode, connector: 'Value' },
    { nodeId: addNodeId, connector: 'B' }
  );
  await connectNodes(
    page,
    { nodeId: addNodeId, connector: 'Value' },
    { nodeId: outputNode, connector: 'Input' }
  );

  await saveFlow(page, flowId);
  const simulation = await startSimulation(page, flowId);
  try {
    for (const vector of [
      { a: 2, b: 3, expected: 5 },
      { a: 0, b: 0, expected: 0 },
      { a: -4.5, b: 1.25, expected: -3.25 }
    ]) {
      await test.step(`${vector.a} + ${vector.b} = ${vector.expected}`, async () => {
        await applyAnalogInputs(page, { [inputA]: vector.a, [inputB]: vector.b });
        await expectAnalogOutput(page, output, vector.expected);
      });
    }

    await expect(page.getByRole('alert')).toHaveCount(0);
  } finally {
    await stopSimulation(page, simulation);
  }
});
