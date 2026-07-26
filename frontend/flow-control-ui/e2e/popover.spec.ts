import { expect, test, type Page } from '@playwright/test';

/**
 * Mount the real AppPopover component in the browser served by Vite.
 *
 * AppPopover is intentionally a reusable surface rather than part of a specific
 * route, so this fixture gives the browser a trigger and mounts the production
 * component without adding test-only UI to the application.
 */
const mountPopover = async (
  page: Page,
  options: {
    id: string;
    triggerTarget: string;
    popoverMode?: 'auto' | 'manual';
  }
): Promise<void> => {
  // Keep the host route deterministic; the fixture does not depend on backend data.
  await page.route('/api/credentials', (route) => route.fulfill({ json: { items: [] } }));
  await page.goto('/credentials');
  await page.locator('body').evaluate((body, fixture) => {
    const script = document.createElement('script');
    script.type = 'module';
    script.textContent = `
        import { createApp, h } from '/node_modules/.vite/deps/vue.js';
        import AppPopover from '/src/components/AppPopover.vue';

        const fixture = ${JSON.stringify(fixture)};
        const host = document.createElement('div');
        host.id = 'popover-e2e-fixture';
        document.body.append(host);

        const trigger = document.createElement('button');
        trigger.type = 'button';
        trigger.textContent = 'Open app options';
        trigger.setAttribute('popovertarget', fixture.triggerTarget);
        host.append(trigger);

        const popoverHost = document.createElement('div');
        host.append(popoverHost);
        createApp({
          render: () =>
            h(
              AppPopover,
              {
                id: fixture.id,
                contentLabel: 'App options',
                popoverMode: fixture.popoverMode ?? 'auto'
              },
              { default: () => h('p', 'Popover content') }
            )
        }).mount(popoverHost);

        host.dataset.ready = 'true';
      `;
    body.append(script);
  }, options);

  // Expected outcome: `page.locator('#popover-e2e-fixture')` exposes the required attribute.
  // Acceptance criteria: `page.locator('#popover-e2e-fixture')` must have attribute arguments `'data-ready', 'true'`, because this condition proves that
  // the arranged test scenario.
  await expect(page.locator('#popover-e2e-fixture')).toHaveAttribute('data-ready', 'true');
};

// Positive test: a correctly connected native trigger should open the real
// AppPopover, expose its content, and allow Escape to restore the closed state.

/**
 * Purpose: Protects the behavioral contract that opens and dismisses an AppPopover connected to a valid trigger.
 * Description: Exercises opens and dismisses an AppPopover connected to a valid trigger from its arranged starting state and
 * verifies the observable results required by the scenario.
 */
test('opens and dismisses an AppPopover connected to a valid trigger', async ({ page }) => {
  const popover = page.locator('#app-options-popover');
  const trigger = page.getByRole('button', { name: 'Open app options' });

  await test.step('Mount an auto popover with a trigger targeting its ID', async () => {
    await mountPopover(page, {
      id: 'app-options-popover',
      triggerTarget: 'app-options-popover'
    });

    // Expected result: native popovers start closed and are not visible.

    // Expected outcome: `popover` is visible to the user.
    // Acceptance criteria: `popover` must be visible, because this condition proves that
    // opens and dismisses an AppPopover connected to a valid trigger.
    await expect(popover).not.toBeVisible();

    // Expected outcome: `popover` exposes the required attribute.
    // Acceptance criteria: `popover` must have attribute arguments `'role', 'dialog'`, because this condition proves that
    // opens and dismisses an AppPopover connected to a valid trigger.
    await expect(popover).toHaveAttribute('role', 'dialog');

    // Expected outcome: `popover` exposes the required attribute.
    // Acceptance criteria: `popover` must have attribute arguments `'aria-label', 'App options'`, because this condition proves that
    // opens and dismisses an AppPopover connected to a valid trigger.
    await expect(popover).toHaveAttribute('aria-label', 'App options');
  });

  await test.step('Activate the correctly connected trigger', async () => {
    await trigger.click();

    // Expected result: the browser opens the popover and reveals its slotted content.

    // Expected outcome: `popover` is visible to the user.
    // Acceptance criteria: `popover` must be visible, because this condition proves that
    // opens and dismisses an AppPopover connected to a valid trigger.
    await expect(popover).toBeVisible();

    // Expected outcome: `popover` displays the required text.
    // Acceptance criteria: `popover` must display `'Popover content'`, because this condition proves that
    // opens and dismisses an AppPopover connected to a valid trigger.
    await expect(popover).toHaveText('Popover content');

    // Expected outcome: `popover` uses the required rendered style.
    // Acceptance criteria: `popover` must have CSS arguments `'opacity', '1'`, because this condition proves that
    // opens and dismisses an AppPopover connected to a valid trigger.
    await expect(popover).toHaveCSS('opacity', '1');
  });

  await test.step('Press Escape to dismiss the open auto popover', async () => {
    await page.keyboard.press('Escape');

    // Expected result: native Escape handling closes the popover without application JavaScript.

    // Expected outcome: `popover` is visible to the user.
    // Acceptance criteria: `popover` must be visible, because this condition proves that
    // opens and dismisses an AppPopover connected to a valid trigger.
    await expect(popover).not.toBeVisible();
  });
});

// Negative test: a trigger with an incorrect target must not accidentally open
// another popover or reveal content intended to remain hidden.

/**
 * Purpose: Protects the behavioral contract that does not open AppPopover when the trigger target does not match.
 * Description: Exercises does not open AppPopover when the trigger target does not match from its arranged starting state and
 * verifies the observable results required by the scenario.
 */
test('does not open AppPopover when the trigger target does not match', async ({ page }) => {
  const popover = page.locator('#app-options-popover');

  await test.step('Mount a popover whose trigger references a missing ID', async () => {
    await mountPopover(page, {
      id: 'app-options-popover',
      triggerTarget: 'missing-popover'
    });

    // Expected result: the valid popover remains closed before any interaction.

    // Expected outcome: `popover` is visible to the user.
    // Acceptance criteria: `popover` must be visible, because this condition proves that
    // does not open AppPopover when the trigger target does not match.
    await expect(popover).not.toBeVisible();
  });

  await test.step('Activate the incorrectly connected trigger', async () => {
    await page.getByRole('button', { name: 'Open app options' }).click();

    // Expected result: an invalid target cannot reveal the AppPopover.

    // Expected outcome: `popover` is visible to the user.
    // Acceptance criteria: `popover` must be visible, because this condition proves that
    // does not open AppPopover when the trigger target does not match.
    await expect(popover).not.toBeVisible();
    await expect
      .poll(() => popover.evaluate((element) => element.matches(':popover-open')))
      .toBe(false);
  });
});
