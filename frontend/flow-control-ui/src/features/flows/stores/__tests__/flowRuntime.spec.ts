import { createPinia, setActivePinia } from 'pinia';
import { beforeEach, describe, expect, it } from 'vitest';

import { useFlowRuntimeStore } from '@/features/flows/stores/flowRuntime';

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

  /**
   * Purpose: Protects the behavioral contract that tracks pending, successful, and failed deployment transitions.
   * Description: Exercises tracks pending, successful, and failed deployment transitions from its arranged starting state and
   * verifies the observable results required by the scenario.
   */
  it('tracks pending, successful, and failed deployment transitions', () => {
    const store = useFlowRuntimeStore();
    store.beginDeployment(snapshot.flowId);

    // Expected outcome: `store.isDeploying(snapshot.flowId)` has the required value.
    // Acceptance criteria: `store.isDeploying(snapshot.flowId)` must be `true`, because this condition proves that
    // tracks pending, successful, and failed deployment transitions.
    expect(store.isDeploying(snapshot.flowId)).toBe(true);

    store.completeDeployment(snapshot);

    // Expected outcome: `store.isDeploying(snapshot.flowId)` has the required value.
    // Acceptance criteria: `store.isDeploying(snapshot.flowId)` must be `false`, because this condition proves that
    // tracks pending, successful, and failed deployment transitions.
    expect(store.isDeploying(snapshot.flowId)).toBe(false);

    // Expected outcome: `store.snapshotFor(snapshot.flowId)` matches the required structure.
    // Acceptance criteria: `store.snapshotFor(snapshot.flowId)` must equal `snapshot`, because this condition proves that
    // tracks pending, successful, and failed deployment transitions.
    expect(store.snapshotFor(snapshot.flowId)).toEqual(snapshot);

    // Expected outcome: `store.isConnected(snapshot.flowId)` has the required value.
    // Acceptance criteria: `store.isConnected(snapshot.flowId)` must be `true`, because this condition proves that
    // tracks pending, successful, and failed deployment transitions.
    expect(store.isConnected(snapshot.flowId)).toBe(true);

    store.beginDeployment(snapshot.flowId);
    store.failDeployment(snapshot.flowId, 'startup failed');

    // Expected outcome: `store.isDeploying(snapshot.flowId)` has the required value.
    // Acceptance criteria: `store.isDeploying(snapshot.flowId)` must be `false`, because this condition proves that
    // tracks pending, successful, and failed deployment transitions.
    expect(store.isDeploying(snapshot.flowId)).toBe(false);

    // Expected outcome: `store.deploymentError(snapshot.flowId)` has the required value.
    // Acceptance criteria: `store.deploymentError(snapshot.flowId)` must be `'startup failed'`, because this condition proves that
    // tracks pending, successful, and failed deployment transitions.
    expect(store.deploymentError(snapshot.flowId)).toBe('startup failed');
  });

  /**
   * Purpose: Protects the behavioral contract that clears stale node values when runtime connectivity is lost.
   * Description: Exercises clears stale node values when runtime connectivity is lost from its arranged starting state and
   * verifies the observable results required by the scenario.
   */
  it('clears stale node values when runtime connectivity is lost', () => {
    const store = useFlowRuntimeStore();
    store.applySnapshot(snapshot);

    store.disconnect(snapshot.flowId);

    // Expected outcome: `store.isConnected(snapshot.flowId)` has the required value.
    // Acceptance criteria: `store.isConnected(snapshot.flowId)` must be `false`, because this condition proves that
    // clears stale node values when runtime connectivity is lost.
    expect(store.isConnected(snapshot.flowId)).toBe(false);

    // Expected outcome: `store.snapshotFor(snapshot.flowId)` is not supplied.
    // Acceptance criteria: `store.snapshotFor(snapshot.flowId)` must be undefined, because this condition proves that
    // clears stale node values when runtime connectivity is lost.
    expect(store.snapshotFor(snapshot.flowId)).toBeUndefined();
  });
});
