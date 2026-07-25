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
    kind: http_json
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
  expect(group.status()).toBe(201);
  expect(group.headers().etag).toBe('1');

  const member = await request.post('/api/points', {
    data: boundPointYaml(memberId, `Member ${suffix}`, '/member', groupId),
    headers: yamlHeaders
  });
  expect(member.status()).toBe(201);

  const standalone = await request.post('/api/points', {
    data: boundPointYaml(standaloneId, `Standalone ${suffix}`, '/standalone', undefined, sourceId),
    headers: yamlHeaders
  });
  expect(standalone.status()).toBe(201);

  const filtered = await request.get(
    `/api/points?page=1&pageSize=10&filter=${encodeURIComponent(suffix)}&sort=ascending`
  );
  await expect(filtered).toBeOK();
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
  await expect(update).toBeOK();
  expect(update.headers().etag).toBe('2');

  const madeStandalone = await request.post(
    `/api/point-groups/${groupId}/make-points-standalone?revision=1`
  );
  await expect(madeStandalone).toBeOK();
  expect((await madeStandalone.json()).updatedItems).toBe(1);

  await expect(await request.delete(`/api/points/${memberId}?revision=2`)).toBeOK();
  await expect(await request.delete(`/api/points/${standaloneId}?revision=2`)).toBeOK();
  await expect(await request.delete(`/api/point-groups/${groupId}?revision=1`)).toBeOK();

  const reloaded = await request.get(
    `/api/points?page=1&pageSize=10&filter=${encodeURIComponent(suffix)}`
  );
  await expect(reloaded).toBeOK();
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
