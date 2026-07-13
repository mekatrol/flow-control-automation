// @vitest-environment jsdom

import { mount } from '@vue/test-utils';
import { describe, expect, it } from 'vitest';

import FlowNode from '../FlowNode.vue';
import { sampleFlows } from '../../__tests__/fixtures/sampleFlows';

describe('FlowNode', () => {
  it('uses registry metadata and exposes an accessible node name and status', () => {
    const node = sampleFlows[0]!.nodes[0]!;
    const wrapper = mount(FlowNode, {
      props: { node, selected: false, marker: '!', status: 'running', statusValue: '21.5 °C' }
    });

    expect(wrapper.attributes('aria-label')).toBe(
      'Average temperature, Calculator node, running, 21.5 °C'
    );
    expect(wrapper.text()).toContain('∑');
    expect(wrapper.text()).toContain('Calculator');
    expect(wrapper.text()).toContain('!');
    expect(wrapper.get('.node-status').attributes('aria-label')).toBe('running: 21.5 °C');
  });

  it('emits selection from keyboard activation', async () => {
    const wrapper = mount(FlowNode, {
      props: { node: sampleFlows[0]!.nodes[0]!, selected: false }
    });

    await wrapper.trigger('keydown', { key: 'Enter' });

    expect(wrapper.emitted('select')).toEqual([['temperature-average']]);
  });
});
