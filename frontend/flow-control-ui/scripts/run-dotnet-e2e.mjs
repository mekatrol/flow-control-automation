import { spawnSync } from 'node:child_process';
import process from 'node:process';
import { fileURLToPath } from 'node:url';

const playwrightCli = fileURLToPath(
  new URL('../node_modules/@playwright/test/cli.js', import.meta.url)
);
const result = spawnSync(
  process.execPath,
  [
    playwrightCli,
    'test',
    'e2e/backendCompatibility.spec.ts',
    'e2e/pointsApi.spec.ts',
    'e2e/controllerTemplatesApi.spec.ts',
    '--project=desktop-chromium'
  ],
  {
    env: { ...process.env, FLOW_UI_E2E_BACKEND: 'dotnet' },
    stdio: 'inherit'
  }
);

if (result.error) throw result.error;
process.exitCode = result.status ?? 1;
