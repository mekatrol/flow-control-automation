// @vitest-environment jsdom

import { mount } from '@vue/test-utils';
import { describe, expect, it } from 'vitest';

import AppFlowDesignerToolbar from '@/features/flows/components/AppFlowDesignerToolbar.vue';

describe('FlowDesignerToolbar', () => {
  /**
   * Purpose: Protects the behavioral contract that uses distinct directional and terminal icons for all stacking operations.
   * Description: Exercises uses distinct directional and terminal icons for all stacking operations from its arranged starting state and
   * verifies the observable results required by the scenario.
   */
  it('uses distinct directional and terminal icons for all stacking operations', async () => {
    const wrapper = mount(AppFlowDesignerToolbar, {
      props: { selectedNodeId: 'node-1', canMoveFront: true, canMoveBack: true }
    });

    // Expected outcome: `wrapper.findAll('svg[data-icon]')` contains the required number of entries.
    // Acceptance criteria: `wrapper.findAll('svg[data-icon]')` must contain exactly 4 entries, because this condition proves that
    // uses distinct directional and terminal icons for all stacking operations.
    expect(wrapper.findAll('svg[data-icon]')).toHaveLength(4);

    // Expected outcome: `wrapper.findAll('svg[data-icon] path.direction')` contains the required number of entries.
    // Acceptance criteria: `wrapper.findAll('svg[data-icon] path.direction')` must contain exactly 4 entries, because this condition proves that
    // uses distinct directional and terminal icons for all stacking operations.
    expect(wrapper.findAll('svg[data-icon] path.direction')).toHaveLength(4);

    // Expected outcome: `wrapper.findAll('svg[data-icon] path.destination')` contains the required number of entries.
    // Acceptance criteria: `wrapper.findAll('svg[data-icon] path.destination')` must contain exactly 2 entries, because this condition proves that
    // uses distinct directional and terminal icons for all stacking operations.
    expect(wrapper.findAll('svg[data-icon] path.destination')).toHaveLength(2);

    // Expected outcome: `wrapper.find('svg[data-icon="front"] path.destination'` has the required value.
    // Acceptance criteria: `wrapper.find('svg[data-icon="front"] path.destination'` must be `true`, because this condition proves that
    // uses distinct directional and terminal icons for all stacking operations.
    expect(wrapper.find('svg[data-icon="front"] path.destination').exists()).toBe(true);

    // Expected outcome: `wrapper.find('svg[data-icon="back"] path.destination'` has the required value.
    // Acceptance criteria: `wrapper.find('svg[data-icon="back"] path.destination'` must be `true`, because this condition proves that
    // uses distinct directional and terminal icons for all stacking operations.
    expect(wrapper.find('svg[data-icon="back"] path.destination').exists()).toBe(true);

    const buttons = wrapper.findAll('button');
    for (const button of buttons) await button.trigger('click');

    // Expected outcome: `wrapper.emitted('reorder')` matches the required structure.
    // Acceptance criteria: `wrapper.emitted('reorder')` must equal `[['front'], ['forward'], ['backward'], ['back']]`, because this condition proves that
    // uses distinct directional and terminal icons for all stacking operations.
    expect(wrapper.emitted('reorder')).toEqual([['front'], ['forward'], ['backward'], ['back']]);
  });
});
