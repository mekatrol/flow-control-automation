import { afterEach, describe, expect, it, vi } from 'vitest';

import { sampleFlows } from '../../__tests__/fixtures/sampleFlows';
import { FlowApiError, flowApi } from '../flowApi';

const response = (body: unknown, status = 200): Response =>
  new Response(JSON.stringify(body), { status, headers: { 'content-type': 'application/json' } });

describe('flow API client', () => {
  afterEach(() => vi.unstubAllGlobals());

  it('validates a successful response and sends a serialised save', async () => {
    const fetchMock = vi
      .fn<typeof fetch>()
      .mockResolvedValueOnce(response(sampleFlows[0]))
      .mockResolvedValueOnce(response(sampleFlows[0]));
    vi.stubGlobal('fetch', fetchMock);

    await expect(flowApi.getFlow('climate control')).resolves.toEqual(sampleFlows[0]);
    await expect(flowApi.saveFlow(sampleFlows[0]!)).resolves.toEqual(sampleFlows[0]);
    expect(fetchMock).toHaveBeenNthCalledWith(1, '/api/flows/climate%20control', {
      method: 'GET',
      signal: undefined
    });
    expect(fetchMock.mock.calls[1]?.[1]).toMatchObject({
      method: 'PUT',
      body: JSON.stringify(sampleFlows[0])
    });
  });

  it('lists, creates, and deletes flows through typed endpoints', async () => {
    const fetchMock = vi
      .fn<typeof fetch>()
      .mockResolvedValueOnce(response(sampleFlows))
      .mockResolvedValueOnce(response(sampleFlows[0]))
      .mockResolvedValueOnce(new Response(null, { status: 204 }));
    vi.stubGlobal('fetch', fetchMock);

    await expect(flowApi.listFlows()).resolves.toEqual(sampleFlows);
    await expect(flowApi.createFlow('Climate control')).resolves.toEqual(sampleFlows[0]);
    await expect(flowApi.deleteFlow('climate control')).resolves.toBeUndefined();
    expect(fetchMock).toHaveBeenNthCalledWith(2, '/api/flows', {
      method: 'POST',
      headers: { 'content-type': 'application/json' },
      body: JSON.stringify({ name: 'Climate control' }),
      signal: undefined
    });
    expect(fetchMock).toHaveBeenNthCalledWith(3, '/api/flows/climate%20control', {
      method: 'DELETE',
      signal: undefined
    });
  });

  it('reports validation and HTTP failures consistently', async () => {
    vi.stubGlobal('fetch', vi.fn<typeof fetch>().mockResolvedValue(response({ nope: true })));
    await expect(flowApi.getFlow('bad')).rejects.toMatchObject({ kind: 'validation' });

    vi.stubGlobal(
      'fetch',
      vi.fn<typeof fetch>().mockResolvedValue(response({ message: 'runtime unavailable' }, 503))
    );
    await expect(flowApi.getFlow('offline')).rejects.toMatchObject({
      kind: 'http',
      status: 503,
      message: 'Flow request failed: runtime unavailable'
    });

    vi.stubGlobal('fetch', vi.fn<typeof fetch>().mockResolvedValue(response({ nope: true })));
    await expect(flowApi.listFlows()).rejects.toMatchObject({ kind: 'validation' });
  });

  it('reports network failure and cancellation separately', async () => {
    vi.stubGlobal('fetch', vi.fn<typeof fetch>().mockRejectedValue(new TypeError('network down')));
    await expect(flowApi.getFlow('offline')).rejects.toMatchObject({ kind: 'network' });

    vi.stubGlobal(
      'fetch',
      vi.fn<typeof fetch>().mockRejectedValue(new DOMException('request aborted', 'AbortError'))
    );
    await expect(flowApi.getFlow('cancelled')).rejects.toEqual(
      new FlowApiError('cancelled', 'The flow request was cancelled.')
    );
  });
});
