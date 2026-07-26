import { expect, test } from '@playwright/test';

/**
 * Purpose: Protects the behavioral contract that creates a write-only credential and never displays its secret again.
 * Description: Exercises creates a write-only credential and never displays its secret again from its arranged starting state and
 * verifies the observable results required by the scenario.
 */
test('creates a write-only credential and never displays its secret again', async ({ page }) => {
  const credentials: Array<Record<string, unknown>> = [];
  await page.route('/api/credentials', async (route) => {
    if (route.request().method() === 'GET') {
      await route.fulfill({ json: { items: credentials } });
      return;
    }
    const input = route.request().postDataJSON() as Record<string, unknown>;

    // Expected outcome: `input.password` has the required value.
    // Acceptance criteria: `input.password` must be `'broker-secret'`, because this condition proves that
    // creates a write-only credential and never displays its secret again.
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

  // Expected outcome: `password` exposes the required attribute.
  // Acceptance criteria: `password` must have attribute arguments `'type', 'password'`, because this condition proves that
  // creates a write-only credential and never displays its secret again.
  await expect(password).toHaveAttribute('type', 'password');

  // Expected outcome: `page.getByRole('button', { name: 'Show password' })` resolves to the required number of elements.
  // Acceptance criteria: `page.getByRole('button', { name: 'Show password' })` must resolve to exactly 0 elements, because this condition proves that
  // creates a write-only credential and never displays its secret again.
  await expect(page.getByRole('button', { name: 'Show password' })).toHaveCount(0);
  await password.fill('broker-secret');
  const showPassword = page.getByRole('button', { name: 'Show password' });

  // Expected outcome: `showPassword` is visible to the user.
  // Acceptance criteria: `showPassword` must be visible, because this condition proves that
  // creates a write-only credential and never displays its secret again.
  await expect(showPassword).toBeVisible();
  await showPassword.click();

  // Expected outcome: `password` exposes the required attribute.
  // Acceptance criteria: `password` must have attribute arguments `'type', 'text'`, because this condition proves that
  // creates a write-only credential and never displays its secret again.
  await expect(password).toHaveAttribute('type', 'text');
  await page.getByRole('button', { name: 'Hide password' }).click();

  // Expected outcome: `password` exposes the required attribute.
  // Acceptance criteria: `password` must have attribute arguments `'type', 'password'`, because this condition proves that
  // creates a write-only credential and never displays its secret again.
  await expect(password).toHaveAttribute('type', 'password');
  await page.getByRole('button', { name: 'Create credential' }).click();

  const savedCredentials = page.getByLabel('Saved credentials');

  // Expected outcome: the saved credential reference is visible in the credential list.
  // Acceptance criteria: the saved credential list must contain `secret://plant-mqtt`, because this condition proves that
  // creates a write-only credential and never displays its secret again.
  await expect(savedCredentials.getByText('secret://plant-mqtt', { exact: true })).toBeVisible();

  // Expected outcome: `page.getByLabel('Replacement password')` contains the required input value.
  // Acceptance criteria: `page.getByLabel('Replacement password')` must have value `''`, because this condition proves that
  // creates a write-only credential and never displays its secret again.
  await expect(page.getByLabel('Replacement password')).toHaveValue('');

  // Expected outcome: `page.getByRole('button', { name: 'Show password' })` resolves to the required number of elements.
  // Acceptance criteria: `page.getByRole('button', { name: 'Show password' })` must resolve to exactly 0 elements, because this condition proves that
  // creates a write-only credential and never displays its secret again.
  await expect(page.getByRole('button', { name: 'Show password' })).toHaveCount(0);

  // Expected outcome: `page.getByText('broker-secret')` resolves to the required number of elements.
  // Acceptance criteria: `page.getByText('broker-secret')` must resolve to exactly 0 elements, because this condition proves that
  // creates a write-only credential and never displays its secret again.
  await expect(page.getByText('broker-secret')).toHaveCount(0);

  // Expected outcome: `page.getByText(/Sensitive values are now hidden/)` is present in the rendered document.
  // Acceptance criteria: `page.getByText(/Sensitive values are now hidden/)` must be attached to the document, because this condition proves that
  // creates a write-only credential and never displays its secret again.
  await expect(page.getByText(/Sensitive values are now hidden/)).toBeAttached();
});
