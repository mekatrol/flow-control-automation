import { afterEach, describe, expect, it, vi } from 'vitest';

import { flowRuntimeApi, parseFlowRuntimeSnapshot } from '../flowRuntimeApi';

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

  it('validates snapshots and calls deploy and runtime endpoints', async () => {
    const fetchMock = vi
      .fn<typeof fetch>()
      .mockImplementation(async () => new Response(JSON.stringify(snapshot), { status: 200 }));
    vi.stubGlobal('fetch', fetchMock);

    await expect(flowRuntimeApi.getRuntime('climate control')).resolves.toEqual(snapshot);
    await expect(flowRuntimeApi.deployFlow('climate control')).resolves.toEqual(snapshot);
    expect(fetchMock).toHaveBeenNthCalledWith(1, '/api/flows/climate%20control/runtime', {
      method: 'GET',
      signal: undefined
    });
    expect(fetchMock).toHaveBeenNthCalledWith(2, '/api/flows/climate%20control/deploy', {
      method: 'POST',
      signal: undefined
    });
  });

  it('rejects malformed flow, node, and value state', () => {
    expect(() => parseFlowRuntimeSnapshot({})).toThrow(/flow ID/);
    expect(() => parseFlowRuntimeSnapshot({ ...snapshot, state: 'starting' })).toThrow(
      /flow state/
    );
    expect(() =>
      parseFlowRuntimeSnapshot({
        ...snapshot,
        nodes: { unknown: { state: 'running', value: 4, updatedAt: snapshot.updatedAt } }
      })
    ).toThrow(/invalid value/);
  });

  it('reports invalid responses and request failures consistently', async () => {
    vi.stubGlobal('fetch', vi.fn<typeof fetch>().mockResolvedValue(new Response('{}')));
    await expect(flowRuntimeApi.getRuntime('bad')).rejects.toMatchObject({ kind: 'validation' });

    vi.stubGlobal(
      'fetch',
      vi.fn<typeof fetch>().mockResolvedValue(new Response('{}', { status: 503 }))
    );
    await expect(flowRuntimeApi.deployFlow('offline')).rejects.toMatchObject({
      kind: 'http',
      status: 503
    });
  });
});
