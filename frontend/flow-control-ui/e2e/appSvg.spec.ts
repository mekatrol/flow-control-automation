import { expect, test, type Page } from '@playwright/test';

const mountAppSvg = async (page: Page): Promise<void> => {
  await page.route('/api/credentials', (route) => route.fulfill({ json: { items: [] } }));
  await page.goto('/credentials');
  const welcome = page.getByRole('dialog', { name: 'Welcome to Flow Control' });
  if (await welcome.isVisible()) await welcome.getByRole('button', { name: 'Close' }).click();

  await page.locator('body').evaluate((body) => {
    const script = document.createElement('script');
    script.type = 'module';
    script.textContent = `
      import { createApp, h, ref } from '/node_modules/.vite/deps/vue.js';
      import AppSvg from '/src/components/AppSvg.vue';

      const host = document.createElement('div');
      host.id = 'app-svg-e2e-fixture';
      document.body.append(host);

      createApp({
        setup() {
          const source = ref('/src/assets/icons/info-notice-icon.svg');
          const size = ref(20);
          return () => h('section', [
            h(AppSvg, {
              src: source.value,
              size: size.value,
              automation: 'dynamic-svg',
              label: 'Current notice type'
            }),
            h('button', {
              type: 'button',
              onClick: () => {
                source.value = '/src/assets/icons/warning-notice-icon.svg';
                size.value = 32;
              }
            }, 'Change icon')
          ]);
        }
      }).mount(host);
      host.dataset.ready = 'true';
    `;
    body.append(script);
  });

  // Expected outcome: The browser fixture reports that the production component mounted.
  // Acceptance criteria: `data-ready` is `true` because runtime interaction must wait until
  // Vue has installed AppSvg and established its reactive bindings.
  await expect(page.locator('#app-svg-e2e-fixture')).toHaveAttribute('data-ready', 'true');
};

/**
 * Purpose: Protects reactive SVG masking in a real browser rather than only Vue's DOM model.
 * Description: Renders an informative icon, changes its source and size at runtime, and observes
 * browser-computed mask and dimensions without replacing its automation target.
 */
test('updates an AppSvg source and size at runtime', async ({ page }) => {
  await mountAppSvg(page);
  const icon = page.locator('[data-automation="dynamic-svg"]');

  // Expected outcome: The initial icon has accessible image semantics.
  // Acceptance criteria: Its accessible name is "Current notice type" because meaningful
  // SVGs must expose the label supplied by their caller.
  await expect(icon).toHaveAccessibleName('Current notice type');

  // Expected outcome: The initial numeric size is normalized in the browser.
  // Acceptance criteria: The element is 20 by 20 pixels because the arranged `size` is 20
  // and AppSvg applies it equally to both dimensions.
  await expect(icon).toHaveCSS('width', '20px');

  await page.getByRole('button', { name: 'Change icon' }).click();

  // Expected outcome: A runtime size change updates the existing icon element.
  // Acceptance criteria: The element becomes 32 by 32 pixels because reactive size changes
  // must apply without requiring the parent to remount AppSvg.
  await expect(icon).toHaveCSS('width', '32px');

  // Expected outcome: A runtime source change replaces the browser's mask image.
  // Acceptance criteria: The computed mask contains `warning-notice-icon.svg` because the
  // reactive source now identifies the warning artwork.
  await expect
    .poll(() => icon.evaluate((element) => getComputedStyle(element).maskImage))
    .toContain('warning-notice-icon.svg');
});
