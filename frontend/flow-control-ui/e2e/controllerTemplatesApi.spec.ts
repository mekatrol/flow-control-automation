import { expect, test } from '@playwright/test';

const environment = (
  globalThis as unknown as {
    process?: { env?: Record<string, string | undefined> };
  }
).process?.env;

test.skip(
  environment?.FLOW_UI_E2E_BACKEND !== 'dotnet',
  'Runs only with the dedicated .NET-backed Playwright command'
);

test('views the default and round-trips a constrained controller template', async ({
  request
}) => {
  const suffix = `${Date.now()}-${Math.random().toString(36).slice(2)}`;
  const id = `compact-${suffix}`;
  const headers = { 'Content-Type': 'application/yaml' };

  const builtIn = await request.get('/api/controller-templates/default');
  await expect(builtIn).toBeOK();
  expect((await builtIn.json()).readOnly).toBe(true);

  const create = await request.post('/api/controller-templates', {
    data: templateYaml(id, `Compact ${suffix}`),
    headers
  });
  expect(create.status()).toBe(201);
  expect(create.headers().etag).toBe('1');

  const yaml = await request.get(`/api/controller-templates/${id}/yaml`);
  await expect(yaml).toBeOK();
  expect(yaml.headers()['content-type']).toContain('application/yaml');
  expect(await yaml.text()).toContain(`id: ${id}`);

  const update = await request.put(`/api/controller-templates/${id}`, {
    data: templateYaml(id, `Updated ${suffix}`),
    headers: { ...headers, 'If-Match': '1' }
  });
  await expect(update).toBeOK();
  expect(update.headers().etag).toBe('2');

  const list = await request.get('/api/controller-templates');
  await expect(list).toBeOK();
  expect(
    (await list.json()).items.some((template: { id: string }) => template.id === id)
  ).toBe(true);

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
  flowFunctions: [and, read-point, write-point]
  executionModes: [interval]
  runtimeFeatures: [bound_points]
limits:
  maxFlows: 8
  maxNodesPerFlow: 64
  maxConnectionsPerFlow: 96
  minimumIntervalMilliseconds: 100
`;
