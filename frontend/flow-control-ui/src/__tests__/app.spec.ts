// @vitest-environment jsdom

import { createPinia } from 'pinia';
import { mount } from '@vue/test-utils';
import { createMemoryHistory, createRouter } from 'vue-router';
import { describe, expect, it, vi } from 'vitest';

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
    const showModal = vi.fn<() => void>();
    HTMLDialogElement.prototype.showModal = showModal;
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

    // Expected outcome: Loading the application presents the informational welcome notice.
    // Acceptance criteria: Native `showModal` is called once because the temporary demo
    // must open one whole-view welcome dialog after the application mounts.
    expect(showModal).toHaveBeenCalledOnce();

    // Expected outcome: The welcome notice uses the requested informational presentation.
    // Acceptance criteria: The notice has the `notice--info` class because the page-load
    // demonstration must exercise the info variant rather than an urgent error state.
    expect(wrapper.get('.notice').classes()).toContain('notice--info');

    // Expected outcome: The welcome notice explains what the application is for.
    // Acceptance criteria: The dialog contains "Welcome to Flow Control" because users
    // should immediately recognize the demo as an application welcome message.
    expect(wrapper.get('#welcome-notice').text()).toContain('Welcome to Flow Control');
  });
});
