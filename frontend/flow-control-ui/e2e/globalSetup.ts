import type { FullConfig } from '@playwright/test';

export default async function clearSimulatorSessions(config: FullConfig): Promise<void> {
  const project = config.projects[0];
  const baseURL = project?.use.baseURL;
  if (typeof baseURL !== 'string') throw new Error('Playwright baseURL is not configured.');

  const response = await fetch(new URL('/api/simulator-sessions', baseURL), {
    method: 'DELETE',
    headers: { 'X-Api-Key': 'flow-control-e2e-administrator-key' }
  });
  if (!response.ok) {
    throw new Error(`Failed to clear simulator sessions before the test run: ${response.status}.`);
  }
}
