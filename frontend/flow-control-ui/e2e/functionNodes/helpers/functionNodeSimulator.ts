import { expect, type Page } from '@playwright/test';

export interface StartedSimulation {
  flowId: string;
  sessionId: string;
}

export const startSimulation = async (page: Page, flowId: string): Promise<StartedSimulation> => {
  await page.getByRole('link', { name: 'Simulate' }).click();
  await expect(page).toHaveURL(new RegExp(`/flows/${flowId}/simulator$`));
  const started = page.waitForResponse(
    (response) =>
      response.request().method() === 'POST' &&
      new URL(response.url()).pathname === `/api/flows/${flowId}/execution-contexts`
  );
  await page.getByRole('button', { name: 'Create context' }).click();
  const response = await started;
  const responseText = await response.text();
  let body: unknown;
  try {
    body = JSON.parse(responseText);
  } catch {
    throw new Error(
      `Starting simulator session for ${flowId} returned ${response.status()} with ${responseText ? `a non-JSON body: ${responseText}` : 'an empty body'}.`
    );
  }
  expect(response.status(), responseText).toBe(201);
  expect(body).toEqual(
    expect.objectContaining({
      flowId,
      id: expect.any(String)
    })
  );
  const simulation = { flowId, sessionId: (body as { id: string }).id };
  await expect(page.getByLabel('Flow debugging').getByRole('status')).toHaveText('ready');
  return simulation;
};

export const stopSimulation = async (page: Page, simulation: StartedSimulation): Promise<void> => {
  const path = `/api/execution-contexts/${encodeURIComponent(simulation.sessionId)}/stop`;
  const response = await page.request.post(path);
  if (![200, 204, 404].includes(response.status())) {
    const body = await response.text().catch(() => '<response body unavailable>');
    throw new Error(`Failed to stop simulator session ${simulation.sessionId}: ${body}`);
  }
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
  const submitted = page.waitForResponse(
    (response) =>
      response.request().method() === 'PUT' &&
      new URL(response.url()).pathname.endsWith('/inputs')
  );
  const applied = page.waitForResponse(
    (response) =>
      response.request().method() === 'POST' &&
      new URL(response.url()).pathname.endsWith('/step-tick')
  );
  await panel.getByRole('button', { name: 'Apply' }).click();
  const inputResponse = await submitted;
  const response = await applied;
  if (!inputResponse.ok()) throw new Error(await inputResponse.text());
  if (!response.ok()) throw new Error(await response.text());
  const body = (await response.json()) as {
    io?: { inputs?: Array<{ pointId: string; typedValue: { boolean: boolean; number: number } }> };
  };
  const request = inputResponse.request().postDataJSON() as {
    inputs: Array<{ inputId: string; typedValue: { boolean: boolean; number: number } }>;
  };
  for (const [pointId, expected] of Object.entries(values)) {
    const submitted = request.inputs.find(
      ({ inputId }) => inputId === pointId || inputId.endsWith(`/${pointId}`)
    )?.typedValue;
    expect(submitted, `Apply must submit ${pointId}.`).toBeDefined();
    expect(typeof expected === 'boolean' ? submitted!.boolean : submitted!.number).toBe(expected);
    const appliedValue = body.io?.inputs?.find(
      ({ pointId: id }) => id === pointId || id.endsWith(`/${pointId}`)
    )?.typedValue;
    expect(appliedValue, `Simulator must apply ${pointId} before stepping.`).toBeDefined();
    expect(typeof expected === 'boolean' ? appliedValue!.boolean : appliedValue!.number).toBe(
      expected
    );
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
  const submitted = page.waitForResponse(
    (response) =>
      response.request().method() === 'PUT' &&
      new URL(response.url()).pathname.endsWith('/inputs')
  );
  const applied = page.waitForResponse(
    (response) =>
      response.request().method() === 'POST' &&
      new URL(response.url()).pathname.endsWith('/step-tick')
  );
  await panel.getByRole('button', { name: 'Apply' }).click();
  const inputResponse = await submitted;
  const response = await applied;
  if (!inputResponse.ok()) throw new Error(await inputResponse.text());
  if (!response.ok()) throw new Error(await response.text());
  const body = (await response.json()) as {
    io?: { inputs?: Array<{ pointId: string; typedValue: { boolean: boolean; number: number } }> };
  };
  const request = inputResponse.request().postDataJSON() as {
    inputs: Array<{ inputId: string; typedValue: { boolean: boolean; number: number } }>;
  };
  for (const [pointId, expected] of Object.entries(values)) {
    const submitted = request.inputs.find(
      ({ inputId }) => inputId === pointId || inputId.endsWith(`/${pointId}`)
    )?.typedValue;
    expect(submitted, `Apply must submit ${pointId}.`).toBeDefined();
    expect(typeof expected === 'boolean' ? submitted!.boolean : submitted!.number).toBe(expected);
    const appliedValue = body.io?.inputs?.find(
      ({ pointId: id }) => id === pointId || id.endsWith(`/${pointId}`)
    )?.typedValue;
    expect(appliedValue, `Simulator must apply ${pointId} before stepping.`).toBeDefined();
    expect(typeof expected === 'boolean' ? appliedValue!.boolean : appliedValue!.number).toBe(
      expected
    );
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
