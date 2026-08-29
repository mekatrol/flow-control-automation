import { spawn, spawnSync } from 'node:child_process';
import { createServer } from 'node:net';
import process from 'node:process';
import { fileURLToPath } from 'node:url';

const getAvailablePort = () =>
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

const dotnet = process.argv.includes('--dotnet');
const forwarded = process.argv.slice(2).filter((argument) => argument !== '--dotnet');
const port = process.env.FLOW_UI_E2E_PORT
  ? Number(process.env.FLOW_UI_E2E_PORT)
  : await getAvailablePort();
const backendURL =
  process.env.FLOW_UI_E2E_BACKEND_URL ?? `http://127.0.0.1:${await getAvailablePort()}`;
const apiKey = 'flow-control-e2e-administrator-key';
const backendDll =
  process.env.FLOW_UI_E2E_BACKEND_DLL ??
  '../../backend/Server/Server.Api/bin/Debug/net10.0/Server.Api.dll';
const children = [];

const start = (command, args, env, stdio = ['pipe', 'ignore', 'ignore']) => {
  const child = spawn(command, args, { env: { ...process.env, ...env }, stdio });
  children.push(child);
  return child;
};

const waitFor = async (url) => {
  const deadline = Date.now() + 60_000;
  while (Date.now() < deadline) {
    try {
      const response = await fetch(url);
      if (response.ok) return;
    } catch {
      // The process is still starting.
    }
    await new Promise((resolve) => setTimeout(resolve, 200));
  }
  throw new Error(`Timed out waiting for ${url}`);
};

const stop = (child) => {
  if (!child.pid) return;
  child.kill(process.platform === 'win32' ? 'SIGKILL' : 'SIGTERM');
};

try {
  if (dotnet && !process.env.FLOW_UI_E2E_BACKEND_URL) {
    start(
      'dotnet',
      [backendDll],
      {
        SERVER_ADDRESS: backendURL,
        CREDENTIAL_ENCRYPTION_KEY: 'AAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8=',
        CONTROLLER_DATA_FILE: `${process.env.TEMP}/flow-control-controllers-${process.pid}.json`,
        ConnectionStrings__FlowControl: `Data Source=${process.env.TEMP}/flow-control-e2e-${process.pid}.db`,
        ApiAccess__Identities__e2e__Key: apiKey,
        ApiAccess__Identities__e2e__Permissions__0: '*'
      }
    );
    await waitFor(`${backendURL}/api/health`);
  }
  start(
    process.execPath,
    ['./node_modules/vite/bin/vite.js', '--host', '127.0.0.1', '--port', String(port), '--strictPort'],
    {
      FLOW_UI_E2E: '1',
      VITE_FLOW_CONTROL_API_KEY: apiKey,
      ...(dotnet ? { VITE_API_PROXY: backendURL } : {})
    }
  );
  await waitFor(`http://127.0.0.1:${port}`);

  const playwrightCli = fileURLToPath(new URL('../node_modules/@playwright/test/cli.js', import.meta.url));
  const result = spawnSync(process.execPath, [playwrightCli, 'test', ...forwarded], {
    env: {
      ...process.env,
      FLOW_UI_E2E_MANAGED_SERVERS: '1',
      FLOW_UI_E2E_PORT: String(port),
      FLOW_UI_E2E_BACKEND: dotnet ? 'dotnet' : 'mock'
    },
    stdio: 'inherit'
  });
  if (result.error) throw result.error;
  process.exitCode = result.status ?? 1;
} finally {
  for (const child of children.reverse()) stop(child);
}
