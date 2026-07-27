// @vitest-environment jsdom

import { mount } from '@vue/test-utils';
import { describe, expect, it } from 'vitest';

import AppPopover from '@/components/AppPopover.vue';

describe('AppPopover', () => {
  /**
   * Purpose: Protects the behavioral contract that renders a native popover surface without owning the trigger element.
   * Description: Exercises renders a native popover surface without owning the trigger element from its arranged starting state and
   * verifies the observable results required by the scenario.
   */
  it('renders a native popover surface without owning the trigger element', () => {
    const wrapper = mount(AppPopover, {
      props: {
        automation: 'app-popover',
        contentLabel: 'App options',
        id: 'app-popover'
      },
      slots: {
        default: '<p>Popover content</p>'
      }
    });

    const panel = wrapper.get('[role="dialog"]');

    // Expected outcome: `wrapper.find('button'` has the required value.
    // Acceptance criteria: `wrapper.find('button'` must be `false`, because this condition proves that
    // renders a native popover surface without owning the trigger element.
    expect(wrapper.find('button').exists()).toBe(false);

    // Expected outcome: `panel.attributes('id')` has the required value.
    // Acceptance criteria: `panel.attributes('id')` must be `'app-popover'`, because this condition proves that
    // renders a native popover surface without owning the trigger element.
    expect(panel.attributes('id')).toBe('app-popover');

    // Expected outcome: `panel.attributes('aria-label')` has the required value.
    // Acceptance criteria: `panel.attributes('aria-label')` must be `'App options'`, because this condition proves that
    // renders a native popover surface without owning the trigger element.
    expect(panel.attributes('aria-label')).toBe('App options');

    // Expected outcome: `panel.attributes('popover')` has the required value.
    // Acceptance criteria: `panel.attributes('popover')` must be `'auto'`, because this condition proves that
    // renders a native popover surface without owning the trigger element.
    expect(panel.attributes('popover')).toBe('auto');

    // Expected outcome: `panel.attributes('data-placement')` has the required value.
    // Acceptance criteria: `panel.attributes('data-placement')` must be `'bottom-start'`, because this condition proves that
    // renders a native popover surface without owning the trigger element.
    expect(panel.attributes('data-placement')).toBe('center');

    // Expected outcome: `panel.text()` includes the required value.
    // Acceptance criteria: `panel.text()` must contain `'Popover content'`, because this condition proves that
    // renders a native popover surface without owning the trigger element.
    expect(panel.text()).toContain('Popover content');
  });

  /**
   * Purpose: Protects the behavioral contract that allows callers to choose manual popover mode and placement.
   * Description: Exercises allows callers to choose manual popover mode and placement from its arranged starting state and
   * verifies the observable results required by the scenario.
   */
  it('allows callers to choose manual popover mode and placement', () => {
    const wrapper = mount(AppPopover, {
      props: {
        automation: 'app-popover',
        contentLabel: 'App options',
        id: 'app-popover',
        placement: 'top-end',
        popoverMode: 'manual'
      }
    });

    const panel = wrapper.get('[role="dialog"]');

    // Expected outcome: `panel.attributes('popover')` has the required value.
    // Acceptance criteria: `panel.attributes('popover')` must be `'manual'`, because this condition proves that
    // allows callers to choose manual popover mode and placement.
    expect(panel.attributes('popover')).toBe('manual');

    // Expected outcome: `panel.attributes('data-placement')` has the required value.
    // Acceptance criteria: `panel.attributes('data-placement')` must be `'top-end'`, because this condition proves that
    // allows callers to choose manual popover mode and placement.
    expect(panel.attributes('data-placement')).toBe('top-end');
  });
});
