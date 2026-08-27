// @vitest-environment jsdom

import { mount } from '@vue/test-utils';
import { describe, expect, it } from 'vitest';
import AppFlowSimulatorPanel from '@/features/flows/components/AppFlowSimulatorPanel.vue';

describe('flow simulator controls', () => {
  it('offers only start before a simulation session exists', async () => {
    const wrapper = mount(AppFlowSimulatorPanel, { props: { lifecycle: 'idle' } });
    const buttons = wrapper.findAll('button');

    expect(buttons.map((button) => button.text())).toEqual(['Start simulation', 'Stop simulation']);
    expect(buttons[0]!.attributes('disabled')).toBeUndefined();
    expect(buttons[1]!.attributes('disabled')).toBeDefined();

    await buttons[0]!.trigger('click');
    expect(wrapper.emitted('start-simulation')).toHaveLength(1);
  });

  it('offers stop and reports running for an active session', async () => {
    const wrapper = mount(AppFlowSimulatorPanel, { props: { lifecycle: 'running' } });
    const buttons = wrapper.findAll('button');

    expect(wrapper.get('[role="status"]').text()).toBe('running');
    expect(buttons[0]!.attributes('disabled')).toBeDefined();
    expect(buttons[1]!.attributes('disabled')).toBeUndefined();

    await buttons[1]!.trigger('click');
    expect(wrapper.emitted('stop-simulation')).toHaveLength(1);
  });
});
