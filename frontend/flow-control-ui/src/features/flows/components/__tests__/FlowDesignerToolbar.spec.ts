// @vitest-environment jsdom

import { mount } from '@vue/test-utils';
import { describe, expect, it } from 'vitest';

import FlowDesignerToolbar from '../FlowDesignerToolbar.vue';

describe('FlowDesignerToolbar', () => {
  it('uses distinct directional and terminal icons for all stacking operations', async () => {
    const wrapper = mount(FlowDesignerToolbar, {
      props: { selectedNodeId: 'node-1', canMoveFront: true, canMoveBack: true }
    });

    expect(wrapper.findAll('svg[data-icon]')).toHaveLength(4);
    expect(wrapper.findAll('svg[data-icon] path.direction')).toHaveLength(4);
    expect(wrapper.findAll('svg[data-icon] path.destination')).toHaveLength(2);
    expect(wrapper.get('svg[data-icon="front"] path.destination').exists()).toBe(true);
    expect(wrapper.get('svg[data-icon="back"] path.destination').exists()).toBe(true);

    const buttons = wrapper.findAll('button');
    for (const button of buttons) await button.trigger('click');
    expect(wrapper.emitted('reorder')).toEqual([['front'], ['forward'], ['backward'], ['back']]);
  });
});
