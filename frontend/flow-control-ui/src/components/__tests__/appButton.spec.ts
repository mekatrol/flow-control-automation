// @vitest-environment jsdom

import { mount } from '@vue/test-utils';
import { describe, expect, it } from 'vitest';

import AppButton from '@/components/AppButton.vue';

describe('AppButton', () => {
  /**
   * Purpose: Protects the behavioral contract that shows its text by default.
   * Description: Exercises shows its text by default from its arranged starting state and
   * verifies the observable results required by the scenario.
   */
  it('shows its text by default', () => {
    const wrapper = mount(AppButton, {
      props: { text: 'Save flow', icon: '/save.svg' }
    });

    // Expected outcome: `wrapper.get('button'` has the required value.
    // Acceptance criteria: `wrapper.get('button'` must be `'Save flow'`, because this condition proves that
    // shows its text by default.
    expect(wrapper.get('button').text()).toBe('Save flow');

    // Expected outcome: No `aria-label` is provided.
    // Acceptance criteria: The button's `aria-label` attribute must be undefined,
    // allowing its visible text to serve as the accessible name.
    expect(wrapper.get('button').attributes('aria-label')).toBeUndefined();

    // Expected outcome: `wrapper.get('.button-icon'` has the required value.
    // Acceptance criteria: `wrapper.get('.button-icon'` must be `'true'`, because this condition proves that
    // shows its text by default.
    expect(wrapper.get('.button-icon').attributes('aria-hidden')).toBe('true');
  });

  /**
   * Purpose: Protects the behavioral contract that uses the defined text as the accessible label when text is hidden.
   * Description: Exercises uses the defined text as the accessible label when text is hidden from its arranged starting state and
   * verifies the observable results required by the scenario.
   */
  it('uses the defined text as the accessible label when text is hidden', () => {
    const wrapper = mount(AppButton, {
      props: { text: 'Save name', icon: '/save.svg', hideText: true }
    });

    // Expected outcome: `wrapper.get('button'` has the required value.
    // Acceptance criteria: `wrapper.get('button'` must be `''`, because this condition proves that
    // uses the defined text as the accessible label when text is hidden.
    expect(wrapper.get('button').text()).toBe('');

    // Expected outcome: `wrapper.get('button'` has the required value.
    // Acceptance criteria: `wrapper.get('button'` must be `'Save name'`, because this condition proves that
    // uses the defined text as the accessible label when text is hidden.
    expect(wrapper.get('button').attributes('aria-label')).toBe('Save name');
  });
});
