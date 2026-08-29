import { spawn } from 'node:child_process';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import process from 'node:process';

const waitForBackend = async (url, child) => {
  const deadline = Date.now() + 60_000;
  while (Date.now() < deadline) {
    if (child.exitCode !== null) {
      throw new Error(`The E2E backend exited before becoming ready (exit code ${child.exitCode}).`);
    }
    try {
      const response = await fetch(new URL('/api/health', url));
      if (response.ok) return;
    } catch {
      // The backend is still starting.
    }
    await new Promise((resolve) => setTimeout(resolve, 200));
  }
  throw new Error(`Timed out waiting for the E2E backend at ${url}.`);
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
  } else {
    child.kill('SIGTERM');
  }

  await exited;
};

export default async function setup(config) {
  const project = config.projects[0];
  const baseURL = project?.use.baseURL;
  if (typeof baseURL !== 'string') throw new Error('Playwright baseURL is not configured.');

  let backend;
  if (process.env.FLOW_UI_E2E_OWNS_BACKEND === '1') {
    const backendURL = process.env.FLOW_UI_E2E_BACKEND_URL;
    if (!backendURL) throw new Error('The E2E backend URL is not configured.');

    backend = spawn(
      'dotnet',
      ['../../backend/Server/Server.Api/bin/Debug/net10.0/Server.Api.dll'],
      {
        env: {
          ...process.env,
          SERVER_ADDRESS: backendURL,
          CREDENTIAL_ENCRYPTION_KEY: 'AAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8=',
          ApiAccess__Identities__e2e__Key: 'flow-control-e2e-administrator-key',
          ApiAccess__Identities__e2e__Permissions__0: '*',
          ConnectionStrings__FlowControl: `Data Source=${join(tmpdir(), `flow-control-e2e-${process.pid}.db`)}`
        },
        stdio: 'ignore',
        windowsHide: true
      }
    );

    try {
      await waitForBackend(backendURL, backend);
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
