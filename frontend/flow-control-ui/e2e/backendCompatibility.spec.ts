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
 * Purpose: Protects the behavioral contract that the frontend proxy reaches the .NET backend compatibility surface.
 * Description: Exercises the frontend proxy reaches the .NET backend compatibility surface from its arranged starting state and
 * verifies the observable results required by the scenario.
 */
test('the frontend proxy reaches the .NET backend compatibility surface', async ({ request }) => {
  const suffix = `${Date.now()}-${Math.random().toString(36).slice(2)}`;
  const flowName = `Compatibility ${suffix}`;
  const flowId = flowName.toLowerCase().replaceAll(/[^a-z0-9]+/g, '-');
  const credentialId = `compatibility-${suffix}`.toLowerCase().replaceAll(/[^a-z0-9]+/g, '-');
  const sourceId = `source-${suffix}`.toLowerCase().replaceAll(/[^a-z0-9]+/g, '-');

  const health = await request.get('/api/health');

  // Expected outcome: The HTTP operation succeeds.
  // Acceptance criteria: the response must have a successful HTTP status, because this condition proves that
  // the frontend proxy reaches the .NET backend compatibility surface.
  await expect(health).toBeOK();

  // Expected outcome: `await health.json()` matches the required structure.
  // Acceptance criteria: `await health.json()` must equal `{ status: 'ok' }`, because this condition proves that
  // the frontend proxy reaches the .NET backend compatibility surface.
  expect(await health.json()).toEqual({ status: 'ok' });

  const createdFlowResponse = await request.post('/api/flows', {
    data: { name: flowName }
  });

  // Expected outcome: `createdFlowResponse.status()` has the required value.
  // Acceptance criteria: `createdFlowResponse.status()` must be `201`, because this condition proves that
  // the frontend proxy reaches the .NET backend compatibility surface.
  expect(createdFlowResponse.status()).toBe(201);
  const flow = await createdFlowResponse.json();

  // Expected outcome: `flow.id` has the required value.
  // Acceptance criteria: `flow.id` must be `flowId`, because this condition proves that
  // the frontend proxy reaches the .NET backend compatibility surface.
  expect(flow.id).toBe(flowId);

  const savedFlowResponse = await request.put(`/api/flows/${flowId}`, {
    data: { ...flow, description: 'Verified through the frontend proxy' }
  });

  // Expected outcome: The HTTP operation succeeds.
  // Acceptance criteria: the response must have a successful HTTP status, because this condition proves that
  // the frontend proxy reaches the .NET backend compatibility surface.
  await expect(savedFlowResponse).toBeOK();
  const deployment = await request.post(`/api/flows/${flowId}/deploy`);

  // Expected outcome: The HTTP operation succeeds.
  // Acceptance criteria: the response must have a successful HTTP status, because this condition proves that
  // the frontend proxy reaches the .NET backend compatibility surface.
  await expect(deployment).toBeOK();

  // Expected outcome: `(await deployment.json()` has the required value.
  // Acceptance criteria: `(await deployment.json()` must be `'running'`, because this condition proves that
  // the frontend proxy reaches the .NET backend compatibility surface.
  expect((await deployment.json()).state).toBe('running');

  const credentialResponse = await request.post('/api/credentials', {
    data: {
      id: credentialId,
      name: `Compatibility credential ${suffix}`,
      kind: 'token',
      token: 'playwright-only-secret'
    }
  });

  // Expected outcome: `credentialResponse.status()` has the required value.
  // Acceptance criteria: `credentialResponse.status()` must be `201`, because this condition proves that
  // the frontend proxy reaches the .NET backend compatibility surface.
  expect(credentialResponse.status()).toBe(201);
  const credentialMetadata = await credentialResponse.json();

  // Expected outcome: `credentialMetadata` omits the protected property.
  // Acceptance criteria: `credentialMetadata` must not contain property `'token'`, because this condition proves that
  // the frontend proxy reaches the .NET backend compatibility surface.
  expect(credentialMetadata).not.toHaveProperty('token');

  // Expected outcome: `credentialMetadata` omits the protected property.
  // Acceptance criteria: `credentialMetadata` must not contain property `'password'`, because this condition proves that
  // the frontend proxy reaches the .NET backend compatibility surface.
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

  // Expected outcome: `sourceResponse.status()` has the required value.
  // Acceptance criteria: `sourceResponse.status()` must be `201`, because this condition proves that
  // the frontend proxy reaches the .NET backend compatibility surface.
  expect(sourceResponse.status()).toBe(201);
  const sources = await request.get('/api/point-sources?page=1&pageSize=50');

  // Expected outcome: The HTTP operation succeeds.
  // Acceptance criteria: the response must have a successful HTTP status, because this condition proves that
  // the frontend proxy reaches the .NET backend compatibility surface.
  await expect(sources).toBeOK();

  // Expected outcome: `(await sources.json()` has the required value.
  // Acceptance criteria: `(await sources.json()` must be `true`, because this condition proves that
  // the frontend proxy reaches the .NET backend compatibility surface.
  expect((await sources.json()).items.some((source: { id: string }) => source.id === sourceId)).toBe(
    true
  );
});
