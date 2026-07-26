// @vitest-environment jsdom

import { mount } from '@vue/test-utils';
import { describe, expect, it } from 'vitest';

import AppPopover from '@/components/AppPopover.vue';

describe('AppPopover', () => {
  it('renders a native popover surface without owning the trigger element', () => {
    const wrapper = mount(AppPopover, {
      props: {
        contentLabel: 'App options',
        id: 'app-popover'
      },
      slots: {
        default: '<p>Popover content</p>'
      }
    });

    const panel = wrapper.get('[role="dialog"]');

    expect(wrapper.find('button').exists()).toBe(false);
    expect(panel.attributes('id')).toBe('app-popover');
    expect(panel.attributes('aria-label')).toBe('App options');
    expect(panel.attributes('popover')).toBe('auto');
    expect(panel.attributes('data-placement')).toBe('bottom-start');
    expect(panel.text()).toContain('Popover content');
  });

  it('allows callers to choose manual popover mode and placement', () => {
    const wrapper = mount(AppPopover, {
      props: {
        contentLabel: 'App options',
        id: 'app-popover',
        placement: 'top-end',
        popoverMode: 'manual'
      }
    });

    const panel = wrapper.get('[role="dialog"]');

    expect(panel.attributes('popover')).toBe('manual');
    expect(panel.attributes('data-placement')).toBe('top-end');
  });
});
