// @vitest-environment jsdom

import { mount } from '@vue/test-utils';
import { describe, expect, it } from 'vitest';

import AppFlowNode from '@/features/flows/components/AppFlowNode.vue';
import { sampleFlows } from '@/features/flows/__tests__/fixtures/sampleFlows';

describe('FlowNode', () => {
  /**
   * Purpose: Protects connector-level debugger values and non-colour breakpoint communication.
   * Description: Renders a committed connector value with before/after breakpoints and verifies both visible and accessible text.
   */
  it('renders typed connector overlays and textual breakpoint positions', () => {
    const node = sampleFlows[0]!.nodes[0]!;
    const output = node.connectors.find((connector) => connector.direction === 'output')!;
    const wrapper = mount(AppFlowNode, {
      props: {
        automation: 'flow-node-debug',
        node,
        selected: false,
        breakpointPositions: ['before', 'after'],
        connectorValues: {
          [output.id]: { value: '21.5', units: '°C', quality: 'good', state: 'committed' }
        }
      }
    });

    // Expected outcome: Connector value, units, quality, and commit state are visible together.
    // Acceptance criteria: The overlay contains the complete typed phrase, proving none of those distinctions relies on colour alone.
    expect(wrapper.get('.connector-value').text()).toContain('21.5 °C · good · committed');
    // Expected outcome: Both breakpoint positions have textual markers and accessible names.
    // Acceptance criteria: B/A are rendered and the node label announces both positions, protecting keyboard and non-visual debugging.
    expect(wrapper.findAll('.breakpoint-marker').map((marker) => marker.text())).toEqual([
      'B',
      'A'
    ]);
    expect(wrapper.attributes('aria-label')).toContain('breakpoints before and after');
  });
  /**
   * Purpose: Protects the behavioral contract that uses registry metadata and exposes an accessible node name and status.
   * Description: Exercises uses registry metadata and exposes an accessible node name and status from its arranged starting state and
   * verifies the observable results required by the scenario.
   */
  it('uses registry metadata and exposes an accessible node name and status', () => {
    const node = sampleFlows[0]!.nodes[0]!;
    const wrapper = mount(AppFlowNode, {
      props: {
        automation: 'flow-node-source',
        node,
        selected: false,
        status: 'running',
        statusValue: '21.5 °C'
      }
    });

    // Expected outcome: `wrapper.attributes('aria-label')` has the required value.
    // Acceptance criteria: `wrapper.attributes('aria-label')` must be `'Average temperature, Calculator node, running, 21.5 °C'`, because this condition proves that
    // uses registry metadata and exposes an accessible node name and status.
    expect(wrapper.attributes('aria-label')).toBe(
      'Average temperature, Calculator node, running, 21.5 °C'
    );

    // Expected outcome: `wrapper.attributes('data-node-category')` has the required value.
    // Acceptance criteria: `wrapper.attributes('data-node-category')` must be `'maths'`, because this condition proves that
    // uses registry metadata and exposes an accessible node name and status.
    expect(wrapper.attributes('data-node-category')).toBe('maths');

    // Expected outcome: `wrapper.get('.node-icon image'` has the required value.
    // Acceptance criteria: `wrapper.get('.node-icon image'` must be `'/icons/flow-nodes/calculator.svg'`, because this condition proves that
    // uses registry metadata and exposes an accessible node name and status.
    expect(wrapper.get('.node-icon image').attributes('href')).toBe(
      '/icons/flow-nodes/calculator.svg'
    );

    // Expected outcome: `wrapper.text()` includes the required value.
    // Acceptance criteria: `wrapper.text()` must contain `'Calculator'`, because this condition proves that
    // uses registry metadata and exposes an accessible node name and status.
    expect(wrapper.text()).toContain('Calculator');

    // Expected outcome: `wrapper.get('.node-status'` has the required value.
    // Acceptance criteria: `wrapper.get('.node-status'` must be `'running: 21.5 °C'`, because this condition proves that
    // uses registry metadata and exposes an accessible node name and status.
    expect(wrapper.get('.node-status').attributes('aria-label')).toBe('running: 21.5 °C');

    // Expected outcome: `wrapper.findAll('.node-marker')` contains the required number of entries.
    // Acceptance criteria: `wrapper.findAll('.node-marker')` must contain exactly 3 entries, because this condition proves that
    // uses registry metadata and exposes an accessible node name and status.
    expect(wrapper.findAll('.node-marker')).toHaveLength(3);

    // Expected outcome: `wrapper.find('.node-marker.orange rect'` has the required value.
    // Acceptance criteria: `wrapper.find('.node-marker.orange rect'` must be `true`, because this condition proves that
    // uses registry metadata and exposes an accessible node name and status.
    expect(wrapper.find('.node-marker.orange rect').exists()).toBe(true);

    // Expected outcome: `wrapper.find('.node-marker.green path'` has the required value.
    // Acceptance criteria: `wrapper.find('.node-marker.green path'` must be `true`, because this condition proves that
    // uses registry metadata and exposes an accessible node name and status.
    expect(wrapper.find('.node-marker.green path').exists()).toBe(true);

    // Expected outcome: `wrapper.find('.node-marker.blue circle'` has the required value.
    // Acceptance criteria: `wrapper.find('.node-marker.blue circle'` must be `true`, because this condition proves that
    // uses registry metadata and exposes an accessible node name and status.
    expect(wrapper.find('.node-marker.blue circle').exists()).toBe(true);

    // Expected outcome: `wrapper.findAll('rect.connector-port')` contains the required number of entries.
    // Acceptance criteria: `wrapper.findAll('rect.connector-port')` must contain exactly node.connectors.length entries, because this condition proves that
    // uses registry metadata and exposes an accessible node name and status.
    expect(wrapper.findAll('rect.connector-port')).toHaveLength(node.connectors.length);

    // Expected outcome: `wrapper.get('.node-body'` has the required value.
    // Acceptance criteria: `wrapper.get('.node-body'` must be `'170'`, because this condition proves that
    // uses registry metadata and exposes an accessible node name and status.
    expect(wrapper.get('.node-body').attributes('width')).toBe('170');

    // Expected outcome: `wrapper.findAll('.node-marker'` matches the required structure.
    // Acceptance criteria: `wrapper.findAll('.node-marker'` must equal `['translate(110 -8`, because this condition proves that
    // uses registry metadata and exposes an accessible node name and status.
    expect(wrapper.findAll('.node-marker').map((marker) => marker.attributes('transform'))).toEqual(
      ['translate(110 -8)', 'translate(130 -8)', 'translate(150 -8)']
    );

    // Expected outcome: `wrapper .findAll('.flow-connector'` has the required value.
    // Acceptance criteria: `wrapper .findAll('.flow-connector'` must be `true`, because this condition proves that
    // uses registry metadata and exposes an accessible node name and status.
    expect(
      wrapper
        .findAll('.flow-connector')
        .some((connector) => connector.attributes('transform')?.startsWith('translate(170 '))
    ).toBe(true);
  });

  /**
   * Purpose: Protects the behavioral contract that emits selection from keyboard activation.
   * Description: Exercises emits selection from keyboard activation from its arranged starting state and
   * verifies the observable results required by the scenario.
   */
  it('emits selection from keyboard activation', async () => {
    const wrapper = mount(AppFlowNode, {
      props: {
        automation: 'flow-node-source',
        node: sampleFlows[0]!.nodes[0]!,
        selected: false
      }
    });

    await wrapper.trigger('keydown', { key: 'Enter' });

    // Expected outcome: `wrapper.emitted('select')` matches the required structure.
    // Acceptance criteria: `wrapper.emitted('select')` must equal `[['temperature-average']]`, because this condition proves that
    // emits selection from keyboard activation.
    expect(wrapper.emitted('select')).toEqual([['temperature-average']]);
  });
});
