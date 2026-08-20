import { expect, test } from '@playwright/test';

const environment = (
  globalThis as unknown as {
    process?: { env?: Record<string, string | undefined> };
  }
).process?.env;

/**
 * Purpose: Protects the behavioral contract that the declared test scenario.
 * Description: Exercises the declared test scenario from its arranged starting state and
 * verifies the observable results required by the scenario.
 */
test.skip(
  environment?.FLOW_UI_E2E_BACKEND !== 'dotnet',
  'Runs only with the dedicated .NET-backed Playwright command'
);

/**
 * Purpose: Protects the behavioral contract that views the default and round-trips a constrained controller template.
 * Description: Exercises views the default and round-trips a constrained controller template from its arranged starting state and
 * verifies the observable results required by the scenario.
 */
test('views the default and round-trips a constrained controller template', async ({
  request
}) => {
  const suffix = `${Date.now()}-${Math.random().toString(36).slice(2)}`;
  const id = `compact-${suffix}`;
  const headers = { 'Content-Type': 'application/yaml' };

  const builtIn = await request.get('/api/controller-templates/default');

  // Expected outcome: The HTTP operation succeeds.
  // Acceptance criteria: the response must have a successful HTTP status, because this condition proves that
  // views the default and round-trips a constrained controller template.
  await expect(builtIn).toBeOK();

  // Expected outcome: `(await builtIn.json()` has the required value.
  // Acceptance criteria: `(await builtIn.json()` must be `true`, because this condition proves that
  // views the default and round-trips a constrained controller template.
  expect((await builtIn.json()).readOnly).toBe(true);

  const create = await request.post('/api/controller-templates', {
    data: templateYaml(id, `Compact ${suffix}`),
    headers
  });

  // Expected outcome: `create.status()` has the required value.
  // Acceptance criteria: `create.status()` must be `201`, because this condition proves that
  // views the default and round-trips a constrained controller template.
  expect(create.status()).toBe(201);

  // Expected outcome: `create.headers(` has the required value.
  // Acceptance criteria: `create.headers(` must be `'1'`, because this condition proves that
  // views the default and round-trips a constrained controller template.
  expect(create.headers().etag).toBe('1');

  const yaml = await request.get(`/api/controller-templates/${id}/yaml`);

  // Expected outcome: The HTTP operation succeeds.
  // Acceptance criteria: the response must have a successful HTTP status, because this condition proves that
  // views the default and round-trips a constrained controller template.
  await expect(yaml).toBeOK();

  // Expected outcome: `yaml.headers()['content-type']` includes the required value.
  // Acceptance criteria: `yaml.headers()['content-type']` must contain `'application/yaml'`, because this condition proves that
  // views the default and round-trips a constrained controller template.
  expect(yaml.headers()['content-type']).toContain('application/yaml');

  // Expected outcome: `await yaml.text()` includes the required value.
  // Acceptance criteria: `await yaml.text()` must contain ``id: ${id}``, because this condition proves that
  // views the default and round-trips a constrained controller template.
  expect(await yaml.text()).toContain(`id: ${id}`);

  const update = await request.put(`/api/controller-templates/${id}`, {
    data: templateYaml(id, `Updated ${suffix}`),
    headers: { ...headers, 'If-Match': '1' }
  });

  // Expected outcome: The HTTP operation succeeds.
  // Acceptance criteria: the response must have a successful HTTP status, because this condition proves that
  // views the default and round-trips a constrained controller template.
  await expect(update).toBeOK();

  // Expected outcome: `update.headers(` has the required value.
  // Acceptance criteria: `update.headers(` must be `'2'`, because this condition proves that
  // views the default and round-trips a constrained controller template.
  expect(update.headers().etag).toBe('2');

  const list = await request.get('/api/controller-templates');

  // Expected outcome: The HTTP operation succeeds.
  // Acceptance criteria: the response must have a successful HTTP status, because this condition proves that
  // views the default and round-trips a constrained controller template.
  await expect(list).toBeOK();

  // Expected outcome: `(await list.json()` has the required value.
  // Acceptance criteria: `(await list.json()` must be `true`, because this condition proves that
  // views the default and round-trips a constrained controller template.
  expect(
    (await list.json()).items.some((template: { id: string }) => template.id === id)
  ).toBe(true);

  // Expected outcome: The HTTP operation succeeds.
  // Acceptance criteria: the response must have a successful HTTP status, because this condition proves that
  // views the default and round-trips a constrained controller template.
  await expect(
    await request.delete(`/api/controller-templates/${id}?revision=2`)
  ).toBeOK();
});

const templateYaml = (id: string, name: string): string => `schemaVersion: 1
id: ${id}
name: ${name}
readOnly: false
capabilities:
  pointTypes: [digital]
  pointDirections: [input, output]
  pointFeatures: [read, command]
  connectorDataTypes: [boolean]
  flowFunctions: [and, readPoint, writePoint]
  executionModes: [interval]
  runtimeFeatures: [boundPoints]
limits:
  maxFlows: 8
  maxNodesPerFlow: 64
  maxConnectionsPerFlow: 96
  minimumIntervalMilliseconds: 100
`;
