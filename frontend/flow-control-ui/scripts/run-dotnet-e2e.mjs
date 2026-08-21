import { spawnSync } from 'node:child_process';
import process from 'node:process';
import { fileURLToPath } from 'node:url';

const runner = fileURLToPath(new URL('./run-e2e.mjs', import.meta.url));
const result = spawnSync(
  process.execPath,
  [
    runner,
    '--dotnet',
    'e2e/backendCompatibility.spec.ts',
    'e2e/pointsApi.spec.ts',
    'e2e/controllerTemplatesApi.spec.ts',
    '--project=desktop-chromium'
  ],
  {
    env: process.env,
    stdio: 'inherit'
  }
);

if (result.error) throw result.error;
process.exitCode = result.status ?? 1;
