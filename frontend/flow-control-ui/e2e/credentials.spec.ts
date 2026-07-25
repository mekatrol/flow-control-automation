import { expect, test } from '@playwright/test';

test('creates a write-only credential and never displays its secret again', async ({ page }) => {
  const credentials: Array<Record<string, unknown>> = [];
  await page.route('/api/credentials', async (route) => {
    if (route.request().method() === 'GET') {
      await route.fulfill({ json: { items: credentials } });
      return;
    }
    const input = route.request().postDataJSON() as Record<string, unknown>;
    expect(input.password).toBe('broker-secret');
    const metadata = {
      id: input.id,
      name: input.name,
      kind: input.kind,
      username: input.username,
      revision: 1,
      createdAt: '2026-07-25T00:00:00Z',
      updatedAt: '2026-07-25T00:00:00Z'
    };
    credentials.push(metadata);
    await route.fulfill({ status: 201, json: metadata });
  });

  await page.goto('/credentials');
  await page.getByLabel('Display name').fill('Plant MQTT');
  await page.getByLabel('Reference ID').fill('plant-mqtt');
  await page.getByLabel('Username').fill('flow-reader');
  const password = page.getByLabel('Password', { exact: true });
  await expect(password).toHaveAttribute('type', 'password');
  await password.fill('broker-secret');
  await page.getByRole('button', { name: 'Create credential' }).click();

  await expect(page.getByText('secret://plant-mqtt')).toBeVisible();
  await expect(page.getByLabel('Replacement password')).toHaveValue('');
  await expect(page.getByText('broker-secret')).toHaveCount(0);
  await expect(page.getByText(/Sensitive values are now hidden/)).toBeAttached();
});
