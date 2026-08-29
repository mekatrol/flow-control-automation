import { spawn } from 'node:child_process';
import { randomUUID } from 'node:crypto';
import { tmpdir } from 'node:os';
import { isAbsolute, join, resolve } from 'node:path';
import process from 'node:process';
import { fileURLToPath } from 'node:url';

const defaultBackendDll = fileURLToPath(
  new URL('../../../backend/Server/Server.Api/bin/Debug/net10.0/Server.Api.dll', import.meta.url)
);
const frontendRoot = fileURLToPath(new URL('../', import.meta.url));

const getBackendDll = () => {
  const configured = process.env.FLOW_UI_E2E_BACKEND_DLL;
  if (!configured) return defaultBackendDll;
  return isAbsolute(configured) ? configured : resolve(frontendRoot, configured);
};

const formatBackendFailure = (message, output) => {
  const details = output().trim();
  return new Error(details ? `${message}\n\nBackend output:\n${details}` : message);
};

const waitForBackend = async (url, child, output) => {
  const deadline = Date.now() + 60_000;
  while (Date.now() < deadline) {
    if (child.exitCode !== null) {
      throw formatBackendFailure(
        `The E2E backend exited before becoming ready (exit code ${child.exitCode}).`,
        output
      );
    }
    try {
      const response = await fetch(new URL('/api/health', url));
      if (response.ok) return;
    } catch {
      // The backend is still starting.
    }
    await new Promise((resolve) => setTimeout(resolve, 200));
  }
  throw formatBackendFailure(`Timed out waiting for the E2E backend at ${url}.`, output);
};

const stopBackend = async (child) => {
  if (!child.pid || child.exitCode !== null) return;
  const exited = new Promise((resolve) => child.once('exit', resolve));

  if (process.platform === 'win32') {
    const taskkill = spawn('taskkill', ['/pid', String(child.pid), '/T', '/F'], {
      stdio: 'ignore',
      windowsHide: true
    });
    await new Promise((resolve) => taskkill.once('exit', resolve));
    if (child.exitCode === null) child.kill('SIGKILL');
  } else {
    child.kill('SIGTERM');
  }

  await Promise.race([
    exited,
    new Promise((resolve) => {
      const timeout = setTimeout(resolve, 5_000);
      timeout.unref();
    })
  ]);
};

export default async function setup(config) {
  const project = config.projects[0];
  const baseURL = project?.use.baseURL;
  if (typeof baseURL !== 'string') throw new Error('Playwright baseURL is not configured.');

  let backend;
  if (process.env.FLOW_UI_E2E_OWNS_BACKEND === '1') {
    const backendURL = process.env.FLOW_UI_E2E_BACKEND_URL;
    if (!backendURL) throw new Error('The E2E backend URL is not configured.');

    let backendOutput = '';
    const runId = randomUUID();
    const appendBackendOutput = (chunk) => {
      backendOutput = `${backendOutput}${chunk}`.slice(-16_384);
    };

    backend = spawn(
      'dotnet',
      [getBackendDll()],
      {
        cwd: frontendRoot,
        env: {
          ...process.env,
          SERVER_ADDRESS: backendURL,
          CREDENTIAL_ENCRYPTION_KEY: 'AAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8=',
          ApiAccess__Identities__e2e__Key: 'flow-control-e2e-administrator-key',
          ApiAccess__Identities__e2e__Permissions__0: '*',
          CONTROLLER_DATA_FILE: join(tmpdir(), `flow-control-controllers-${runId}.json`),
          ConnectionStrings__FlowControl: `Data Source=${join(tmpdir(), `flow-control-e2e-${runId}.db`)}`
        },
        stdio: ['ignore', 'pipe', 'pipe'],
        windowsHide: true
      }
    );
    backend.stdout.on('data', appendBackendOutput);
    backend.stderr.on('data', appendBackendOutput);

    try {
      await Promise.race([
        waitForBackend(backendURL, backend, () => backendOutput),
        new Promise((_, reject) =>
          backend.once('error', (error) =>
            reject(
              formatBackendFailure(`Failed to start the E2E backend: ${error.message}`, () => backendOutput)
            )
          )
        )
      ]);
    } catch (error) {
      await stopBackend(backend);
      throw error;
    }
  }

  const response = await fetch(new URL('/api/simulator-sessions', baseURL), {
    method: 'DELETE',
    headers: { 'X-Api-Key': 'flow-control-e2e-administrator-key' }
  });
  if (!response.ok) {
    if (backend) await stopBackend(backend);
    throw new Error(`Failed to clear simulator sessions before the test run: ${response.status}.`);
  }

  if (backend) return () => stopBackend(backend);
}
