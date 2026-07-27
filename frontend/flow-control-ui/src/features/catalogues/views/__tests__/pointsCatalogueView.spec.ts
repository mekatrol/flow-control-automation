// @vitest-environment jsdom

import { createPinia } from 'pinia';
import { flushPromises, mount } from '@vue/test-utils';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import AppPointsCatalogueView from '@/features/catalogues/views/AppPointsCatalogueView.vue';

beforeEach(() => {
  HTMLDialogElement.prototype.showModal = vi.fn<() => void>();
  HTMLDialogElement.prototype.close = vi.fn<() => void>();
});

afterEach(() => vi.unstubAllGlobals());

describe('PointsCatalogueView', () => {
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
                groupId: 'room',
                implementation: 'bound',
                direction: 'input',
                valueType: 'analog',
                units: 'deg_c',
                readable: true,
                commandable: false,
                persistence: 'volatile',
                sourceId: null,
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
    const wrapper = mount(AppPointsCatalogueView, {
      global: { plugins: [createPinia()] }
    });
    await flushPromises();

    // Expected outcome: `wrapper.get('h1'` has the required value.
    // Acceptance criteria: `wrapper.get('h1'` must be `'Points'`, because this condition proves that
    // renders a semantic, keyboard-reachable table with point relationships.
    expect(wrapper.get('h1').text()).toBe('Points');

    // Expected outcome: `wrapper.get('table caption'` includes the required value.
    // Acceptance criteria: `wrapper.get('table caption'` must contain `'Configured points'`, because this condition proves that
    // renders a semantic, keyboard-reachable table with point relationships.
    expect(wrapper.get('table caption').text()).toContain('Configured points');

    // Expected outcome: `wrapper.get('th[scope="row"]'` includes the required value.
    // Acceptance criteria: `wrapper.get('th[scope="row"]'` must contain `'Temperature'`, because this condition proves that
    // renders a semantic, keyboard-reachable table with point relationships.
    expect(wrapper.get('th[scope="row"]').text()).toContain('Temperature');

    // Expected outcome: `wrapper.text()` includes the required value.
    // Acceptance criteria: `wrapper.text()` must contain `'Group: room'`, because this condition proves that
    // renders a semantic, keyboard-reachable table with point relationships.
    expect(wrapper.text()).toContain('Group: room');

    // Expected outcome: `wrapper.text()` includes the required value.
    // Acceptance criteria: `wrapper.text()` must contain `'Inherited from group'`, because this condition proves that
    // renders a semantic, keyboard-reachable table with point relationships.
    expect(wrapper.text()).toContain('Inherited from group');

    // Expected outcome: `wrapper.get('[role="region"]'` has the required value.
    // Acceptance criteria: `wrapper.get('[role="region"]'` must be `'0'`, because this condition proves that
    // renders a semantic, keyboard-reachable table with point relationships.
    expect(wrapper.get('[role="region"]').attributes('tabindex')).toBe('0');

    // Expected outcome: `wrapper.get('input[type="search"]'` has the required value.
    // Acceptance criteria: `wrapper.get('input[type="search"]'` must be `'points-filter'`, because this condition proves that
    // renders a semantic, keyboard-reachable table with point relationships.
    expect(wrapper.get('input[type="search"]').attributes('id')).toBe('points-filter');
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
    const wrapper = mount(AppPointsCatalogueView, {
      global: { plugins: [createPinia()] }
    });
    await flushPromises();

    // Expected outcome: A failed catalogue request displays the API error.
    // Acceptance criteria: The alert contains "missing" because the arranged 404
    // response supplies that diagnostic message for the user.
    expect(wrapper.get('[role="alert"]').text()).toContain('missing');

    // Expected outcome: The error notice offers a recovery action.
    // Acceptance criteria: The alert button is labelled "Retry" because a failed
    // catalogue load must remain recoverable without reloading the application.
    expect(wrapper.get('[role="alert"] button').text()).toBe('Retry');
  });
});
