// @vitest-environment jsdom

import { describe, expect, it, vi } from 'vitest';
import { mount } from '@vue/test-utils';

import AppFlowNodePalette, {
  filterNodeTypes,
  groupNodeTypes
} from '@/features/flows/components/AppFlowNodePalette.vue';
import { flowNodeTypes, paletteNodeTypes } from '@/features/flows/nodeTypes';

describe('node palette filtering and grouping', () => {
  it('offers one add action per function without learn actions', () => {
    const wrapper = mount(AppFlowNodePalette, {
      props: {}
    });

    const addActions = wrapper.findAll('button.palette-add-button');
    expect(addActions).toHaveLength(paletteNodeTypes.length);
    expect(paletteNodeTypes).toEqual(flowNodeTypes);
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

  it('uses the rendered canvas node as the drag image', async () => {
    const wrapper = mount(AppFlowNodePalette);
    const average = wrapper
      .findAll('button.palette-add-button')
      .find((action) => action.text().includes('Average'));
    const setDragImage = vi.fn<(image: Element, x: number, y: number) => void>();
    const dataTransfer = {
      effectAllowed: 'none',
      setData: vi.fn<(format: string, data: string) => void>(),
      setDragImage
    };

    await average?.trigger('dragstart', { dataTransfer });

    const preview = wrapper.get(
      'svg.palette-drag-preview:has(#flow-node-tooltip-palette-preview-average)'
    );
    expect(preview.attributes('viewBox')).toBe('-10 -10 92 92');
    expect(setDragImage).toHaveBeenCalledWith(preview.element, 46, 46);
  });

  /**
   * Purpose: Protects the behavioral contract that filters by label and category without case sensitivity.
   * Description: Exercises filters by label and category without case sensitivity from its arranged starting state and
   * verifies the observable results required by the scenario.
   */
  it('filters by label and category without case sensitivity', () => {
    // Expected outcome: `filterNodeTypes('PULSE'` matches the required structure.
    // Acceptance criteria: `filterNodeTypes('PULSE'` must equal `['pulse']`, because this condition proves that
    // filters by label and category without case sensitivity.
    expect(filterNodeTypes('PULSE').map(({ nodeType }) => nodeType)).toEqual(['pulse']);

    expect(filterNodeTypes('CONTROL')).not.toHaveLength(0);
    expect(filterNodeTypes('io').map(({ nodeType }) => nodeType)).toEqual([
      'analogInput',
      'analogOutput',
      'analogVirtual',
      'digitalConstant',
      'digitalInput',
      'digitalOutput',
      'digitalVirtual',
      'analogConstant'
    ]);

    // Expected outcome: `filterNodeTypes('missing')` matches the required structure.
    // Acceptance criteria: `filterNodeTypes('missing')` must equal `[]`, because this condition proves that
    // filters by label and category without case sensitivity.
    expect(filterNodeTypes('missing')).toEqual([]);
  });

  /**
   * Purpose: Protects the behavioral contract that groups registry entries by authoring category.
   * Description: Exercises groups registry entries by authoring category from its arranged starting state and
   * verifies the observable results required by the scenario.
   */
  it('groups registry entries by authoring category', () => {
    const groups = groupNodeTypes(filterNodeTypes(''));

    expect(Object.keys(groups)).toEqual(['io', 'control', 'timing', 'maths']);

    for (const definitions of Object.values(groups)) {
      const labels = definitions?.map(({ label }) => label) ?? [];
      expect(labels).toEqual([...labels].sort((left, right) => left.localeCompare(right)));
    }

    expect(groups.io?.map(({ nodeType }) => nodeType)).toEqual([
      'analogConstant',
      'analogInput',
      'analogOutput',
      'analogVirtual',
      'digitalConstant',
      'digitalInput',
      'digitalOutput',
      'digitalVirtual'
    ]);

    // Expected outcome: `groups.maths?.map(({ nodeType }) => nodeType)` matches the required structure.
    // Acceptance criteria: `groups.maths?.map(({ nodeType }) => nodeType)` must equal `[ 'average', 'calculator', 'clamp', 'line', 'max', 'min' ]`, because this condition proves that
    // groups registry entries by authoring category.
    expect(groups.maths?.map(({ nodeType }) => nodeType)).toEqual([
      'add',
      'average',
      'calculator',
      'clamp',
      'divide',
      'line',
      'max',
      'min',
      'multiply',
      'negate',
      'power',
      'subtract'
    ]);
  });
});
