// @vitest-environment jsdom

import { createPinia } from 'pinia';
import { mount } from '@vue/test-utils';
import { createMemoryHistory, createRouter } from 'vue-router';
import { beforeEach, describe, expect, it } from 'vitest';

import App from '@/App.vue';
import AppLayout from '@/layouts/AppLayout.vue';
import { useWait } from '@/composables/useWait';

const FlowListStub = { template: '<h1>Flows</h1>' };

describe('App', () => {
  beforeEach(() => sessionStorage.setItem('flow-control-api-key', 'unit-test-key'));
  /**
   * Purpose: Protects the application-shell contract that provides navigation, theme
   * control, skip navigation, and the active route without blocking page content.
   * Description: Mounts the flows route in the application shell and verifies its
   * landmark structure, theme control, and routed heading.
   */
  it('renders the current route inside the application shell', async () => {
    const router = createRouter({
      history: createMemoryHistory(),
      routes: [
        {
          path: '/',
          component: AppLayout,
          children: [{ path: 'flows', name: 'flows', component: FlowListStub }]
        }
      ]
    });

    await router.push('/flows');
    await router.isReady();

    const wrapper = mount(App, {
      global: { plugins: [createPinia(), router] }
    });

    // Expected outcome: `wrapper.get('.brand'` includes the required value.
    // Acceptance criteria: `wrapper.get('.brand'` must contain `'Flow Control'`, because this condition proves that
    // renders the current route inside the application shell.
    expect(wrapper.get('.brand').text()).toContain('Flow Control');

    // Expected outcome: `wrapper.get('.skip-link'` has the required value.
    // Acceptance criteria: `wrapper.get('.skip-link'` must be `'#main-content'`, because this condition proves that
    // renders the current route inside the application shell.
    expect(wrapper.get('.skip-link').attributes('href')).toBe('#main-content');

    // Expected outcome: `wrapper.get('.theme-selector'` has the required value.
    // Acceptance criteria: `wrapper.get('.theme-selector'` must be `'system'`, because this condition proves that
    // renders the current route inside the application shell.
    expect(wrapper.get('.theme-selector').attributes('data-theme-preference')).toBe('system');

    // Expected outcome: `wrapper.get('main'` has the required value.
    // Acceptance criteria: `wrapper.get('main'` must be `'main-content'`, because this condition proves that
    // renders the current route inside the application shell.
    expect(wrapper.get('main').attributes('id')).toBe('main-content');

    // Expected outcome: `wrapper.get('main h1'` has the required value.
    // Acceptance criteria: `wrapper.get('main h1'` must be `'Flows'`, because this condition proves that
    // renders the current route inside the application shell.
    expect(wrapper.get('main h1').text()).toBe('Flows');
  });

  it('blocks the application while any operation is waiting', async () => {
    const pinia = createPinia();
    const wrapper = mount(App, {
      global: { plugins: [pinia], stubs: { RouterView: true } }
    });
    const { wait, endWait } = useWait();

    wait();
    wait();
    await wrapper.vm.$nextTick();

    expect(wrapper.get('.spinner-overlay').attributes('role')).toBe('status');
    expect(wrapper.get('.app-content').attributes()).toHaveProperty('inert');

    endWait();
    await wrapper.vm.$nextTick();
    expect(wrapper.find('.spinner-overlay').exists()).toBe(true);

    endWait();
    await wrapper.vm.$nextTick();
    expect(wrapper.find('.spinner-overlay').exists()).toBe(false);
    expect(wrapper.get('.app-content').attributes()).not.toHaveProperty('inert');
  });
});
