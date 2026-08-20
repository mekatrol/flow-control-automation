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
 * Purpose: Protects the behavioral contract that round-trips point and group YAML through the server-backed API.
 * Description: Exercises round-trips point and group YAML through the server-backed API from its arranged starting state and
 * verifies the observable results required by the scenario.
 */
test('round-trips point and group YAML through the server-backed API', async ({ request }) => {
  const suffix = `${Date.now()}-${Math.random().toString(36).slice(2)}`;
  const sourceId = `points-source-${suffix}`;
  const groupId = `points-group-${suffix}`;
  const memberId = `member-${suffix}`;
  const standaloneId = `standalone-${suffix}`;
  const yamlHeaders = { 'Content-Type': 'application/yaml' };

  const source = await request.post('/api/point-sources', {
    data: `schemaVersion: 1
sources:
  - id: ${sourceId}
    name: Points source ${suffix}
    enabled: true
    kind: httpJson
    connection:
      baseUrl: https://example.test
      allowedReadMethods: [GET]
      maximumResponseBytes: 1024
    tls:
      verifyServerCertificate: true
    timeouts:
      connectMilliseconds: 100
      requestMilliseconds: 100
`,
    headers: yamlHeaders
  });

  // Expected outcome: `source.status()` has the required value.
  // Acceptance criteria: `source.status()` must be `201`, because this condition proves that
  // round-trips point and group YAML through the server-backed API.
  expect(source.status()).toBe(201);

  const group = await request.post('/api/point-groups', {
    data: `schemaVersion: 1
groups:
  - id: ${groupId}
    name: Shared group ${suffix}
    sourceId: ${sourceId}
    mappingDefaults: {}
points: []
`,
    headers: yamlHeaders
  });

  // Expected outcome: `group.status()` has the required value.
  // Acceptance criteria: `group.status()` must be `201`, because this condition proves that
  // round-trips point and group YAML through the server-backed API.
  expect(group.status()).toBe(201);

  // Expected outcome: `group.headers(` has the required value.
  // Acceptance criteria: `group.headers(` must be `'1'`, because this condition proves that
  // round-trips point and group YAML through the server-backed API.
  expect(group.headers().etag).toBe('1');

  const member = await request.post('/api/points', {
    data: boundPointYaml(memberId, `Member ${suffix}`, '/member', groupId),
    headers: yamlHeaders
  });

  // Expected outcome: `member.status()` has the required value.
  // Acceptance criteria: `member.status()` must be `201`, because this condition proves that
  // round-trips point and group YAML through the server-backed API.
  expect(member.status()).toBe(201);

  const standalone = await request.post('/api/points', {
    data: boundPointYaml(standaloneId, `Standalone ${suffix}`, '/standalone', undefined, sourceId),
    headers: yamlHeaders
  });

  // Expected outcome: `standalone.status()` has the required value.
  // Acceptance criteria: `standalone.status()` must be `201`, because this condition proves that
  // round-trips point and group YAML through the server-backed API.
  expect(standalone.status()).toBe(201);

  const filtered = await request.get(
    `/api/points?page=1&pageSize=10&filter=${encodeURIComponent(suffix)}&sort=ascending`
  );

  // Expected outcome: The HTTP operation succeeds.
  // Acceptance criteria: the response must have a successful HTTP status, because this condition proves that
  // round-trips point and group YAML through the server-backed API.
  await expect(filtered).toBeOK();

  // Expected outcome: `(await filtered.json()` matches the required structure.
  // Acceptance criteria: `(await filtered.json()` must equal `[ memberId, standaloneId ]`, because this condition proves that
  // round-trips point and group YAML through the server-backed API.
  expect((await filtered.json()).items.map((point: { id: string }) => point.id)).toEqual([
    memberId,
    standaloneId
  ]);

  const update = await request.put(`/api/points/${standaloneId}`, {
    data: boundPointYaml(
      standaloneId,
      `Edited standalone ${suffix}`,
      '/standalone',
      undefined,
      sourceId
    ),
    headers: { ...yamlHeaders, 'If-Match': '1' }
  });

  // Expected outcome: The HTTP operation succeeds.
  // Acceptance criteria: the response must have a successful HTTP status, because this condition proves that
  // round-trips point and group YAML through the server-backed API.
  await expect(update).toBeOK();

  // Expected outcome: `update.headers(` has the required value.
  // Acceptance criteria: `update.headers(` must be `'2'`, because this condition proves that
  // round-trips point and group YAML through the server-backed API.
  expect(update.headers().etag).toBe('2');

  const madeStandalone = await request.post(
    `/api/point-groups/${groupId}/make-points-standalone?revision=1`
  );

  // Expected outcome: The HTTP operation succeeds.
  // Acceptance criteria: the response must have a successful HTTP status, because this condition proves that
  // round-trips point and group YAML through the server-backed API.
  await expect(madeStandalone).toBeOK();

  // Expected outcome: `(await madeStandalone.json()` has the required value.
  // Acceptance criteria: `(await madeStandalone.json()` must be `1`, because this condition proves that
  // round-trips point and group YAML through the server-backed API.
  expect((await madeStandalone.json()).updatedItems).toBe(1);

  // Expected outcome: The HTTP operation succeeds.
  // Acceptance criteria: the response must have a successful HTTP status, because this condition proves that
  // round-trips point and group YAML through the server-backed API.
  await expect(await request.delete(`/api/points/${memberId}?revision=2`)).toBeOK();

  // Expected outcome: The HTTP operation succeeds.
  // Acceptance criteria: the response must have a successful HTTP status, because this condition proves that
  // round-trips point and group YAML through the server-backed API.
  await expect(await request.delete(`/api/points/${standaloneId}?revision=2`)).toBeOK();

  // Expected outcome: The HTTP operation succeeds.
  // Acceptance criteria: the response must have a successful HTTP status, because this condition proves that
  // round-trips point and group YAML through the server-backed API.
  await expect(await request.delete(`/api/point-groups/${groupId}?revision=1`)).toBeOK();

  const reloaded = await request.get(
    `/api/points?page=1&pageSize=10&filter=${encodeURIComponent(suffix)}`
  );

  // Expected outcome: The HTTP operation succeeds.
  // Acceptance criteria: the response must have a successful HTTP status, because this condition proves that
  // round-trips point and group YAML through the server-backed API.
  await expect(reloaded).toBeOK();

  // Expected outcome: `(await reloaded.json()` has the required value.
  // Acceptance criteria: `(await reloaded.json()` must be `0`, because this condition proves that
  // round-trips point and group YAML through the server-backed API.
  expect((await reloaded.json()).totalItems).toBe(0);
});

const boundPointYaml = (
  id: string,
  name: string,
  path: string,
  groupId?: string,
  sourceId?: string
): string => {
  return `schemaVersion: 1
groups: []
points:
  - id: ${id}
    name: ${name}
    enabled: true
${groupId ? `    groupId: ${groupId}\n` : ''}    implementation: bound
    direction: input
    valueType: analog
    readable: true
    commandable: false
    persistence: volatile
${sourceId ? `    sourceId: ${sourceId}\n` : ''}    mapping:
      path: ${path}
      method: GET
`;
};
