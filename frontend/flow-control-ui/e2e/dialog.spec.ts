import { expect, test, type Page } from '@playwright/test';

/**
 * Mount the real AppDialog component in the browser served by Vite.
 *
 * AppDialog is a reusable surface rather than part of a route, so this fixture
 * mounts the production component without adding test-only UI to the application.
 */
const mountDialog = async (page: Page, preventCancel = false): Promise<void> => {
  await page.route('/api/credentials', (route) => route.fulfill({ json: { items: [] } }));
  await page.goto('/credentials');
  await page.locator('body').evaluate(
    (body, fixture) => {
      const script = document.createElement('script');
      script.type = 'module';
      script.textContent = `
      import { createApp, h, ref } from '/node_modules/.vite/deps/vue.js';
      import AppDialog from '/src/components/AppDialog.vue';

      const fixture = ${JSON.stringify(fixture)};
      const host = document.createElement('div');
      host.id = 'dialog-e2e-fixture';
      document.body.append(host);

      createApp({
        setup() {
          const dialog = ref();
          return () =>
            h('div', [
              h(
                'button',
                {
                  type: 'button',
                  onClick: () => dialog.value.showModal()
                },
                'Open credential dialog'
              ),
              h(
                AppDialog,
                {
                  ref: dialog,
                  id: 'credential-dialog',
                  contentLabel: 'Credential details',
                  onCancel: fixture.preventCancel
                    ? (event) => event.preventDefault()
                    : undefined
                },
                { default: () => h('p', 'Dialog content') }
              )
            ]);
        }
      }).mount(host);

      host.dataset.ready = 'true';
    `;
      body.append(script);
    },
    { preventCancel }
  );

  // Expected outcome: The production dialog fixture finishes mounting before interaction.
  // Acceptance criteria: `data-ready` is `true` because the trigger and exposed dialog API
  // are usable only after Vue has mounted the fixture component tree.
  await expect(page.locator('#dialog-e2e-fixture')).toHaveAttribute('data-ready', 'true');
};

/**
 * Purpose: Protects the native modal lifecycle of AppDialog in a real browser.
 * Description: Exercises opening the component, modal presentation, and Escape dismissal.
 */
test('opens AppDialog modally and dismisses it with Escape', async ({ page }) => {
  await mountDialog(page);

  const dialog = page.locator('#credential-dialog');

  // Expected outcome: A mounted dialog remains closed until its workflow opens it.
  // Acceptance criteria: The credential dialog is not visible because mounting a reusable
  // modal must not interrupt the user without an explicit open action.
  await expect(dialog).not.toBeVisible();

  // Expected outcome: The browser exposes the dialog's caller-provided accessible name.
  // Acceptance criteria: `aria-label` is "Credential details" because assistive technology
  // must identify this otherwise heading-free modal when it opens.
  await expect(dialog).toHaveAttribute('aria-label', 'Credential details');

  // consumers need the same stable identity configured by the caller.

  await page.getByRole('button', { name: 'Open credential dialog' }).click();

  // Expected outcome: The explicit trigger presents the dialog to the user.
  // Acceptance criteria: The credential dialog is visible because one call to the exposed
  // `showModal` API must enter the native modal state.
  await expect(dialog).toBeVisible();

  // Expected outcome: The opened dialog displays its caller-provided slot content.
  // Acceptance criteria: The dialog text is "Dialog content" because AppDialog must render
  // workflow content inside the native modal rather than replacing it.
  await expect(dialog).toHaveText('Dialog content');

  // Expected outcome: The visible dialog is a native open modal, not merely styled as visible.
  // Acceptance criteria: The native `open` property is true because correct focus trapping
  // and Escape behavior depend on the platform dialog lifecycle.
  await expect
    .poll(() => dialog.evaluate((element) => (element as HTMLDialogElement).open))
    .toBe(true);

  await page.keyboard.press('Escape');

  // Expected outcome: Escape dismisses a normally dismissible dialog.
  // Acceptance criteria: The dialog is no longer visible because no caller prevented the
  // native cancel event in this standard dismissal scenario.
  await expect(dialog).not.toBeVisible();

  // Expected outcome: Escape ends the native open state.
  // Acceptance criteria: The native `open` property is false because dismissal must close
  // the platform dialog rather than only hide its rendered content.
  await expect
    .poll(() => dialog.evaluate((element) => (element as HTMLDialogElement).open))
    .toBe(false);
});

/**
 * Purpose: Protects the cancel interception needed to guard unsaved form changes.
 * Description: Exercises preventing the native cancel event and verifies the modal stays open.
 */
test('keeps AppDialog open when its cancel event is prevented', async ({ page }) => {
  await mountDialog(page, true);

  const dialog = page.locator('#credential-dialog');
  await page.getByRole('button', { name: 'Open credential dialog' }).click();

  // Expected outcome: The guarded dialog is open before cancellation is attempted.
  // Acceptance criteria: The dialog is visible because the test must exercise prevention
  // against an active native modal rather than an already closed element.
  await expect(dialog).toBeVisible();

  await page.keyboard.press('Escape');

  // Expected outcome: Preventing the cancel event keeps guarded content visible.
  // Acceptance criteria: The dialog remains visible because unsaved work must not disappear
  // when the caller rejects the Escape dismissal request.
  await expect(dialog).toBeVisible();

  // Expected outcome: Prevented cancellation preserves the native modal state.
  // Acceptance criteria: The native `open` property remains true because the caller's
  // prevention must stop the platform close operation itself.
  await expect
    .poll(() => dialog.evaluate((element) => (element as HTMLDialogElement).open))
    .toBe(true);
});
