// @vitest-environment jsdom

import { mount } from '@vue/test-utils';
import { describe, expect, it } from 'vitest';

import AppFilter from '@/components/AppFilter.vue';
import { EVENTS } from '@/constants/events';

describe('AppFilter', () => {
  /**
   * Purpose: Protects the shared filter shell's ability to host different labelled control types.
   * Description: Renders search and select controls through the default slot and verifies the semantic form,
   * automation hooks, and standard apply action remain available.
   */
  it('renders flexible filter controls with a standard apply action', () => {
    const wrapper = mount(AppFilter, {
      props: { automation: 'example-filter', constrained: true },
      slots: {
        default:
          '<label class="app-filter-field">Name<input type="search"></label><label>Status<select><option>All</option></select></label>'
      }
    });

    // Expected outcome: The filter exposes a semantic search landmark.
    // Acceptance criteria: The root form has role "search" because assistive technology must identify the grouped controls as filtering functionality.
    expect(wrapper.get('form').attributes('role')).toBe('search');

    // Expected outcome: Consumers can provide heterogeneous filter controls.
    // Acceptance criteria: One search input and one select are rendered because AppFilter must support page-specific filter combinations through its slot.
    expect(wrapper.findAll('input[type="search"]')).toHaveLength(1);

    // Expected outcome: The select control is retained alongside the search input.
    // Acceptance criteria: Exactly one select is rendered because flexible content must not be restricted to text filters.
    expect(wrapper.findAll('select')).toHaveLength(1);

    // Expected outcome: The component exposes a stable root automation identifier.
    // Acceptance criteria: The form has data-automation "example-filter" because end-to-end consumers need a stable hook for the complete filter.
    expect(wrapper.get('form').attributes('data-automation')).toBe('example-filter');

    // Expected outcome: Every filter receives the consistent apply action.
    // Acceptance criteria: The submit button text is "Apply filter" because that is the application-wide explicit filtering action.
    expect(wrapper.get('button[type="submit"]').text()).toBe('Apply filter');
  });

  /**
   * Purpose: Protects explicit filter application by mouse, keyboard, and assistive-technology form submission.
   * Description: Submits the native search form and verifies AppFilter emits its typed apply-filter event once.
   */
  it('emits the shared apply event when submitted', async () => {
    const wrapper = mount(AppFilter, {
      props: { automation: 'example-filter' },
      slots: { default: '<input aria-label="Name" type="search">' }
    });

    await wrapper.get('form').trigger('submit');

    // Expected outcome: One user submission requests one filter application.
    // Acceptance criteria: The apply-filter event is emitted exactly once because a single form submit must not duplicate data requests.
    expect(wrapper.emitted(EVENTS.APPLY_FILTER)).toHaveLength(1);
  });
});
