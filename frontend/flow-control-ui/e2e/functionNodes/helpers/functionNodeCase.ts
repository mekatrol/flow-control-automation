import { expect, type Page, type TestInfo } from '@playwright/test';

import {
  addNode,
  addVirtualPointNode,
  configureSelectedNode,
  connectNodes,
  createFlow,
  moveNode,
  saveFlow,
  type NodeConfigurationValue
} from './functionNodeDesigner';
import {
  applyInputs,
  expectOutput,
  startSimulation,
  stopSimulation
} from './functionNodeSimulator';
import { getNodeTypeDefinition } from '@/features/flows/nodeTypes';
import type { FlowNodeType } from '@/features/flows/types';
import { test } from './functionNodeTest';

export { test };

export interface FunctionVector {
  inputs: Record<string, boolean | number>;
  expected: boolean | number;
  expectedBeforeAdvance?: boolean | number;
  advanceMs?: number;
  expectedError?: boolean;
}

export interface FunctionNodeCase {
  nodeType: FlowNodeType;
  configuration?: Record<string, NodeConfigurationValue>;
  unconnectedInputs?: string[];
  pauseSimulation?: boolean;
  vectors: FunctionVector[];
  testLabel?: string;
}

export const booleanBinaryCase = (
  nodeType: FunctionNodeCase['nodeType'],
  expected: [boolean, boolean, boolean, boolean]
): FunctionNodeCase => ({
  nodeType,
  vectors: [
    { inputs: { a: false, b: false }, expected: expected[0] },
    { inputs: { a: false, b: true }, expected: expected[1] },
    { inputs: { a: true, b: false }, expected: expected[2] },
    { inputs: { a: true, b: true }, expected: expected[3] }
  ]
});

export const defineFunctionNodeTest = (
  testCase: FunctionNodeCase
): readonly [string, ({ page }: { page: Page }, testInfo: TestInfo) => Promise<void>] => {
  const label = testCase.testLabel ?? getNodeTypeDefinition(testCase.nodeType).label;
  return [
    `${label} evaluates virtual inputs and publishes its virtual output`,
    async ({ page }, testInfo) => {
      testInfo.setTimeout(60_000);
      await runFunctionNodeCase(page, testInfo, testCase);
    }
  ];
};

export const runFunctionNodeCase = async (
  page: Page,
  testInfo: TestInfo,
  testCase: FunctionNodeCase
): Promise<void> => {
  const definition = getNodeTypeDefinition(testCase.nodeType);
  const inputs = definition.connectors.filter(
    ({ id, direction }) => direction === 'input' && !testCase.unconnectedInputs?.includes(id)
  );
  const outputs = definition.connectors.filter(({ direction }) => direction === 'output');
  const output = outputs.find(({ id }) => id !== 'error');
  if (!output) throw new Error(`${definition.label} does not expose an output connector.`);

  const suffix = `${testInfo.workerIndex}-${Date.now().toString(36)}`;
  const flowId = await createFlow(page, `E2E ${definition.label} ${suffix}`);
  const functionNode = await addNode(page, definition.label);
  if (testCase.configuration) await configureSelectedNode(page, testCase.configuration);
  const functionY = inputs.length > 1 ? 144 : 120;
  await moveNode(page, functionNode, { x: 264, y: functionY });

  const pointIds: Record<string, string> = {};
  for (const [index, connector] of inputs.entries()) {
    const numeric = connector.dataType === 'number';
    const pointId = `${testCase.nodeType}-${connector.id}-${suffix}`;
    pointIds[connector.id] = pointId;
    const pointNode = await addVirtualPointNode(
      page,
      numeric ? 'Analog Input' : 'Digital Input',
      pointId
    );
    await moveNode(page, pointNode, { x: 24, y: 48 + index * 120 });
    await connectNodes(
      page,
      { nodeId: pointNode, connector: 'Value' },
      { nodeId: functionNode, connector: connector.label }
    );
  }

  const outputPointIds: Record<string, string> = {};
  for (const [index, connector] of outputs.entries()) {
    const outputPointId = `${testCase.nodeType}-${connector.id}-${suffix}`;
    outputPointIds[connector.id] = outputPointId;
    const outputNode = await addVirtualPointNode(
      page,
      connector.dataType === 'number' ? 'Analog Output' : 'Digital Output',
      outputPointId
    );
    await moveNode(page, outputNode, { x: 504, y: functionY + index * 120 });
    await connectNodes(
      page,
      { nodeId: functionNode, connector: connector.label },
      { nodeId: outputNode, connector: 'Set' }
    );
  }

  await saveFlow(page, flowId);
  const simulation = await startSimulation(page, flowId);
  try {
    if (
      testCase.pauseSimulation ||
      testCase.vectors.some(({ advanceMs }) => advanceMs !== undefined)
    ) {
      const response = await page.request.post(
        `/api/flows/${encodeURIComponent(simulation.flowId)}/simulator-sessions/${encodeURIComponent(simulation.sessionId)}/pause`
      );
      expect(response.ok(), await response.text()).toBeTruthy();
      // The test pauses through the API, so allow the UI's 250 ms session poll
      // to observe that state before applying inputs. Otherwise the UI still
      // believes it is running and automatically resumes after each apply.
      await page.waitForTimeout(300);
    }
    for (const vector of testCase.vectors) {
      let values: Record<string, boolean | number> | undefined;
      if (inputs.length) {
        values = Object.fromEntries(
          Object.entries(vector.inputs).map(([connectorId, value]) => [
            pointIds[connectorId]!,
            value
          ])
        );
        await applyInputs(page, values);
      }
      if (vector.expectedBeforeAdvance !== undefined) {
        await expectOutput(page, outputPointIds[output.id]!, vector.expectedBeforeAdvance);
      }
      if (vector.advanceMs !== undefined) {
        const response = await page.request.post(
          `/api/flows/${encodeURIComponent(simulation.flowId)}/simulator-sessions/${encodeURIComponent(simulation.sessionId)}/advance`,
          { data: { milliseconds: vector.advanceMs, scan: true } }
        );
        expect(response.ok(), await response.text()).toBeTruthy();
        if (values) await applyInputs(page, values);
      }
      await expectOutput(page, outputPointIds[output.id]!, vector.expected);
      if (vector.expectedError !== undefined) {
        await expectOutput(page, outputPointIds.error!, vector.expectedError);
      }
    }
  } finally {
    await stopSimulation(page, simulation);
  }
};
