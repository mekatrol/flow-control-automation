import { describe, expect, it } from 'vitest';

import { filterNodeKinds, groupNodeKinds } from '../FlowNodePalette.vue';

describe('node palette filtering and grouping', () => {
  it('filters by label and category without case sensitivity', () => {
    expect(filterNodeKinds('PULSE').map(({ kind }) => kind)).toEqual(['pulse']);
    expect(filterNodeKinds('logic').map(({ kind }) => kind)).toEqual(['override']);
    expect(filterNodeKinds('missing')).toEqual([]);
  });

  it('groups registry entries by authoring category', () => {
    const groups = groupNodeKinds(filterNodeKinds(''));

    expect(Object.keys(groups).sort()).toEqual(['logic', 'maths', 'routing', 'timing']);
    expect(groups.maths?.map(({ kind }) => kind)).toEqual(['calculator']);
  });
});
