import { defineConfig, devices } from '@playwright/test';
import { tmpdir } from 'node:os';
import { join } from 'node:path';

// Allow parallel worktrees or a developer's existing preview server to coexist
// with an isolated test run while preserving the usual local default.
const port = Number(process.env.FLOW_UI_E2E_PORT ?? 5174);
const baseURL = `http://127.0.0.1:${port}`;
const useDotnetBackend = process.env.FLOW_UI_E2E_BACKEND === 'dotnet';
const externalBackendURL = process.env.FLOW_UI_E2E_BACKEND_URL;
const backendURL = externalBackendURL ?? 'http://127.0.0.1:5008';
const testEncryptionKey = 'AAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8=';
const isHeaded = process.argv.includes('--headed');

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
    trace: 'on-first-retry'
  },
  projects: [
    {
      name: 'desktop-chromium',
      use: { ...devices['Desktop Chrome'] }
    },
    {
      name: 'desktop-firefox',
      use: { ...devices['Desktop Firefox'] }
    },
    {
      name: 'desktop-edge',
      use: { ...devices['Desktop Edge'], channel: 'msedge' }
    },
    {
      name: 'mobile-chromium',
      use: { ...devices['Pixel 7'] }
    }
  ],
  webServer: [
    ...(useDotnetBackend && !externalBackendURL
      ? [
          {
            command:
              'dotnet run --no-build --no-launch-profile --project ../../backend/Server/Server.Api/Server.Api.csproj',
            url: `${backendURL}/api/health`,
            reuseExistingServer: false,
            timeout: 60_000,
            env: {
              SERVER_ADDRESS: backendURL,
              CREDENTIAL_ENCRYPTION_KEY: testEncryptionKey,
              ConnectionStrings__FlowControl: `Data Source=${join(tmpdir(), `flow-control-e2e-${process.pid}.db`)}`
            }
          }
        ]
      : []),
    {
      command: `node ./node_modules/vite/bin/vite.js --host 127.0.0.1 --port ${port} --strictPort`,
      url: baseURL,
      // A normal mocked run may leave a reusable Vite process without the .NET
      // proxy target. Backend runs must start their own correctly configured proxy.
      reuseExistingServer: !process.env.CI && !useDotnetBackend,
      timeout: 30_000,
      env: {
        FLOW_UI_E2E: '1',
        ...(useDotnetBackend ? { VITE_API_PROXY: backendURL } : {})
      }
    }
  ]
});
