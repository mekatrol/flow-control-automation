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

test('the frontend proxy reaches the .NET backend compatibility surface', async ({ request }) => {
  const suffix = `${Date.now()}-${Math.random().toString(36).slice(2)}`;
  const flowName = `Compatibility ${suffix}`;
  const flowId = flowName.toLowerCase().replaceAll(/[^a-z0-9]+/g, '-');
  const credentialId = `compatibility-${suffix}`.toLowerCase().replaceAll(/[^a-z0-9]+/g, '-');
  const sourceId = `source-${suffix}`.toLowerCase().replaceAll(/[^a-z0-9]+/g, '-');

  const health = await request.get('/api/health');
  await expect(health).toBeOK();
  expect(await health.json()).toEqual({ status: 'ok' });

  const createdFlowResponse = await request.post('/api/flows', {
    data: { name: flowName }
  });
  expect(createdFlowResponse.status()).toBe(201);
  const flow = await createdFlowResponse.json();
  expect(flow.id).toBe(flowId);

  const savedFlowResponse = await request.put(`/api/flows/${flowId}`, {
    data: { ...flow, description: 'Verified through the frontend proxy' }
  });
  await expect(savedFlowResponse).toBeOK();
  const deployment = await request.post(`/api/flows/${flowId}/deploy`);
  await expect(deployment).toBeOK();
  expect((await deployment.json()).state).toBe('running');

  const credentialResponse = await request.post('/api/credentials', {
    data: {
      id: credentialId,
      name: `Compatibility credential ${suffix}`,
      kind: 'token',
      token: 'playwright-only-secret'
    }
  });
  expect(credentialResponse.status()).toBe(201);
  const credentialMetadata = await credentialResponse.json();
  expect(credentialMetadata).not.toHaveProperty('token');
  expect(credentialMetadata).not.toHaveProperty('password');

  const sourceYaml = `schemaVersion: 1
sources:
  - id: ${sourceId}
    name: Compatibility source ${suffix}
    enabled: true
    kind: http_json
    connection:
      baseUrl: https://example.test
      allowedReadMethods: [GET]
      maximumResponseBytes: 1024
    credentialRef: secret://${credentialId}
    tls:
      verifyServerCertificate: true
    timeouts:
      connectMilliseconds: 100
      requestMilliseconds: 100
`;
  const sourceResponse = await request.post('/api/point-sources', {
    data: sourceYaml,
    headers: { 'Content-Type': 'application/yaml' }
  });
  expect(sourceResponse.status()).toBe(201);
  const sources = await request.get('/api/point-sources?page=1&pageSize=50');
  await expect(sources).toBeOK();
  expect((await sources.json()).items.some((source: { id: string }) => source.id === sourceId)).toBe(
    true
  );
});
