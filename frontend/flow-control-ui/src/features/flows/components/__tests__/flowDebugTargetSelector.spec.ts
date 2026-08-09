// @vitest-environment jsdom

import { mount } from '@vue/test-utils';
import { describe, expect, it } from 'vitest';

import AppFlowDebugTargetSelector from '@/features/flows/components/AppFlowDebugTargetSelector.vue';

const targets = [
  { id: 'host', kind: 'host' as const, label: 'Host' },
  {
    id: 'controller:kc868-a16',
    kind: 'controller' as const,
    label: 'KC868-A16',
    controllerTemplateId: 'kc868-a16',
    controllerTemplateRevision: 3
  }
];

describe('flow debug target selector', () => {
  it('emits only configured target selections', async () => {
    const wrapper = mount(AppFlowDebugTargetSelector, {
      props: { automation: 'target', modelValue: 'host', targets }
    });

    await wrapper.get('select').setValue('controller:kc868-a16');

    expect(wrapper.emitted('update:modelValue')).toEqual([['controller:kc868-a16']]);
    expect(wrapper.text()).toContain('Controller — KC868-A16');
  });

  it('labels hardware targets as shadow mode', () => {
    const wrapper = mount(AppFlowDebugTargetSelector, {
      props: {
        automation: 'target',
        modelValue: 'controller:kc868-a16',
        targets
      }
    });

    expect(wrapper.text()).toContain('Template revision 3 · Shadow mode');
  });
});
