import { defineConfig, devices } from '@playwright/test';
import { createServer } from 'node:net';

const getAvailablePort = (): Promise<number> =>
  new Promise((resolve, reject) => {
    const server = createServer();
    server.unref();
    server.once('error', reject);
    server.listen(0, '127.0.0.1', () => {
      const address = server.address();
      if (!address || typeof address === 'string') {
        server.close();
        reject(new Error('Could not allocate an E2E server port.'));
        return;
      }
      server.close((error) => (error ? reject(error) : resolve(address.port)));
    });
  });

const managedServers = process.env.FLOW_UI_E2E_MANAGED_SERVERS === '1';
// The npm wrapper supplies its allocated frontend port. Direct Playwright and
// VS Code runs allocate isolated ports so concurrent or stale servers cannot
// collide with the test run.
const port = process.env.FLOW_UI_E2E_PORT
  ? Number(process.env.FLOW_UI_E2E_PORT)
  : await getAvailablePort();
// Playwright reloads this config in its worker processes. Publishing the chosen
// port makes every worker inherit the server owner's allocation.
process.env.FLOW_UI_E2E_PORT ??= String(port);
const baseURL = `http://127.0.0.1:${port}`;
// Direct Playwright and VS Code extension runs provision the real backend.
// The npm wrapper sets FLOW_UI_E2E_MANAGED_SERVERS and owns server startup, so
// mocked command-line suites remain lightweight and route-isolated.
const useDotnetBackend = process.env.FLOW_UI_E2E_BACKEND !== 'mock';
const externalBackendURL = process.env.FLOW_UI_E2E_BACKEND_URL;
const backendURL =
  externalBackendURL ?? `http://127.0.0.1:${managedServers ? 5018 : await getAvailablePort()}`;
process.env.FLOW_UI_E2E_BACKEND_URL ??= backendURL;
// Test Explorer keeps Playwright's web servers alive between runs. Let global
// setup own the backend instead so its teardown can release the compiled DLL
// as soon as each run finishes.
if (useDotnetBackend && !managedServers && !externalBackendURL) {
  process.env.FLOW_UI_E2E_OWNS_BACKEND = '1';
}
const testApiKey = 'flow-control-e2e-administrator-key';
const isHeaded = process.argv.includes('--headed');
const runFirefox = process.env.FLOW_UI_E2E_FIREFOX === '1';

export default defineConfig({
  testDir: './e2e',
  testIgnore: useDotnetBackend ? undefined : 'functionNodes/**',
  tsconfig: './tsconfig.app.json',
  fullyParallel: true,
  // Visible browsers are substantially more resource-intensive. With every test and
  // browser project fully parallel, headed runs can stall while creating a context or
  // dispatching an otherwise-ready click. Keep headless runs at Playwright's default.
  workers: isHeaded ? 1 : undefined,
  forbidOnly: Boolean(process.env.CI),
  retries: process.env.CI ? 2 : 0,
  globalSetup: useDotnetBackend ? './e2e/globalSetup.mjs' : undefined,
  reporter: process.env.CI ? 'github' : 'list',
  use: {
    baseURL,
    extraHTTPHeaders: { 'X-Api-Key': testApiKey },
    trace: 'on-first-retry'
  },
  projects: [
    {
      name: 'desktop-chromium',
      use: { ...devices['Desktop Chrome'] }
    },
    ...(runFirefox
      ? [{ name: 'desktop-firefox', use: { ...devices['Desktop Firefox'] } }]
      : []),
    {
      name: 'desktop-edge',
      use: { ...devices['Desktop Edge'], channel: 'msedge' }
    },
    {
      name: 'mobile-chromium',
      use: { ...devices['Pixel 7'] }
    }
  ],
  webServer: managedServers ? [] : ([
    {
      command: `node ./node_modules/vite/bin/vite.js --host 127.0.0.1 --port ${port} --strictPort`,
      url: baseURL,
      reuseExistingServer: !process.env.CI,
      timeout: 30_000,
      stdout: 'ignore' as const,
      stderr: 'ignore' as const,
      env: {
        FLOW_UI_E2E: '1',
        VITE_HIDDEN_FLOW_NODE_KINDS: '',
        VITE_FLOW_CONTROL_API_KEY: testApiKey,
        ...(useDotnetBackend ? { VITE_API_PROXY: backendURL } : {})
      }
    }
  ])
});
