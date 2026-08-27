import { expect, test, type Page } from '@playwright/test';

const mountAppNotice = async (page: Page): Promise<void> => {
  await page.route('/api/credentials', (route) => route.fulfill({ json: { items: [] } }));
  await page.goto('/credentials');
  await page.locator('body').evaluate((body) => {
    const script = document.createElement('script');
    script.type = 'module';
    script.textContent = `
      import { createApp, h, ref } from '/node_modules/.vite/deps/vue.js';
      import AppNotice from '/src/components/AppNotice.vue';

      const host = document.createElement('div');
      host.id = 'app-error-e2e-fixture';
      document.body.append(host);
      Object.defineProperty(navigator, 'clipboard', {
        configurable: true,
        value: { writeText: async (text) => { host.dataset.clipboard = text; } }
      });

      createApp({
        setup() {
          const notice = ref();
          return () => h('div', [
            h('button', { type: 'button', onClick: () => notice.value.showModal() }, 'Show error'),
            h(AppNotice, {
              ref: notice,
              id: 'runtime-error',
              title: 'Runtime failed',
              message: 'Fallback message',
              variant: 'error'
            }, {
              content: () => h('p', [
                'Open ',
                h('a', { href: '/flows' }, 'flow diagnostics'),
                ' for incident 42.'
              ])
            })
          ]);
        }
      }).mount(host);
      host.dataset.ready = 'true';
    `;
    body.append(script);
  });

  // Expected outcome: The browser fixture reports that the production component mounted.
  // Acceptance criteria: `data-ready` is `true` because interaction must wait until Vue has
  // installed AppNotice and its exposed modal lifecycle.
  await expect(page.locator('#app-error-e2e-fixture')).toHaveAttribute('data-ready', 'true');
};

/**
 * Purpose: Protects the real-browser modal, rich-content, clipboard, and keyboard behavior.
 * Description: Opens an error overlay, copies linked details, and dismisses it with Escape while
 * observing the accessible dialog and plain-text clipboard payload.
 */
test('presents and operates the AppNotice overlay', async ({ page }) => {
  await mountAppNotice(page);
  const host = page.locator('#app-error-e2e-fixture');
  const dialog = page.getByRole('dialog', { name: 'Runtime failed' });

  await page.getByRole('button', { name: 'Show error' }).click();

  // Expected outcome: The error appears as a browser-managed whole-view modal.
  // Acceptance criteria: The "Runtime failed" dialog is visible because `showModal` must
  // place the AppDialog and its backdrop over the active application view.
  await expect(dialog).toBeVisible();

  // Expected outcome: Rich error content preserves its actionable link.
  // Acceptance criteria: The "flow diagnostics" link targets `/flows` because callers must
  // be able to provide navigable help within the content slot.
  await expect(dialog.getByRole('link', { name: 'flow diagnostics' })).toHaveAttribute(
    'href',
    '/flows'
  );

  await dialog.getByRole('button', { name: 'Copy details' }).click();

  // Expected outcome: Copying linked details produces a plain-text diagnostic.
  // Acceptance criteria: The fixture receives the rendered sentence without HTML because
  // AppNotice must make clipboard output portable to logs, tickets, and messages.
  await expect(host).toHaveAttribute('data-clipboard', 'Open flow diagnostics for incident 42.');

  await page.keyboard.press('Escape');

  // Expected outcome: Keyboard cancellation dismisses a dismissible error overlay.
  // Acceptance criteria: The dialog is hidden after Escape because AppDialog must preserve
  // native keyboard behavior and restore access to the underlying view.
  await expect(dialog).not.toBeVisible();
});
