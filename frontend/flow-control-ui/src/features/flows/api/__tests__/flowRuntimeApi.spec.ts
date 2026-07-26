import { afterEach, describe, expect, it, vi } from 'vitest';

import { flowRuntimeApi, parseFlowRuntimeSnapshot } from '@/features/flows/api/flowRuntimeApi';

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

describe('flow runtime API', () => {
  afterEach(() => vi.unstubAllGlobals());

  /**
   * Purpose: Protects the behavioral contract that validates snapshots and calls deploy and runtime endpoints.
   * Description: Exercises validates snapshots and calls deploy and runtime endpoints from its arranged starting state and
   * verifies the observable results required by the scenario.
   */
  it('validates snapshots and calls deploy and runtime endpoints', async () => {
    const fetchMock = vi
      .fn<typeof fetch>()
      .mockImplementation(async () => new Response(JSON.stringify(snapshot), { status: 200 }));
    vi.stubGlobal('fetch', fetchMock);

    // Expected outcome: `flowRuntimeApi.getRuntime('climate control')` matches the required structure.
    // Acceptance criteria: `flowRuntimeApi.getRuntime('climate control')` must equal `snapshot`, because this condition proves that
    // validates snapshots and calls deploy and runtime endpoints.
    await expect(flowRuntimeApi.getRuntime('climate control')).resolves.toEqual(snapshot);

    // Expected outcome: `flowRuntimeApi.deployFlow('climate control')` matches the required structure.
    // Acceptance criteria: `flowRuntimeApi.deployFlow('climate control')` must equal `snapshot`, because this condition proves that
    // validates snapshots and calls deploy and runtime endpoints.
    await expect(flowRuntimeApi.deployFlow('climate control')).resolves.toEqual(snapshot);

    // Expected outcome: `fetchMock` receives the required call in sequence.
    // Acceptance criteria: `fetchMock` must have the numbered call and arguments `1, '/api/flows/climate%20control/runtime', { method: 'GET', signal: undefined }`, because this condition proves that
    // validates snapshots and calls deploy and runtime endpoints.
    expect(fetchMock).toHaveBeenNthCalledWith(1, '/api/flows/climate%20control/runtime', {
      method: 'GET',
      signal: undefined
    });

    // Expected outcome: `fetchMock` receives the required call in sequence.
    // Acceptance criteria: `fetchMock` must have the numbered call and arguments `2, '/api/flows/climate%20control/deploy', { method: 'POST', signal: undefined }`, because this condition proves that
    // validates snapshots and calls deploy and runtime endpoints.
    expect(fetchMock).toHaveBeenNthCalledWith(2, '/api/flows/climate%20control/deploy', {
      method: 'POST',
      signal: undefined
    });
  });

  /**
   * Purpose: Protects the behavioral contract that rejects malformed flow, node, and value state.
   * Description: Exercises rejects malformed flow, node, and value state from its arranged starting state and
   * verifies the observable results required by the scenario.
   */
  it('rejects malformed flow, node, and value state', () => {

    // Expected outcome: The invalid operation is rejected.
    // Acceptance criteria: the operation must throw the asserted error, because this condition proves that
    // rejects malformed flow, node, and value state.
    expect(() => parseFlowRuntimeSnapshot({})).toThrow(/flow ID/);

    // Expected outcome: The invalid operation is rejected.
    // Acceptance criteria: the operation must throw the asserted error, because this condition proves that
    // rejects malformed flow, node, and value state.
    expect(() => parseFlowRuntimeSnapshot({ ...snapshot, state: 'starting' })).toThrow(
      /flow state/
    );

    // Expected outcome: The invalid operation is rejected.
    // Acceptance criteria: the operation must throw the asserted error, because this condition proves that
    // rejects malformed flow, node, and value state.
    expect(() =>
      parseFlowRuntimeSnapshot({
        ...snapshot,
        nodes: { unknown: { state: 'running', value: 4, updatedAt: snapshot.updatedAt } }
      })
    ).toThrow(/invalid value/);
  });

  /**
   * Purpose: Protects the behavioral contract that reports invalid responses and request failures consistently.
   * Description: Exercises reports invalid responses and request failures consistently from its arranged starting state and
   * verifies the observable results required by the scenario.
   */
  it('reports invalid responses and request failures consistently', async () => {
    vi.stubGlobal('fetch', vi.fn<typeof fetch>().mockResolvedValue(new Response('{}')));

    // Expected outcome: `flowRuntimeApi.getRuntime('bad')` contains the required object fields.
    // Acceptance criteria: `flowRuntimeApi.getRuntime('bad')` must match the object `{ kind: 'validation' }`, because this condition proves that
    // reports invalid responses and request failures consistently.
    await expect(flowRuntimeApi.getRuntime('bad')).rejects.toMatchObject({ kind: 'validation' });

    vi.stubGlobal(
      'fetch',
      vi.fn<typeof fetch>().mockResolvedValue(new Response('{}', { status: 503 }))
    );

    // Expected outcome: `flowRuntimeApi.deployFlow('offline')` contains the required object fields.
    // Acceptance criteria: `flowRuntimeApi.deployFlow('offline')` must match the object `{ kind: 'http', status: 503 }`, because this condition proves that
    // reports invalid responses and request failures consistently.
    await expect(flowRuntimeApi.deployFlow('offline')).rejects.toMatchObject({
      kind: 'http',
      status: 503
    });
  });
});
