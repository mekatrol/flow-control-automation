// @vitest-environment jsdom

import { mount } from '@vue/test-utils';
import { describe, expect, it } from 'vitest';

import FlowNode from '@/features/flows/components/FlowNode.vue';
import { sampleFlows } from '@/features/flows/__tests__/fixtures/sampleFlows';

describe('FlowNode', () => {
  it('uses registry metadata and exposes an accessible node name and status', () => {
    const node = sampleFlows[0]!.nodes[0]!;
    const wrapper = mount(FlowNode, {
      props: { node, selected: false, status: 'running', statusValue: '21.5 °C' }
    });

    expect(wrapper.attributes('aria-label')).toBe(
      'Average temperature, Calculator node, running, 21.5 °C'
    );
    expect(wrapper.attributes('data-node-category')).toBe('maths');
    expect(wrapper.get('.node-icon image').attributes('href')).toBe(
      '/icons/flow-nodes/calculator.svg'
    );
    expect(wrapper.text()).toContain('Calculator');
    expect(wrapper.get('.node-status').attributes('aria-label')).toBe('running: 21.5 °C');
    expect(wrapper.findAll('.node-marker')).toHaveLength(3);
    expect(wrapper.find('.node-marker.orange rect').exists()).toBe(true);
    expect(wrapper.find('.node-marker.green path').exists()).toBe(true);
    expect(wrapper.find('.node-marker.blue circle').exists()).toBe(true);
    expect(wrapper.findAll('rect.connector-port')).toHaveLength(node.connectors.length);
    expect(wrapper.get('.node-body').attributes('width')).toBe('170');
    expect(wrapper.findAll('.node-marker').map((marker) => marker.attributes('transform'))).toEqual([
      'translate(110 -8)',
      'translate(130 -8)',
      'translate(150 -8)'
    ]);
    expect(
      wrapper
        .findAll('.flow-connector')
        .some((connector) => connector.attributes('transform')?.startsWith('translate(170 '))
    ).toBe(true);
  });

  it('emits selection from keyboard activation', async () => {
    const wrapper = mount(FlowNode, {
      props: { node: sampleFlows[0]!.nodes[0]!, selected: false }
    });

    await wrapper.trigger('keydown', { key: 'Enter' });

    expect(wrapper.emitted('select')).toEqual([['temperature-average']]);
  });
});
