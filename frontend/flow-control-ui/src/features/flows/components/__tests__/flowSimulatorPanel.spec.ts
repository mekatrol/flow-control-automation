// @vitest-environment jsdom

import { mount } from '@vue/test-utils';
import { describe, expect, it } from 'vitest';
import AppFlowSimulatorPanel from '@/features/flows/components/AppFlowSimulatorPanel.vue';

describe('flow simulator panel', () => {
  it('exposes deterministic controls and announces lifecycle state', async () => {
    const wrapper = mount(AppFlowSimulatorPanel, {
      props: {
        automation: 'simulator',
        lifecycle: 'ready',
        session: {
          sessionId: 'one',
          flowId: 'flow-a',
          sourceRevision: 1,
          sourceDigest: 'abc',
          lifecycleState: 'ready',
          leaseRemainingMilliseconds: 900000,
          breakpoints: [],
          capabilities: {
            stepTick: true,
            stepNode: true,
            stepInstruction: true,
            continue: true,
            pause: true,
            runTo: true,
            maximumBreakpoints: 32,
            maximumInspectableSlots: 256
          }
        }
      }
    });

    await wrapper.get('[data-automation="simulator-step-tick"]').trigger('click');

    expect(wrapper.emitted('step-tick')).toHaveLength(1);
    expect(wrapper.get('[role="status"]').text()).toBe('Ready');
    expect(wrapper.text()).toContain('Physical equipment cannot be commanded');
  });

  it('requires recompilation after an edit and disables execution', () => {
    const wrapper = mount(AppFlowSimulatorPanel, {
      props: { automation: 'simulator', lifecycle: 'stale' }
    });

    expect(wrapper.get('[role="alert"]').text()).toContain('Start simulation again');
    expect(
      wrapper.get('[data-automation="simulator-step-tick"]').attributes('disabled')
    ).toBeDefined();
    expect(
      wrapper.get('[data-automation="simulator-start"]').attributes('disabled')
    ).toBeUndefined();
  });
});
