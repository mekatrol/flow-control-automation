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

  // Expected outcome: The browser fixture reports that the production component mounted.
  // Acceptance criteria: `data-ready` is `true` because tests may interact with the prompt
  // only after Vue has installed the component and its exposed modal methods.
  await expect(page.locator('#prompt-dialog-e2e-fixture')).toHaveAttribute('data-ready', 'true');
};

/**
 * Purpose: Protects the browser-level fallback decision flow used for destructive confirmations.
 * Description: Opens the standard prompt, exercises cancel and confirm in turn, and observes
 * both modal dismissal and the decision reported to its host workflow.
 */
test('runs the standard AppPromptDialog actions', async ({ page }) => {
  await mountPromptDialog(page);

  const host = page.locator('#prompt-dialog-e2e-fixture');
  const prompt = page.getByRole('dialog', { name: 'Discard changes' });

  await page.getByRole('button', { name: 'Open prompt' }).click();

  // Expected outcome: Opening the standard prompt creates a visible modal decision.
  // Acceptance criteria: The "Discard changes" dialog is visible because the exposed
  // `showModal` operation must present the fallback prompt before either action is possible.
  await expect(prompt).toBeVisible();
  await page.getByRole('button', { name: 'Keep editing' }).click();

  // Expected outcome: Choosing the safe action dismisses the standard prompt.
  // Acceptance criteria: The prompt is not visible because cancellation completes the
  // modal decision while preserving the underlying work.
  await expect(prompt).not.toBeVisible();

  // Expected outcome: The host workflow receives the cancellation decision.
  // Acceptance criteria: `data-result` is `cancel` because "Keep editing" must invoke
  // the cancel callback rather than the destructive confirmation callback.
  await expect(host).toHaveAttribute('data-result', 'cancel');

  await page.getByRole('button', { name: 'Open prompt' }).click();
  await page.getByRole('button', { name: 'Discard changes' }).click();

  // Expected outcome: Choosing the destructive action dismisses the standard prompt.
  // Acceptance criteria: The prompt is not visible because confirmation is a terminal
  // modal decision and must return control to the host workflow.
  await expect(prompt).not.toBeVisible();

  // Expected outcome: The host workflow receives the confirmation decision.
  // Acceptance criteria: `data-result` is `confirm` because "Discard changes" must
  // authorize the destructive branch rather than report cancellation.
  await expect(host).toHaveAttribute('data-result', 'confirm');
});

/**
 * Purpose: Protects custom prompt content without bypassing AppPromptDialog's lifecycle contract.
 * Description: Mounts a workflow-specific slot, activates both supplied callbacks, and observes
 * the corresponding decisions reported to the browser fixture host.
 */
test('provides lifecycle callbacks to a custom prompt slot', async ({ page }) => {
  await mountPromptDialog(page, true);

  const host = page.locator('#prompt-dialog-e2e-fixture');
  await page.getByRole('button', { name: 'Open prompt' }).click();

  // Expected outcome: The custom slot supplies the visible workflow-specific question.
  // Acceptance criteria: "Replace existing value?" is visible because custom content
  // must replace the fallback discard wording while remaining inside the modal.
  await expect(page.getByRole('heading', { name: 'Replace existing value?' })).toBeVisible();
  await page.getByRole('button', { name: 'Retain value' }).click();

  // Expected outcome: The custom safe action invokes the supplied cancel callback.
  // Acceptance criteria: `data-result` is `cancel` because retaining the existing value
  // must reject replacement and preserve the current workflow state.
  await expect(host).toHaveAttribute('data-result', 'cancel');

  await page.getByRole('button', { name: 'Open prompt' }).click();
  await page.getByRole('button', { name: 'Replace value' }).click();

  // Expected outcome: The custom destructive action invokes the supplied confirm callback.
  // Acceptance criteria: `data-result` is `confirm` because replacing the value explicitly
  // authorizes the custom prompt's destructive branch.
  await expect(host).toHaveAttribute('data-result', 'confirm');
});
