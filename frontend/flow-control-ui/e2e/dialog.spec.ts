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
                  automation: 'credential-dialog',
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

  await expect(page.locator('#dialog-e2e-fixture')).toHaveAttribute('data-ready', 'true');
};

/**
 * Purpose: Protects the native modal lifecycle of AppDialog in a real browser.
 * Description: Exercises opening the component, modal presentation, and Escape dismissal.
 */
test('opens AppDialog modally and dismisses it with Escape', async ({ page }) => {
  await mountDialog(page);

  const dialog = page.locator('#credential-dialog');
  await expect(dialog).not.toBeVisible();
  await expect(dialog).toHaveAttribute('aria-label', 'Credential details');
  await expect(dialog).toHaveAttribute('data-automation', 'credential-dialog');

  await page.getByRole('button', { name: 'Open credential dialog' }).click();

  await expect(dialog).toBeVisible();
  await expect(dialog).toHaveText('Dialog content');
  await expect
    .poll(() => dialog.evaluate((element) => (element as HTMLDialogElement).open))
    .toBe(true);

  await page.keyboard.press('Escape');

  await expect(dialog).not.toBeVisible();
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
  await expect(dialog).toBeVisible();

  await page.keyboard.press('Escape');

  await expect(dialog).toBeVisible();
  await expect
    .poll(() => dialog.evaluate((element) => (element as HTMLDialogElement).open))
    .toBe(true);
});
