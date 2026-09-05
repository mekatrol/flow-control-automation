import { expect, test } from './helpers/functionNodeTest';
import { addNode, addVirtualPointNode, configureSelectedNode, connectNodes, createFlow, saveFlow } from './helpers/functionNodeDesigner';
import { applyAnalogInputs, expectAnalogOutput, startSimulation, stopSimulation } from './helpers/functionNodeSimulator';

const analogConstant = async (
  page: Parameters<typeof addNode>[0],
  value: number
): Promise<string> => {
  const id = await addNode(page, 'Analog Constant');
  await configureSelectedNode(page, { Value: value });
  return id;
};

test('Calculator evaluates y = mx + c at several points on the line', async ({ page }, testInfo) => {
  test.setTimeout(60_000);
  const suffix = `${testInfo.workerIndex}-${Date.now().toString(36)}`;
  const flowId = await createFlow(page, `E2E calculator line ${suffix}`);
  const xPoint = `line-x-${suffix}`;
  const yPoint = `line-y-${suffix}`;
  const m = await analogConstant(page, 2.5);
  const x = await addVirtualPointNode(page, 'Analog Input', xPoint);
  const intercept = await analogConstant(page, -4);
  const calculator = await addNode(page, 'Calculator');
  await configureSelectedNode(page, { Formula: 'a * b + c' });
  const output = await addVirtualPointNode(page, 'Analog Output', yPoint);

  await connectNodes(page, { nodeId: m, connector: 'Value' }, { nodeId: calculator, connector: 'A' });
  await connectNodes(page, { nodeId: x, connector: 'Value' }, { nodeId: calculator, connector: 'B' });
  await connectNodes(page, { nodeId: intercept, connector: 'Value' }, { nodeId: calculator, connector: 'C' });
  await connectNodes(page, { nodeId: calculator, connector: 'Output' }, { nodeId: output, connector: 'Set' });
  await saveFlow(page, flowId);
  const savedFlow = await (await page.request.get(`/api/flows/${flowId}`)).json();
  expect(savedFlow.nodes.find((node: { id: string }) => node.id === calculator)?.configuration)
    .toEqual({ formula: 'a * b + c' });

  const simulation = await startSimulation(page, flowId);
  try {
    for (const vector of [{ x: -2, y: -9 }, { x: 0, y: -4 }, { x: 3.2, y: 4 }, { x: 10, y: 21 }]) {
      await applyAnalogInputs(page, { [xPoint]: vector.x });
      await expectAnalogOutput(page, yPoint, vector.y);
    }
    await expect(page.getByRole('alert')).toHaveCount(0);
  } finally { await stopSimulation(page, simulation); }
});

test('Two calculators convert Celsius to Fahrenheit using connected constants', async ({ page }, testInfo) => {
  test.setTimeout(60_000);
  const suffix = `${testInfo.workerIndex}-${Date.now().toString(36)}`;
  const flowId = await createFlow(page, `E2E calculator temperature ${suffix}`);
  const celsiusPoint = `celsius-${suffix}`;
  const fahrenheitPoint = `fahrenheit-${suffix}`;
  const nine = await analogConstant(page, 9);
  const five = await analogConstant(page, 5);
  const ratio = await addNode(page, 'Calculator');
  await configureSelectedNode(page, { Formula: 'a / b' });
  const celsius = await addVirtualPointNode(page, 'Analog Input', celsiusPoint);
  const thirtyTwo = await analogConstant(page, 32);
  const conversion = await addNode(page, 'Calculator');
  await configureSelectedNode(page, { Formula: 'a * b + c' });
  const output = await addVirtualPointNode(page, 'Analog Output', fahrenheitPoint);

  await connectNodes(page, { nodeId: nine, connector: 'Value' }, { nodeId: ratio, connector: 'A' });
  await connectNodes(page, { nodeId: five, connector: 'Value' }, { nodeId: ratio, connector: 'B' });
  await connectNodes(page, { nodeId: celsius, connector: 'Value' }, { nodeId: conversion, connector: 'A' });
  await connectNodes(page, { nodeId: ratio, connector: 'Output' }, { nodeId: conversion, connector: 'B' });
  await connectNodes(page, { nodeId: thirtyTwo, connector: 'Value' }, { nodeId: conversion, connector: 'C' });
  await connectNodes(page, { nodeId: conversion, connector: 'Output' }, { nodeId: output, connector: 'Set' });
  await saveFlow(page, flowId);

  const simulation = await startSimulation(page, flowId);
  try {
    for (const vector of [{ c: -40, f: -40 }, { c: 0, f: 32 }, { c: 25, f: 77 }, { c: 100, f: 212 }]) {
      await applyAnalogInputs(page, { [celsiusPoint]: vector.c });
      await expectAnalogOutput(page, fahrenheitPoint, vector.f);
    }
    await expect(page.getByRole('alert')).toHaveCount(0);
  } finally { await stopSimulation(page, simulation); }
});
