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
});
