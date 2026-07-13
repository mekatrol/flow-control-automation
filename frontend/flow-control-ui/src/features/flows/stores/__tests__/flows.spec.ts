import { createPinia, setActivePinia } from 'pinia';
import { beforeEach, describe, expect, it } from 'vitest';

import { useFlowsStore } from '../flows';

describe('flows store', () => {
  beforeEach(() => {
    setActivePinia(createPinia());
  });

  it('selects a known flow', () => {
    const store = useFlowsStore();

    store.selectFlow('climate-control');

    expect(store.activeFlow?.name).toBe('Climate control');
  });

  it('clears the selection for an unknown flow', () => {
    const store = useFlowsStore();
    store.selectFlow('climate-control');

    store.selectFlow('missing');

    expect(store.activeFlowId).toBeUndefined();
    expect(store.activeFlow).toBeUndefined();
  });
});
