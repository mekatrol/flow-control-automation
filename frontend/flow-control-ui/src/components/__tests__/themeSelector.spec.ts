// @vitest-environment jsdom

import { mount } from '@vue/test-utils';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import ThemeSelector from '@/components/ThemeSelector.vue';

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

  it('cycles through system, dark, and light with accessible state descriptions', async () => {
    const wrapper = mount(ThemeSelector);
    const selector = wrapper.get<HTMLButtonElement>('.theme-selector');
    const status = wrapper.get('[role="status"]');

    expect(selector.attributes('data-theme-preference')).toBe('system');
    expect(selector.attributes('aria-label')).toBe(
      'Theme preference: System. Activate to use Dark theme'
    );
    expect(selector.attributes('aria-describedby')).toBe('theme-selector-help');
    expect(status.text()).toBe('System theme preference selected');
    expect(document.documentElement.dataset.theme).toBeUndefined();

    await selector.trigger('click');
    expect(selector.attributes('data-theme-preference')).toBe('dark');
    expect(selector.attributes('aria-label')).toBe(
      'Theme preference: Dark. Activate to use Light theme'
    );
    expect(status.text()).toBe('Dark theme preference selected');
    expect(document.documentElement.dataset.theme).toBe('dark');

    await selector.trigger('click');
    expect(selector.attributes('data-theme-preference')).toBe('light');
    expect(selector.attributes('aria-label')).toBe(
      'Theme preference: Light. Activate to use System theme'
    );
    expect(status.text()).toBe('Light theme preference selected');
    expect(document.documentElement.dataset.theme).toBe('light');

    await selector.trigger('click');
    expect(selector.attributes('data-theme-preference')).toBe('system');
    expect(status.text()).toBe('System theme preference selected');
    expect(document.documentElement.dataset.theme).toBeUndefined();
    expect(localStorage.getItem('theme-preference')).toBe('system');
  });
});
