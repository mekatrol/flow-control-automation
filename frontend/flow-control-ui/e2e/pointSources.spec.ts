import { expect, test } from '@playwright/test';

// Monaco and its YAML worker are intentionally exercised in sequence within
// each browser project so diagnostics are not distorted by resource contention.
test.describe.configure({ mode: 'serial' });

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

/**
 * Purpose: Protects the behavioral contract that catalogue and YAML editor support create, test, retry, and keyboard use.
 * Description: Exercises catalogue and YAML editor support create, test, retry, and keyboard use from its arranged starting state and
 * verifies the observable results required by the scenario.
 */
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

  // Expected outcome: `page.getByRole('heading', { name: 'Point sources' })` is visible to the user.
  // Acceptance criteria: `page.getByRole('heading', { name: 'Point sources' })` must be visible, because this condition proves that
  // catalogue and YAML editor support create, test, retry, and keyboard use.
  await expect(page.getByRole('heading', { name: 'Point sources' })).toBeVisible();
  await page.getByRole('link', { name: 'New source' }).press('Enter');
  // Monaco keeps its accessible textarea off-screen in Firefox while the
  // interactive editor surface remains visible and keyboard operable.

  // Expected outcome: `page.locator('.monaco-editor')` is visible to the user.
  // Acceptance criteria: `page.locator('.monaco-editor')` must be visible, because this condition proves that
  // catalogue and YAML editor support create, test, retry, and keyboard use.
  await expect(page.locator('.monaco-editor')).toBeVisible({
    timeout: 60_000
  });
  await page.getByRole('radio', { name: /MQTT/ }).check();

  // Expected outcome: `page.getByLabel('MQTT example YAML')` displays the required content.
  // Acceptance criteria: `page.getByLabel('MQTT example YAML')` must contain the text `'brokerUrl: mqtts://'`, because this condition proves that
  // catalogue and YAML editor support create, test, retry, and keyboard use.
  await expect(page.getByLabel('MQTT example YAML')).toContainText('brokerUrl: mqtts://');
  await page.getByRole('button', { name: 'Use this example' }).click();

  // Expected outcome: `page.locator('.monaco-editor .view-lines')` displays the required content.
  // Acceptance criteria: `page.locator('.monaco-editor .view-lines')` must contain the text `'kind: mqtt'`, because this condition proves that
  // catalogue and YAML editor support create, test, retry, and keyboard use.
  await expect(page.locator('.monaco-editor .view-lines')).toContainText('kind: mqtt');
  await page.getByRole('radio', { name: /HTTP \/ JSON/ }).check();

  // Expected outcome: `page.getByLabel('HTTP / JSON example YAML')` displays the required content.
  // Acceptance criteria: `page.getByLabel('HTTP / JSON example YAML')` must contain the text `'allowedReadMethods: [GET]'`, because this condition proves that
  // catalogue and YAML editor support create, test, retry, and keyboard use.
  await expect(page.getByLabel('HTTP / JSON example YAML')).toContainText(
    'allowedReadMethods: [GET]'
  );
  await page.locator('.monaco-editor .view-lines').click();
  await page.keyboard.press('ControlOrMeta+A');
  await page.keyboard.insertText(sourceYAML);
  await page.getByRole('button', { name: 'Test connection' }).press('Enter');

  // Expected outcome: `page.getByRole('heading', { name: 'Connection test: failed' })` is visible to the user.
  // Acceptance criteria: `page.getByRole('heading', { name: 'Connection test: failed' })` must be visible, because this condition proves that
  // catalogue and YAML editor support create, test, retry, and keyboard use.
  await expect(page.getByRole('heading', { name: 'Connection test: failed' })).toBeVisible();
  await page.getByRole('button', { name: 'Retry test' }).press('Enter');

  // Expected outcome: `page.getByRole('heading', { name: 'Connection test: passed' })` is visible to the user.
  // Acceptance criteria: `page.getByRole('heading', { name: 'Connection test: passed' })` must be visible, because this condition proves that
  // catalogue and YAML editor support create, test, retry, and keyboard use.
  await expect(page.getByRole('heading', { name: 'Connection test: passed' })).toBeVisible();
  await page.getByRole('button', { name: 'Save' }).press('Enter');

  // Expected outcome: Navigation reaches the required route.
  // Acceptance criteria: the page URL must match `'/point-sources/weather'`, because this condition proves that
  // catalogue and YAML editor support create, test, retry, and keyboard use.
  await expect(page).toHaveURL('/point-sources/weather');

  // Expected outcome: `page.locator('.monaco-editor .view-lines')` displays the required content.
  // Acceptance criteria: `page.locator('.monaco-editor .view-lines')` must contain the text `'Weather API'`, because this condition proves that
  // catalogue and YAML editor support create, test, retry, and keyboard use.
  await expect(page.locator('.monaco-editor .view-lines')).toContainText('Weather API');
});

/**
 * Purpose: Protects the behavioral contract that reports schema and indentation errors before a source can be tested or saved.
 * Description: Exercises reports schema and indentation errors before a source can be tested or saved from its arranged starting state and
 * verifies the observable results required by the scenario.
 */
test('reports schema and indentation errors before a source can be tested or saved', async ({
  page
}) => {
  await page.goto('/point-sources/new');

  // Expected outcome: `page.locator('.monaco-editor')` is visible to the user.
  // Acceptance criteria: `page.locator('.monaco-editor')` must be visible, because this condition proves that
  // reports schema and indentation errors before a source can be tested or saved.
  await expect(page.locator('.monaco-editor')).toBeVisible({
    timeout: 60_000
  });
  await page.locator('.monaco-editor .view-lines').click();
  await page.keyboard.press('ControlOrMeta+A');
  await page.keyboard.insertText(`schemaVersion: 1
sources:
  - id: broken-mqtt
    name: Broken MQTT
    enabled: true
    kind: mqtt
    connection:
    brokerUrl: mqtt://mqtt.lan:1883
    tls:
      verifyServerCertificate: true
    timeouts:
      connectMilliseconds: 3000
`);

  const summary = page.getByRole('heading', { name: /YAML problems?/ });

  // Expected outcome: `summary` is visible to the user.
  // Acceptance criteria: `summary` must be visible, because this condition proves that
  // reports schema and indentation errors before a source can be tested or saved.
  await expect(summary).toBeVisible();

  // Expected outcome: `page.getByRole('button', { name: 'Save' })` prevents interaction.
  // Acceptance criteria: `page.getByRole('button', { name: 'Save' })` must be disabled, because this condition proves that
  // reports schema and indentation errors before a source can be tested or saved.
  await expect(page.getByRole('button', { name: 'Save' })).toBeDisabled();

  // Expected outcome: `page.getByRole('button', { name: 'Test connection' })` prevents interaction.
  // Acceptance criteria: `page.getByRole('button', { name: 'Test connection' })` must be disabled, because this condition proves that
  // reports schema and indentation errors before a source can be tested or saved.
  await expect(page.getByRole('button', { name: 'Test connection' })).toBeDisabled();

  // Expected outcome: `page.getByRole('button', { name: /Line \d+, column \d+:/ }` is visible to the user.
  // Acceptance criteria: `page.getByRole('button', { name: /Line \d+, column \d+:/ }` must be visible, because this condition proves that
  // reports schema and indentation errors before a source can be tested or saved.
  await expect(page.getByRole('button', { name: /Line \d+, column \d+:/ }).first()).toBeVisible();
});
