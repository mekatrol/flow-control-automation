// @vitest-environment jsdom

import { createPinia } from 'pinia';
import { flushPromises, mount } from '@vue/test-utils';
import { afterEach, describe, expect, it, vi } from 'vitest';
import AppPointsCatalogueView from '@/features/catalogues/views/AppPointsCatalogueView.vue';

afterEach(() => vi.unstubAllGlobals());

describe('PointsCatalogueView', () => {
  it('renders a semantic, keyboard-reachable table with point relationships', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue(
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

    expect(wrapper.get('h1').text()).toBe('Points');
    expect(wrapper.get('table caption').text()).toContain('Configured points');
    expect(wrapper.get('th[scope="row"]').text()).toContain('Temperature');
    expect(wrapper.text()).toContain('Group: room');
    expect(wrapper.text()).toContain('Inherited from group');
    expect(wrapper.get('[role="region"]').attributes('tabindex')).toBe('0');
    expect(wrapper.get('input[type="search"]').attributes('id')).toBe('points-filter');
  });

  it('shows empty and unavailable states', async () => {
    vi.stubGlobal(
      'fetch',
      vi
        .fn()
        .mockResolvedValue(new Response(JSON.stringify({ message: 'missing' }), { status: 404 }))
    );
    const wrapper = mount(AppPointsCatalogueView, {
      global: { plugins: [createPinia()] }
    });
    await flushPromises();
    expect(wrapper.get('[role="alert"]').text()).toContain('does not support');
    expect(wrapper.get('[role="alert"] button').text()).toBe('Check again');
  });
});
