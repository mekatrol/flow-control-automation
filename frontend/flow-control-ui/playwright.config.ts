import { defineConfig, devices } from '@playwright/test';

// Allow parallel worktrees or a developer's existing preview server to coexist
// with an isolated test run while preserving the usual local default.
const port = Number(process.env.FLOW_UI_E2E_PORT ?? 5174);
const baseURL = `http://127.0.0.1:${port}`;
const useDotnetBackend = process.env.FLOW_UI_E2E_BACKEND === 'dotnet';
const backendURL = 'http://127.0.0.1:5008';
const testEncryptionKey = 'AAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8=';

export default defineConfig({
  testDir: './e2e',
  tsconfig: './tsconfig.app.json',
  fullyParallel: true,
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
    ...(useDotnetBackend
      ? [
          {
            command:
              'dotnet run --no-launch-profile --project ../../backend/Server/Server.Api/Server.Api.csproj',
            url: `${backendURL}/api/health`,
            reuseExistingServer: false,
            timeout: 60_000,
            env: {
              SERVER_ADDRESS: backendURL,
              CREDENTIAL_ENCRYPTION_KEY: testEncryptionKey,
              ConnectionStrings__FlowControl: `Data Source=/tmp/flow-control-e2e-${process.pid}.db`
            }
          }
        ]
      : []),
    {
      command: `npm run dev:debug -- --host 127.0.0.1 --port ${port} --strictPort`,
      url: baseURL,
      reuseExistingServer: !process.env.CI,
      timeout: 30_000,
      env: useDotnetBackend ? { VITE_API_PROXY: backendURL } : {}
    }
  ]
});
