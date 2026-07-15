// @vitest-environment jsdom

import { createPinia } from 'pinia';
import { mount } from '@vue/test-utils';
import { createMemoryHistory, createRouter } from 'vue-router';
import { describe, expect, it } from 'vitest';

import App from '@/App.vue';

const FlowListStub = { template: '<h1>Flows</h1>' };

describe('App', () => {
  it('renders the current route inside the application shell', async () => {
    const router = createRouter({
      history: createMemoryHistory(),
      routes: [{ path: '/flows', name: 'flows', component: FlowListStub }]
    });

    await router.push('/flows');
    await router.isReady();

    const wrapper = mount(App, {
      global: { plugins: [createPinia(), router] }
    });

    expect(wrapper.get('.brand').text()).toContain('Flow Control');
    expect(wrapper.get('.skip-link').attributes('href')).toBe('#main-content');
    expect(wrapper.get('main').attributes('id')).toBe('main-content');
    expect(wrapper.get('main h1').text()).toBe('Flows');
  });
});
