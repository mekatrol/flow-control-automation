import { defineConfig, devices } from '@playwright/test';
import { tmpdir } from 'node:os';
import { join } from 'node:path';

// The npm wrapper supplies an allocated port. Direct Playwright and VS Code
// extension runs share the stable local default.
const port = Number(process.env.FLOW_UI_E2E_PORT ?? 5184);
const baseURL = `http://127.0.0.1:${port}`;
// Direct Playwright and VS Code extension runs provision the real backend.
// The npm wrapper sets FLOW_UI_E2E_MANAGED_SERVERS and owns server startup, so
// mocked command-line suites remain lightweight and route-isolated.
const useDotnetBackend = process.env.FLOW_UI_E2E_BACKEND !== 'mock';
const externalBackendURL = process.env.FLOW_UI_E2E_BACKEND_URL;
const backendURL = externalBackendURL ?? 'http://127.0.0.1:5018';
const testEncryptionKey = 'AAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8=';
const testApiKey = 'flow-control-e2e-administrator-key';
const isHeaded = process.argv.includes('--headed');
const runFirefox = process.env.FLOW_UI_E2E_FIREFOX === '1';
const managedServers = process.env.FLOW_UI_E2E_MANAGED_SERVERS === '1';

export default defineConfig({
  testDir: './e2e',
  tsconfig: './tsconfig.app.json',
  fullyParallel: true,
  // Visible browsers are substantially more resource-intensive. With every test and
  // browser project fully parallel, headed runs can stall while creating a context or
  // dispatching an otherwise-ready click. Keep headless runs at Playwright's default.
  workers: isHeaded ? 1 : undefined,
  forbidOnly: Boolean(process.env.CI),
  retries: process.env.CI ? 2 : 0,
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
    ...(useDotnetBackend && !externalBackendURL
      ? [
          {
            command: 'dotnet ../../backend/Server/Server.Api/bin/Debug/net10.0/Server.Api.dll',
            url: `${backendURL}/api/health`,
            reuseExistingServer: false,
            timeout: 60_000,
            stdout: 'ignore' as const,
            stderr: 'ignore' as const,
            env: {
              SERVER_ADDRESS: backendURL,
              CREDENTIAL_ENCRYPTION_KEY: testEncryptionKey,
              ApiAccess__Identities__e2e__Key: testApiKey,
              ApiAccess__Identities__e2e__Permissions__0: '*',
              ConnectionStrings__FlowControl: `Data Source=${join(tmpdir(), `flow-control-e2e-${process.pid}.db`)}`
            }
          }
        ]
      : []),
    {
      command: `node ./node_modules/vite/bin/vite.js --host 127.0.0.1 --port ${port} --strictPort`,
      url: baseURL,
      reuseExistingServer: !process.env.CI,
      timeout: 30_000,
      stdout: 'ignore' as const,
      stderr: 'ignore' as const,
      env: {
        FLOW_UI_E2E: '1',
        VITE_FLOW_CONTROL_API_KEY: testApiKey,
        ...(useDotnetBackend ? { VITE_API_PROXY: backendURL } : {})
      }
    }
  ])
});
