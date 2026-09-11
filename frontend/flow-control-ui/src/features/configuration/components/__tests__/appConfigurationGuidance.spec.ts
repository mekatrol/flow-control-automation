// @vitest-environment jsdom

import { mount } from '@vue/test-utils';
import { afterEach, describe, expect, it, vi } from 'vitest';

import AppConfigurationGuidance from '@/features/configuration/components/AppConfigurationGuidance.vue';
import { fetchConfigurationGuidance } from '@/features/configuration/api/configurationGuidanceApi';

vi.mock('@/features/configuration/api/configurationGuidanceApi', () => ({
  fetchConfigurationGuidance: vi.fn()
}));
vi.mock('@/components/AppYamlEditor.vue', () => ({
  default: { template: '<div data-yaml-editor />' }
}));

describe('AppConfigurationGuidance', () => {
  afterEach(() => {
    document.body.classList.remove('configuration-guidance-open');
    document.body.replaceChildren();
    vi.clearAllMocks();
  });

  /**
   * Purpose: Keeps help visible while its server-generated content is still being requested.
   */
  it('opens the guidance panel immediately and displays an inline loading state', async () => {
    let resolveGuidance: (value: string) => void = () => undefined;
    vi.mocked(fetchConfigurationGuidance).mockReturnValue(
      new Promise<string>((resolve) => {
        resolveGuidance = resolve;
      })
    );
    const wrapper = mount(AppConfigurationGuidance, {
      attachTo: document.body,
      props: { type: 'point', yaml: 'schemaVersion: 1' }
    });

    await wrapper.get('button').trigger('click');

    const panel = document.body.querySelector<HTMLElement>(
      '[aria-label="YAML configuration guidance"]'
    );
    expect(panel).not.toBeNull();
    expect(panel?.textContent).toContain('Generating guidance…');
    expect(document.body.classList.contains('configuration-guidance-open')).toBe(true);

    resolveGuidance('# Point guidance');
    await vi.waitFor(() => expect(panel?.textContent).toContain('Point guidance'));
    wrapper.unmount();
  });
});
