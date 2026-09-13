import { afterEach, describe, expect, it, vi } from 'vitest';

import { pointSourceApi } from '@/features/pointSources/api/pointSourceApi';

describe('pointSourceApi', () => {
  afterEach(() => vi.unstubAllGlobals());

  it('sends an unsaved aggregate and nested point identity when testing a command', async () => {
    const result = {
      operation: 'command' as const,
      sourceId: 'source',
      pointId: 'point',
      mappingId: 'mapping',
      alias: 'value',
      value: 21.5,
      httpResponse: { statusCode: 200, body: '{"value":21.5}' }
    };
    const fetch = vi.fn<typeof globalThis.fetch>().mockResolvedValue(
      new Response(JSON.stringify(result), {
        status: 200,
        headers: { 'Content-Type': 'application/json' }
      })
    );
    vi.stubGlobal('fetch', fetch);

    await expect(
      pointSourceApi.testPoint(
        'source yaml',
        'point',
        'command',
        21.5,
        new AbortController().signal
      )
    ).resolves.toEqual(result);

    expect(fetch).toHaveBeenCalledWith('/api/point-sources/test-point', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        sourceYaml: 'source yaml',
        pointId: 'point',
        operation: 'command',
        value: 21.5
      }),
      signal: expect.any(AbortSignal)
    });
  });
});
