// @vitest-environment jsdom

import { mount } from '@vue/test-utils';
import { describe, expect, it } from 'vitest';

import AppButton from '@/components/AppButton.vue';

describe('AppButton', () => {
  it('shows its text by default', () => {
    const wrapper = mount(AppButton, {
      props: { text: 'Save flow', icon: '/save.svg' }
    });

    expect(wrapper.get('button').text()).toBe('Save flow');
    expect(wrapper.get('button').attributes('aria-label')).toBeUndefined();
    expect(wrapper.get('.button-icon').attributes('aria-hidden')).toBe('true');
  });

  it('uses the defined text as the accessible label when text is hidden', () => {
    const wrapper = mount(AppButton, {
      props: { text: 'Save name', icon: '/save.svg', hideText: true }
    });

    expect(wrapper.get('button').text()).toBe('');
    expect(wrapper.get('button').attributes('aria-label')).toBe('Save name');
  });
});
