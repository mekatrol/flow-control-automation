import { spawnSync } from 'node:child_process';
import { mkdtempSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import process from 'node:process';
import { fileURLToPath } from 'node:url';

const serverProject = fileURLToPath(
  new URL('../../../backend/Server/Server.Api/Server.Api.csproj', import.meta.url)
);
const skipBuild = process.argv.includes('--no-build');
const forwarded = process.argv.slice(2).filter((argument) => argument !== '--no-build');
const buildOutput = skipBuild ? undefined : mkdtempSync(join(tmpdir(), 'flow-function-e2e-build-'));
const build = skipBuild
  ? { status: 0 }
  : spawnSync('dotnet', ['build', serverProject, '--nologo', '--output', buildOutput], {
      env: process.env,
      stdio: 'inherit'
    });

if ('error' in build && build.error) throw build.error;
if (build.status !== 0) {
  process.exitCode = build.status ?? 1;
} else {
  const runner = fileURLToPath(new URL('./run-e2e.mjs', import.meta.url));
  const result = spawnSync(
    process.execPath,
    [
      runner,
      '--dotnet',
      'e2e/functionNodes',
      '--project=desktop-chromium',
      '--workers=1',
      ...forwarded
    ],
    {
      env: {
        ...process.env,
        ...(buildOutput
          ? { FLOW_UI_E2E_BACKEND_DLL: join(buildOutput, 'Server.Api.dll') }
          : {})
      },
      stdio: 'inherit'
    }
  );

  if (result.error) throw result.error;
  process.exitCode = result.status ?? 1;
}

if (buildOutput) rmSync(buildOutput, { recursive: true, force: true });
