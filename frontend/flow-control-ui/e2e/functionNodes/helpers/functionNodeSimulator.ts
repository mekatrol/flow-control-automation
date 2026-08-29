import { expect, type Page } from '@playwright/test';

export interface StartedSimulation {
  flowId: string;
  sessionId: string;
}

export const startSimulation = async (
  page: Page,
  flowId: string
): Promise<StartedSimulation> => {
  await page.getByRole('link', { name: 'Simulate' }).click();
  await expect(page).toHaveURL(new RegExp(`/flows/${flowId}/simulator$`));
  const started = page.waitForResponse(
    (response) =>
      response.request().method() === 'POST' &&
      new URL(response.url()).pathname === `/api/flows/${flowId}/simulator-sessions`
  );
  await page.getByRole('button', { name: 'Start simulation' }).click();
  const response = await started;
  const body: unknown = await response.json();
  expect(response.status(), JSON.stringify(body)).toBe(201);
  expect(body).toEqual(
    expect.objectContaining({
      flowId,
      sessionId: expect.any(String)
    })
  );
  const simulation = { flowId, sessionId: (body as { sessionId: string }).sessionId };
  try {
    await expect(page.getByLabel('Simulation controls').getByRole('status')).toHaveText('running');
    return simulation;
  } catch (error) {
    // The backend session already exists at this point, but the caller cannot
    // clean it up until startSimulation returns. Release it here when the UI
    // fails to reach its expected state so repeated Test Explorer runs do not
    // exhaust the active-emulator limit.
    await stopSimulation(page, simulation);
    throw error;
  }
};

export const stopSimulation = async (
  page: Page,
  simulation: StartedSimulation
): Promise<void> => {
  const path = `/api/flows/${encodeURIComponent(simulation.flowId)}/simulator-sessions/${encodeURIComponent(simulation.sessionId)}`;
  const stopped = page.waitForResponse(
    (response) =>
      response.request().method() === 'DELETE' && new URL(response.url()).pathname === path
  );
  await page.getByRole('button', { name: 'Stop simulation' }).click();
  const response = await stopped;
  if (![204, 404].includes(response.status())) {
    const body = await response.text().catch(() => '<response body unavailable>');
    throw new Error(`Failed to stop simulator session ${simulation.sessionId}: ${body}`);
  }
  await expect(page.getByLabel('Simulation controls').getByRole('status')).toHaveText('stopped');
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
  const response = await applied;
  const body = (await response.json()) as {
    io?: { inputs?: Array<{ pointId: string; typedValue: { boolean: boolean; number: number } }> };
  };
  const request = response.request().postDataJSON() as {
    inputs: Array<{ inputId: string; typedValue: { boolean: boolean; number: number } }>;
  };
  for (const [pointId, expected] of Object.entries(values)) {
    const submitted = request.inputs.find(({ inputId }) => inputId === pointId)?.typedValue;
    expect(submitted, `Apply must submit ${pointId}.`).toBeDefined();
    expect(typeof expected === 'boolean' ? submitted!.boolean : submitted!.number).toBe(expected);
    const appliedValue = body.io?.inputs?.find(({ pointId: id }) => id === pointId)?.typedValue;
    expect(appliedValue, `Simulator must apply ${pointId} before stepping.`).toBeDefined();
    expect(typeof expected === 'boolean' ? appliedValue!.boolean : appliedValue!.number).toBe(expected);
  }
};

export const applyInputs = async (
  page: Page,
  values: Record<string, boolean | number>
): Promise<void> => {
  const panel = page.getByRole('complementary', { name: 'Simulation points' });
  for (const [pointId, value] of Object.entries(values)) {
    if (typeof value === 'boolean') {
      const input = panel.getByRole('checkbox', { name: `${pointId} simulated value` });
      if ((await input.isChecked()) !== value) await input.click();
      await expect(input).toBeChecked({ checked: value });
    } else {
      const input = panel.getByRole('textbox', { name: `${pointId} simulated value` });
      await input.click();
      await input.press('ControlOrMeta+A');
      await input.pressSequentially(String(value));
      await expect(input).toHaveValue(String(value));
    }
  }
  const applied = page.waitForResponse(
    (response) =>
      response.request().method() === 'POST' &&
      new URL(response.url()).pathname.endsWith('/apply-and-step') &&
      response.ok()
  );
  await panel.getByRole('button', { name: 'Apply' }).click();
  const response = await applied;
  const body = (await response.json()) as {
    io?: { inputs?: Array<{ pointId: string; typedValue: { boolean: boolean; number: number } }> };
  };
  const request = response.request().postDataJSON() as {
    inputs: Array<{ inputId: string; typedValue: { boolean: boolean; number: number } }>;
  };
  for (const [pointId, expected] of Object.entries(values)) {
    const submitted = request.inputs.find(({ inputId }) => inputId === pointId)?.typedValue;
    expect(submitted, `Apply must submit ${pointId}.`).toBeDefined();
    expect(typeof expected === 'boolean' ? submitted!.boolean : submitted!.number).toBe(expected);
    const appliedValue = body.io?.inputs?.find(({ pointId: id }) => id === pointId)?.typedValue;
    expect(appliedValue, `Simulator must apply ${pointId} before stepping.`).toBeDefined();
    expect(typeof expected === 'boolean' ? appliedValue!.boolean : appliedValue!.number).toBe(expected);
  }
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

export const expectOutput = async (
  page: Page,
  pointId: string,
  expected: boolean | number
): Promise<void> => {
  const panel = page.getByRole('complementary', { name: 'Simulation points' });
  const row = panel.locator('.point-row').filter({ hasText: pointId });
  await expect(row.getByRole('status')).toHaveText(
    typeof expected === 'boolean' ? (expected ? 'On' : 'Off') : String(expected)
  );
};
