import { createPinia, setActivePinia } from 'pinia';
import { beforeEach, describe, expect, it } from 'vitest';

import { useFlowRuntimeStore } from '../flowRuntime';

const snapshot = {
  flowId: 'climate-control',
  state: 'running' as const,
  updatedAt: '2026-07-14T08:00:00+10:00',
  nodes: {
    'temperature-average': {
      state: 'running' as const,
      value: '22.4 C',
      updatedAt: '2026-07-14T08:00:00+10:00'
    }
  }
};

describe('flow runtime store', () => {
  beforeEach(() => setActivePinia(createPinia()));

  it('tracks pending, successful, and failed deployment transitions', () => {
    const store = useFlowRuntimeStore();
    store.beginDeployment(snapshot.flowId);
    expect(store.isDeploying(snapshot.flowId)).toBe(true);

    store.completeDeployment(snapshot);
    expect(store.isDeploying(snapshot.flowId)).toBe(false);
    expect(store.snapshotFor(snapshot.flowId)).toEqual(snapshot);
    expect(store.isConnected(snapshot.flowId)).toBe(true);

    store.beginDeployment(snapshot.flowId);
    store.failDeployment(snapshot.flowId, 'startup failed');
    expect(store.isDeploying(snapshot.flowId)).toBe(false);
    expect(store.deploymentError(snapshot.flowId)).toBe('startup failed');
  });

  it('clears stale node values when runtime connectivity is lost', () => {
    const store = useFlowRuntimeStore();
    store.applySnapshot(snapshot);

    store.disconnect(snapshot.flowId);

    expect(store.isConnected(snapshot.flowId)).toBe(false);
    expect(store.snapshotFor(snapshot.flowId)).toBeUndefined();
  });
});
