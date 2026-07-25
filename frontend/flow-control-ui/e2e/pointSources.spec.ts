import { expect, test } from '@playwright/test';

const sourceYAML = `schemaVersion: 1
sources:
  - id: weather
    name: Weather API
    enabled: true
    kind: http_json
    connection:
      baseUrl: https://weather.example.test
      allowedReadMethods: [GET]
      followRedirects: false
      maximumResponseBytes: 65536
    tls:
      verifyServerCertificate: true
    timeouts:
      connectMilliseconds: 2000
      requestMilliseconds: 5000
`;

test.beforeEach(async ({ page }) => {
  await page.route(/\/api\/point-sources(?:\?.*)?$/, async (route) => {
    if (route.request().method() !== 'GET') return route.fallback();
    await route.fulfill({
      json: { items: [], totalItems: 0, page: 1, pageSize: 50, pageCount: 1 }
    });
  });
});

test('catalogue and YAML editor support create, test, retry, and keyboard use', async ({
  page
}) => {
  let tests = 0;
  await page.route('/api/point-sources/test', async (route) => {
    tests++;
    await route.fulfill({
      json:
        tests === 1
          ? {
              status: 'failed',
              durationMilliseconds: 12,
              stages: [{ name: 'protocol', status: 'failed', diagnostic: 'HTTP status 503' }]
            }
          : {
              status: 'passed',
              durationMilliseconds: 8,
              stages: [
                { name: 'dns', status: 'passed' },
                { name: 'tcp', status: 'passed' },
                { name: 'tls', status: 'passed' },
                { name: 'authentication', status: 'passed' },
                { name: 'protocol', status: 'passed' }
              ]
            }
    });
  });
  await page.route('/api/point-sources', async (route) => {
    if (route.request().method() !== 'POST') return route.fallback();
    await route.fulfill({
      status: 201,
      headers: { 'Content-Type': 'application/yaml', ETag: '1' },
      body: sourceYAML
    });
  });
  await page.route('/api/point-sources/weather', async (route) => {
    await route.fulfill({
      headers: { 'Content-Type': 'application/yaml', ETag: '1' },
      body: sourceYAML
    });
  });

  await page.goto('/point-sources');
  await expect(page.getByRole('heading', { name: 'Point sources' })).toBeVisible();
  await page.getByRole('link', { name: 'New source' }).press('Enter');
  const editor = page.getByLabel('Point source YAML');
  await page.getByRole('radio', { name: /MQTT/ }).check();
  await expect(page.getByLabel('MQTT example YAML')).toContainText('brokerUrl: mqtts://');
  await page.getByRole('button', { name: 'Use this example' }).click();
  await expect(editor).toHaveValue(/kind: mqtt/);
  await page.getByRole('radio', { name: /HTTP \/ JSON/ }).check();
  await expect(page.getByLabel('HTTP / JSON example YAML')).toContainText(
    'allowedReadMethods: [GET]'
  );
  await editor.fill(sourceYAML);
  await page.getByRole('button', { name: 'Test connection' }).press('Enter');
  await expect(page.getByRole('heading', { name: 'Connection test: failed' })).toBeVisible();
  await page.getByRole('button', { name: 'Retry test' }).press('Enter');
  await expect(page.getByRole('heading', { name: 'Connection test: passed' })).toBeVisible();
  await page.getByRole('button', { name: 'Save' }).press('Enter');
  await expect(page).toHaveURL('/point-sources/weather');
  await expect(page.getByLabel('Point source YAML')).toHaveValue(/Weather API/);
});
