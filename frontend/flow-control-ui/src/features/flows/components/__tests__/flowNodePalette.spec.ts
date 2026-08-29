// @vitest-environment jsdom

import { describe, expect, it } from 'vitest';
import { mount } from '@vue/test-utils';

import AppFlowNodePalette, {
  filterNodeKinds,
  groupNodeKinds
} from '@/features/flows/components/AppFlowNodePalette.vue';
import { flowNodeKinds, paletteNodeKinds } from '@/features/flows/nodeKinds';

describe('node palette filtering and grouping', () => {
  it('offers one add action per function without learn actions', () => {
    const wrapper = mount(AppFlowNodePalette, {
      props: {}
    });

    const addActions = wrapper.findAll('button.palette-add-button');
    expect(addActions).toHaveLength(paletteNodeKinds.length);
    expect(paletteNodeKinds).toEqual(flowNodeKinds);
    expect(addActions.every((action) => action.classes('palette-add-button'))).toBe(true);
    expect(wrapper.text()).not.toContain('Learn');
    expect(wrapper.find('button.app-filter-apply').exists()).toBe(false);
  });

  it('updates the visible nodes when the search model changes', async () => {
    const wrapper = mount(AppFlowNodePalette);

    await wrapper.get('input[type="search"]').setValue('pulse');

    const addActions = wrapper.findAll('button.palette-add-button');
    expect(addActions).toHaveLength(1);
    expect(addActions[0]?.text()).toContain('Pulse');
  });

  /**
   * Purpose: Protects the behavioral contract that filters by label and category without case sensitivity.
   * Description: Exercises filters by label and category without case sensitivity from its arranged starting state and
   * verifies the observable results required by the scenario.
   */
  it('filters by label and category without case sensitivity', () => {
    // Expected outcome: `filterNodeKinds('PULSE'` matches the required structure.
    // Acceptance criteria: `filterNodeKinds('PULSE'` must equal `['pulse']`, because this condition proves that
    // filters by label and category without case sensitivity.
    expect(filterNodeKinds('PULSE').map(({ kind }) => kind)).toEqual(['pulse']);

    // Expected outcome: `filterNodeKinds('logic'` matches the required structure.
    // Acceptance criteria: `filterNodeKinds('logic'` must equal `[ 'and', 'comparator', 'if', 'nand', 'nor', 'not', 'or', 'xnor', 'xor' ]`, because this condition proves that
    // filters by label and category without case sensitivity.
    expect(filterNodeKinds('logic').map(({ kind }) => kind)).toEqual([
      'and',
      'comparator',
      'digitalConstant',
      'digitalInput',
      'digitalOutput',
      'if',
      'levelShifter',
      'memory',
      'nand',
      'nor',
      'numericConstant',
      'not',
      'or',
      'onDelay',
      'qualityGood',
      'risingEdge',
      'xnor',
      'xor'
    ]);

    // Expected outcome: `filterNodeKinds('override'` matches the required structure.
    // Acceptance criteria: `filterNodeKinds('override'` must equal `['override']`, because this condition proves that
    // filters by label and category without case sensitivity.
    expect(filterNodeKinds('override').map(({ kind }) => kind)).toEqual(['override']);

    // Expected outcome: `filterNodeKinds('missing')` matches the required structure.
    // Acceptance criteria: `filterNodeKinds('missing')` must equal `[]`, because this condition proves that
    // filters by label and category without case sensitivity.
    expect(filterNodeKinds('missing')).toEqual([]);
  });

  /**
   * Purpose: Protects the behavioral contract that groups registry entries by authoring category.
   * Description: Exercises groups registry entries by authoring category from its arranged starting state and
   * verifies the observable results required by the scenario.
   */
  it('groups registry entries by authoring category', () => {
    const groups = groupNodeKinds(filterNodeKinds(''));

    // Expected outcome: `Object.keys(groups` matches the required structure.
    // Acceptance criteria: `Object.keys(groups` must equal `['logic', 'maths', 'override', 'routing', 'timing']`, because this condition proves that
    // groups registry entries by authoring category.
    expect(Object.keys(groups).sort()).toEqual(['logic', 'maths', 'override', 'routing', 'timing']);

    // Expected outcome: `groups.override?.map(({ kind }) => kind)` matches the required structure.
    // Acceptance criteria: `groups.override?.map(({ kind }) => kind)` must equal `['override']`, because this condition proves that
    // groups registry entries by authoring category.
    expect(groups.override?.map(({ kind }) => kind)).toEqual(['override']);

    // Expected outcome: `groups.maths?.map(({ kind }) => kind)` matches the required structure.
    // Acceptance criteria: `groups.maths?.map(({ kind }) => kind)` must equal `[ 'average', 'calculator', 'clamp', 'line', 'max', 'min' ]`, because this condition proves that
    // groups registry entries by authoring category.
    expect(groups.maths?.map(({ kind }) => kind)).toEqual([
      'add',
      'average',
      'calculator',
      'clamp',
      'line',
      'max',
      'min'
    ]);
  });
});
