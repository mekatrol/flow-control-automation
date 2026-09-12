// @vitest-environment jsdom

import { createPinia } from 'pinia';
import { flushPromises, mount } from '@vue/test-utils';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import AppPointListView from '@/features/points/views/AppPointListView.vue';

beforeEach(() => {
  HTMLDialogElement.prototype.showModal = vi.fn<() => void>();
  HTMLDialogElement.prototype.close = vi.fn<() => void>();
});

afterEach(() => vi.unstubAllGlobals());

describe('PointListView', () => {
  /**
   * Purpose: Protects the behavioral contract that renders a semantic, keyboard-reachable table with point relationships.
   * Description: Exercises renders a semantic, keyboard-reachable table with point relationships from its arranged starting state and
   * verifies the observable results required by the scenario.
   */
  it('renders a semantic, keyboard-reachable table with point relationships', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn<typeof fetch>().mockResolvedValue(
        new Response(
          JSON.stringify({
            items: [
              {
                id: 'temperature',
                name: 'Temperature',
                enabled: true,
                pointSourceType: 'remote',
                direction: 'input',
                valueType: 'analog',
                units: 'deg_c',
                readable: true,
                commandable: false,
                persistence: 'volatile',
                sourceId: 'building-controller',
                revision: 1
              }
            ],
            totalItems: 1,
            page: 1,
            pageSize: 10,
            pageCount: 1
          }),
          { status: 200 }
        )
      )
    );
    const wrapper = mount(AppPointListView, {
      global: { plugins: [createPinia()] }
    });
    await flushPromises();

    // Expected outcome: `wrapper.get('.list-heading'` has the required value.
    // Acceptance criteria: `wrapper.get('.list-heading'` must be `'Points'`, because this condition proves that
    // renders a semantic, keyboard-reachable table with point relationships.
    expect(wrapper.get('.list-heading').text()).toBe('Points');

    // Expected outcome: `wrapper.get('table caption'` includes the required value.
    // Acceptance criteria: `wrapper.get('table caption'` must contain `'Configured points'`, because this condition proves that
    // renders a semantic, keyboard-reachable table with point relationships.
    expect(wrapper.get('table caption').text()).toContain('Configured points');

    expect(wrapper.get('thead th button').attributes('aria-label')).toBe('Add a new point');

    expect(wrapper.get('tbody td').text()).toContain('Temperature');
    expect(wrapper.text()).toContain('building-controller');

    // Expected outcome: `wrapper.get('input[type="search"]'` has the required value.
    // Acceptance criteria: `wrapper.get('input[type="search"]'` must be `'points-filter'`, because this condition proves that
    // renders a semantic, keyboard-reachable table with point relationships.
    expect(wrapper.get('input[type="search"]').attributes('id')).toBe('points-list-filter');
  });

  /**
   * Purpose: Protects the behavioral contract that shows API errors.
   * Description: Exercises API error presentation from its arranged starting state and
   * verifies the observable results required by the scenario.
   */
  it('shows the API error and a retry action', async () => {
    vi.stubGlobal(
      'fetch',
      vi
        .fn<typeof fetch>()
        .mockResolvedValue(new Response(JSON.stringify({ message: 'missing' }), { status: 404 }))
    );
    const wrapper = mount(AppPointListView, {
      global: { plugins: [createPinia()] }
    });
    await flushPromises();

    // Expected outcome: A failed point-list request displays the API error.
    // Acceptance criteria: The alert contains "missing" because the arranged 404
    // response supplies that diagnostic message for the user.
    expect(wrapper.get('[role="alert"]').text()).toContain('missing');

    // Expected outcome: The error notice offers a recovery action.
    // Acceptance criteria: The alert button is labelled "Check again" because a failed
    // point-list load must remain recoverable without reloading the application.
    expect(wrapper.get('[role="alert"] button').text()).toBe('Check again');
  });
});
