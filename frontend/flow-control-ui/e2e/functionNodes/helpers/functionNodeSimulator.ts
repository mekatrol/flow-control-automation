import { expect, type Page } from '@playwright/test';

export const startSimulation = async (page: Page, flowId: string): Promise<void> => {
  await page.getByRole('link', { name: 'Simulate' }).click();
  await expect(page).toHaveURL(new RegExp(`/flows/${flowId}/simulator$`));
  const started = page.waitForResponse(
    (response) =>
      response.request().method() === 'POST' &&
      new URL(response.url()).pathname === `/api/flows/${flowId}/simulator-sessions` &&
      response.status() === 201
  );
  await page.getByRole('button', { name: 'Start simulation' }).click();
  await started;
  await expect(page.getByLabel('Simulation controls').getByRole('status')).toHaveText('running');
};

export const applyAnalogInputs = async (
  page: Page,
  values: Record<string, number>
): Promise<void> => {
  const panel = page.getByRole('complementary', { name: 'Simulation points' });
  for (const [pointId, value] of Object.entries(values)) {
    const input = panel.getByRole('textbox', { name: `${pointId} simulated value` });
    await input.click();
    await input.press('ControlOrMeta+A');
    await input.pressSequentially(String(value));
    await expect(input).toHaveValue(String(value));
  }
  const applied = page.waitForResponse(
    (response) =>
      response.request().method() === 'POST' &&
      new URL(response.url()).pathname.endsWith('/apply-and-step') &&
      response.ok()
  );
  await panel.getByRole('button', { name: 'Apply' }).click();
  await applied;
};

export const expectAnalogOutput = async (
  page: Page,
  pointId: string,
  expected: number
): Promise<void> => {
  const panel = page.getByRole('complementary', { name: 'Simulation points' });
  const row = panel.locator('.point-row').filter({ hasText: pointId });
  await expect(row.getByRole('status')).toHaveText(String(expected));
};
