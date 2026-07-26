import { expect, test, type Page } from '@playwright/test';

const mountPromptDialog = async (page: Page, customPrompt = false): Promise<void> => {
  await page.route('/api/credentials', (route) => route.fulfill({ json: { items: [] } }));
  await page.goto('/credentials');
  await page.locator('body').evaluate(
    (body, fixture) => {
      const script = document.createElement('script');
      script.type = 'module';
      script.textContent = `
      import { createApp, h, ref } from '/node_modules/.vite/deps/vue.js';
      import AppPromptDialog from '/src/components/AppPromptDialog.vue';

      const fixture = ${JSON.stringify(fixture)};
      const host = document.createElement('div');
      host.id = 'prompt-dialog-e2e-fixture';
      document.body.append(host);

      createApp({
        setup() {
          const prompt = ref();
          const slots = fixture.customPrompt
            ? {
                prompt: ({ cancel, confirm }) =>
                  h('section', [
                    h('h2', 'Replace existing value?'),
                    h('button', { onClick: cancel }, 'Retain value'),
                    h('button', { onClick: confirm }, 'Replace value')
                  ])
              }
            : undefined;

          return () =>
            h('div', [
              h(
                'button',
                { type: 'button', onClick: () => prompt.value.showModal() },
                'Open prompt'
              ),
              h(
                AppPromptDialog,
                {
                  ref: prompt,
                  id: 'app-prompt',
                  contentLabel: 'Discard changes',
                  automation: 'app-prompt',
                  onCancel: () => (host.dataset.result = 'cancel'),
                  onConfirm: () => (host.dataset.result = 'confirm')
                },
                slots
              )
            ]);
        }
      }).mount(host);

      host.dataset.ready = 'true';
    `;
      body.append(script);
    },
    { customPrompt }
  );

  await expect(page.locator('#prompt-dialog-e2e-fixture')).toHaveAttribute('data-ready', 'true');
};

test('runs the standard AppPromptDialog actions', async ({ page }) => {
  await mountPromptDialog(page);

  const host = page.locator('#prompt-dialog-e2e-fixture');
  const prompt = page.getByRole('dialog', { name: 'Discard changes' });

  await page.getByRole('button', { name: 'Open prompt' }).click();
  await expect(prompt).toBeVisible();
  await page.getByRole('button', { name: 'Keep editing' }).click();
  await expect(prompt).not.toBeVisible();
  await expect(host).toHaveAttribute('data-result', 'cancel');

  await page.getByRole('button', { name: 'Open prompt' }).click();
  await page.getByRole('button', { name: 'Discard changes' }).click();
  await expect(prompt).not.toBeVisible();
  await expect(host).toHaveAttribute('data-result', 'confirm');
});

test('provides lifecycle callbacks to a custom prompt slot', async ({ page }) => {
  await mountPromptDialog(page, true);

  const host = page.locator('#prompt-dialog-e2e-fixture');
  await page.getByRole('button', { name: 'Open prompt' }).click();
  await expect(page.getByRole('heading', { name: 'Replace existing value?' })).toBeVisible();
  await page.getByRole('button', { name: 'Retain value' }).click();
  await expect(host).toHaveAttribute('data-result', 'cancel');

  await page.getByRole('button', { name: 'Open prompt' }).click();
  await page.getByRole('button', { name: 'Replace value' }).click();
  await expect(host).toHaveAttribute('data-result', 'confirm');
});
