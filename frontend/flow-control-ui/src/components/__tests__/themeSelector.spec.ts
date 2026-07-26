// @vitest-environment jsdom

import { mount } from '@vue/test-utils';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import AppThemeSelector from '@/components/AppThemeSelector.vue';

describe('ThemeSelector', () => {
  beforeEach(() => {
    const values = new Map<string, string>();
    vi.stubGlobal('localStorage', {
      clear: () => values.clear(),
      getItem: (key: string) => values.get(key) ?? null,
      setItem: (key: string, value: string) => values.set(key, value)
    });
    document.documentElement.removeAttribute('data-theme');
    document.documentElement.removeAttribute('data-theme-preference');
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    document.documentElement.removeAttribute('data-theme');
    document.documentElement.removeAttribute('data-theme-preference');
  });

  /**
   * Purpose: Protects the behavioral contract that cycles through system, dark, and light with accessible state descriptions.
   * Description: Exercises cycles through system, dark, and light with accessible state descriptions from its arranged starting state and
   * verifies the observable results required by the scenario.
   */
  it('cycles through system, dark, and light with accessible state descriptions', async () => {
    const wrapper = mount(AppThemeSelector);
    const selector = wrapper.get<HTMLButtonElement>('.theme-selector');
    const status = wrapper.get('[role="status"]');

    // Expected outcome: `selector.attributes('data-theme-preference')` has the required value.
    // Acceptance criteria: `selector.attributes('data-theme-preference')` must be `'system'`, because this condition proves that
    // cycles through system, dark, and light with accessible state descriptions.
    expect(selector.attributes('data-theme-preference')).toBe('system');

    // Expected outcome: `selector.attributes('aria-label')` has the required value.
    // Acceptance criteria: `selector.attributes('aria-label')` must be `'Theme preference: System. Activate to use Dark theme'`, because this condition proves that
    // cycles through system, dark, and light with accessible state descriptions.
    expect(selector.attributes('aria-label')).toBe(
      'Theme preference: System. Activate to use Dark theme'
    );

    // Expected outcome: `selector.attributes('aria-describedby')` has the required value.
    // Acceptance criteria: `selector.attributes('aria-describedby')` must be `'theme-selector-help'`, because this condition proves that
    // cycles through system, dark, and light with accessible state descriptions.
    expect(selector.attributes('aria-describedby')).toBe('theme-selector-help');

    // Expected outcome: `status.text()` has the required value.
    // Acceptance criteria: `status.text()` must be `'System theme preference selected'`, because this condition proves that
    // cycles through system, dark, and light with accessible state descriptions.
    expect(status.text()).toBe('System theme preference selected');

    // Expected outcome: `document.documentElement.dataset.theme` is not supplied.
    // Acceptance criteria: `document.documentElement.dataset.theme` must be undefined, because this condition proves that
    // cycles through system, dark, and light with accessible state descriptions.
    expect(document.documentElement.dataset.theme).toBeUndefined();

    await selector.trigger('click');

    // Expected outcome: `selector.attributes('data-theme-preference')` has the required value.
    // Acceptance criteria: `selector.attributes('data-theme-preference')` must be `'dark'`, because this condition proves that
    // cycles through system, dark, and light with accessible state descriptions.
    expect(selector.attributes('data-theme-preference')).toBe('dark');

    // Expected outcome: `selector.attributes('aria-label')` has the required value.
    // Acceptance criteria: `selector.attributes('aria-label')` must be `'Theme preference: Dark. Activate to use Light theme'`, because this condition proves that
    // cycles through system, dark, and light with accessible state descriptions.
    expect(selector.attributes('aria-label')).toBe(
      'Theme preference: Dark. Activate to use Light theme'
    );

    // Expected outcome: `status.text()` has the required value.
    // Acceptance criteria: `status.text()` must be `'Dark theme preference selected'`, because this condition proves that
    // cycles through system, dark, and light with accessible state descriptions.
    expect(status.text()).toBe('Dark theme preference selected');

    // Expected outcome: `document.documentElement.dataset.theme` has the required value.
    // Acceptance criteria: `document.documentElement.dataset.theme` must be `'dark'`, because this condition proves that
    // cycles through system, dark, and light with accessible state descriptions.
    expect(document.documentElement.dataset.theme).toBe('dark');

    await selector.trigger('click');

    // Expected outcome: `selector.attributes('data-theme-preference')` has the required value.
    // Acceptance criteria: `selector.attributes('data-theme-preference')` must be `'light'`, because this condition proves that
    // cycles through system, dark, and light with accessible state descriptions.
    expect(selector.attributes('data-theme-preference')).toBe('light');

    // Expected outcome: `selector.attributes('aria-label')` has the required value.
    // Acceptance criteria: `selector.attributes('aria-label')` must be `'Theme preference: Light. Activate to use System theme'`, because this condition proves that
    // cycles through system, dark, and light with accessible state descriptions.
    expect(selector.attributes('aria-label')).toBe(
      'Theme preference: Light. Activate to use System theme'
    );

    // Expected outcome: `status.text()` has the required value.
    // Acceptance criteria: `status.text()` must be `'Light theme preference selected'`, because this condition proves that
    // cycles through system, dark, and light with accessible state descriptions.
    expect(status.text()).toBe('Light theme preference selected');

    // Expected outcome: `document.documentElement.dataset.theme` has the required value.
    // Acceptance criteria: `document.documentElement.dataset.theme` must be `'light'`, because this condition proves that
    // cycles through system, dark, and light with accessible state descriptions.
    expect(document.documentElement.dataset.theme).toBe('light');

    await selector.trigger('click');

    // Expected outcome: `selector.attributes('data-theme-preference')` has the required value.
    // Acceptance criteria: `selector.attributes('data-theme-preference')` must be `'system'`, because this condition proves that
    // cycles through system, dark, and light with accessible state descriptions.
    expect(selector.attributes('data-theme-preference')).toBe('system');

    // Expected outcome: `status.text()` has the required value.
    // Acceptance criteria: `status.text()` must be `'System theme preference selected'`, because this condition proves that
    // cycles through system, dark, and light with accessible state descriptions.
    expect(status.text()).toBe('System theme preference selected');

    // Expected outcome: `document.documentElement.dataset.theme` is not supplied.
    // Acceptance criteria: `document.documentElement.dataset.theme` must be undefined, because this condition proves that
    // cycles through system, dark, and light with accessible state descriptions.
    expect(document.documentElement.dataset.theme).toBeUndefined();

    // Expected outcome: `localStorage.getItem('theme-preference')` has the required value.
    // Acceptance criteria: `localStorage.getItem('theme-preference')` must be `'system'`, because this condition proves that
    // cycles through system, dark, and light with accessible state descriptions.
    expect(localStorage.getItem('theme-preference')).toBe('system');
  });
});
