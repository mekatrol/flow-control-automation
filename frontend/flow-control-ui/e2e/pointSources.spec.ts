import { expect, test } from '@playwright/test';

// Monaco and its YAML worker are intentionally exercised in sequence within
// each browser project so diagnostics are not distorted by resource contention.
test.describe.configure({ mode: 'serial' });

const sourceYAML = `schemaVersion: 1
sources:
  - id: weather
    name: Weather API
    enabled: true
    kind: httpJson
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
 * Purpose: Protects the complete point-source onboarding journey, including accessible
 * keyboard operation, server diagnostics, retry, persistence, and normalized reload.
 * Description: Opens a new source, switches examples, runs a failed then successful
 * connection test with the keyboard, saves it, and observes the server-returned source.
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
              httpResponse: {
                statusCode: 200,
                reasonPhrase: 'OK',
                contentType: 'application/json',
                body: '{"intensity":100}'
              },
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

  // Expected outcome: The point-source catalogue is ready before creation begins.
  // Acceptance criteria: The "Point sources" heading is visible because keyboard navigation
  // to the creation route must start from the loaded catalogue rather than a transient state.
  await expect(page.getByRole('heading', { name: 'Point sources' })).toBeVisible();
  await page.getByRole('link', { name: 'New source' }).press('Enter');
  // Monaco keeps its accessible textarea off-screen in Firefox while the
  // interactive editor surface remains visible and keyboard operable.

  // Expected outcome: The new-source route presents its YAML editor.
  // Acceptance criteria: Monaco is visible because source configuration must be available
  // for review and editing after keyboard activation of the "New source" link.
  await expect(page.locator('.monaco-editor')).toBeVisible({
    timeout: 60_000
  });
  await page.getByRole('radio', { name: /MQTT/ }).check();

  // Expected outcome: Selecting MQTT presents a secure broker example.
  // Acceptance criteria: The MQTT example contains `brokerUrl: mqtts://` because the
  // starter configuration must demonstrate encrypted broker transport.
  await expect(page.getByLabel('MQTT example YAML')).toContainText('brokerUrl: mqtts://');
  await page.getByRole('button', { name: 'Use this example' }).click();

  // Expected outcome: Loading the MQTT example replaces the active editor configuration.
  // Acceptance criteria: The editor contains `kind: mqtt` because "Use this example"
  // must copy the selected example into the source being configured.
  await expect(page.locator('.monaco-editor .view-lines')).toContainText('kind: mqtt');
  await page.getByRole('radio', { name: /HTTP \/ JSON/ }).check();

  // Expected outcome: Selecting HTTP presents a read-only request policy.
  // Acceptance criteria: The HTTP example contains `allowedReadMethods: [GET]` because
  // point sources may read remote data but must not demonstrate mutating methods.
  await expect(page.getByLabel('HTTP / JSON example YAML')).toContainText(
    'allowedReadMethods: [GET]'
  );
  await page.getByRole('button', { name: 'Use this example' }).click();

  // Expected outcome: Loading the selected HTTP example replaces the editor configuration.
  // Acceptance criteria: The rendered YAML contains `kind: httpJson` because the selected
  // example must become the active configuration before it can be tested or saved.
  await expect(page.locator('.monaco-editor .view-lines')).toContainText('kind: httpJson');

  // Expected outcome: A valid loaded example is eligible for persistence.
  // Acceptance criteria: Save is enabled because the HTTP example satisfies the point-source
  // schema and therefore has no client-side validation error blocking persistence.
  await expect(page.getByRole('button', { name: 'Save' })).toBeEnabled();

  // Expected outcome: A valid loaded example is eligible for a connection test.
  // Acceptance criteria: Test connection is enabled because only schema-valid point-source
  // YAML may be submitted to the connection diagnostic endpoint.
  await expect(page.getByRole('button', { name: 'Test connection' })).toBeEnabled();
  await page.getByRole('button', { name: 'Test connection' }).press('Enter');

  // Expected outcome: A failed protocol diagnostic is visibly reported.
  // Acceptance criteria: "Connection test: failed" is visible because the first mocked
  // diagnostic returns HTTP 503 and must not be presented as a successful connection.
  await expect(page.getByRole('heading', { name: 'Connection test: failed' })).toBeVisible();
  await page.getByRole('button', { name: 'Retry test' }).press('Enter');

  // Expected outcome: Retrying replaces the failed result with a successful diagnostic.
  // Acceptance criteria: "Connection test: passed" is visible because every stage in the
  // second mocked diagnostic succeeds and the latest result must supersede the first.
  await expect(page.getByRole('heading', { name: 'Connection test: passed' })).toBeVisible();
  await expect(page.getByRole('heading', { name: 'HTTP response' })).toBeVisible();
  await expect(page.getByText('{"intensity":100}')).toBeVisible();
  await page.getByRole('button', { name: 'Save' }).press('Enter');

  // Expected outcome: Saving a new source transitions to its stable detail route.
  // Acceptance criteria: The URL is `/point-sources/weather` because the server-normalized
  // response identifies the persisted source as `weather`.
  await expect(page).toHaveURL('/point-sources/weather');

  // Expected outcome: The detail route displays the server-returned persisted configuration.
  // Acceptance criteria: The editor contains "Weather API" because the GET for `weather`
  // returns that normalized source and the route must reload it rather than retain the draft.
  await expect(page.locator('.monaco-editor .view-lines')).toContainText('Weather API');
});

/**
 * Purpose: Protects client-side validation from submitting malformed or schema-invalid
 * point-source YAML to persistence and connection-test endpoints.
 * Description: Enters YAML with a structural schema error and bad indentation, waits for
 * diagnostics, and observes that both server actions remain unavailable.
 */
test('reports schema and indentation errors before a source can be tested or saved', async ({
  page
}) => {
  await page.goto('/point-sources/new');

  // Expected outcome: The validation scenario starts with an interactive YAML editor.
  // Acceptance criteria: Monaco is visible because malformed YAML must be entered through
  // the same editor and worker validation path used by real source configuration.
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

  // Expected outcome: Invalid YAML produces a visible diagnostic summary.
  // Acceptance criteria: A "YAML problem" heading is visible because the arranged content
  // contains schema and indentation faults that users must be told how to correct.
  await expect(summary).toBeVisible();

  // Expected outcome: Invalid source YAML cannot be persisted.
  // Acceptance criteria: Save is disabled because submitting known-invalid configuration
  // would defer preventable schema errors to the server.
  await expect(page.getByRole('button', { name: 'Save' })).toBeDisabled();

  // Expected outcome: Invalid source YAML cannot initiate an external connection test.
  // Acceptance criteria: Test connection is disabled because malformed configuration
  // cannot safely or meaningfully identify a remote endpoint to test.
  await expect(page.getByRole('button', { name: 'Test connection' })).toBeDisabled();

  // Expected outcome: At least one diagnostic identifies a navigable source location.
  // Acceptance criteria: A visible diagnostic button names a line and column because users
  // need a precise editor location from which to correct the malformed YAML.
  await expect(page.getByRole('button', { name: /Line \d+, column \d+:/ }).first()).toBeVisible();
});
