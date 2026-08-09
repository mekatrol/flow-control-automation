// @vitest-environment jsdom

import { mount } from '@vue/test-utils';
import { describe, expect, it } from 'vitest';
import AppFlowDebugPanel from '@/features/flows/components/AppFlowDebugPanel.vue';

describe('flow debug panel', () => {
  it('enables only valid lifecycle operations', async () => {
    const wrapper = mount(AppFlowDebugPanel, {
      props: { automation: 'debug', lifecycle: 'ready', targetAvailable: true }
    });
    const buttons = wrapper.findAll('button');
    expect(buttons.map((button) => button.attributes('disabled') === undefined)).toEqual([
      false,
      true,
      true,
      false,
      true
    ]);
    await buttons[1]!.trigger('click');
    expect(wrapper.emitted('step')).toHaveLength(1);
  });

  it('marks snapshots stale and disables execution', () => {
    const wrapper = mount(AppFlowDebugPanel, {
      props: {
        automation: 'debug',
        lifecycle: 'ready',
        targetAvailable: true,
        stale: true,
        snapshot: {
          debugSessionId: '1',
          flowId: 'flow-a',
          revision: 1,
          lifecycleState: 'ready',
          mode: 'manual',
          tickNumber: 1,
          sampledAtMs: 1,
          completedAtMs: 2,
          executionDurationUs: 3,
          inputValidity: [],
          nodes: [],
          proposedOutputs: [],
          overrunCount: 0,
          evaluationFailureCount: 0,
          lastReasonCode: 0,
          lastReason: 'none',
          lastReasonPath: ''
        }
      }
    });
    expect(wrapper.text()).toContain('Stale snapshot');
    expect(wrapper.find('[data-automation="debug-step"]').attributes('disabled')).toBeDefined();
  });

  it('names every physical output and requires explicit confirmation', async () => {
    const wrapper = mount(AppFlowDebugPanel, {
      props: {
        automation: 'debug',
        lifecycle: 'ready',
        targetAvailable: true,
        affectedOutputPoints: ['output-01', 'output-08']
      }
    });
    const enable = wrapper.find('[data-automation="debug-enable-live-output"]');
    expect(wrapper.text()).toContain('output-01, output-08');
    expect(enable.attributes('disabled')).toBeDefined();

    await wrapper.find('input[type="checkbox"]').setValue(true);
    await enable.trigger('click');

    expect(wrapper.emitted('enableLiveOutput')).toEqual([[['output-01', 'output-08']]]);
  });
});
