// @vitest-environment jsdom

import { createPinia } from 'pinia';
import { mount } from '@vue/test-utils';
import { createMemoryHistory, createRouter } from 'vue-router';
import { describe, expect, it } from 'vitest';

import App from '@/App.vue';
import AppLayout from '@/layouts/AppLayout.vue';

const FlowListStub = { template: '<h1>Flows</h1>' };

describe('App', () => {
  /**
   * Purpose: Protects the behavioral contract that renders the current route inside the application shell.
   * Description: Exercises renders the current route inside the application shell from its arranged starting state and
   * verifies the observable results required by the scenario.
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
});
