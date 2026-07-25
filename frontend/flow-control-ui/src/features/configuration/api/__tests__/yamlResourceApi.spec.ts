import { afterEach, describe, expect, it, vi } from 'vitest';
import {
  controllerTemplateConfigurationApi,
  pointConfigurationApi,
  pointGroupConfigurationApi,
  YamlResourceError
} from '@/features/configuration/api/yamlResourceApi';

afterEach(() => vi.unstubAllGlobals());

describe('YAML resource APIs', () => {
  it('sends revision-safe YAML writes and maps returned revisions', async () => {
    const fetch = vi.fn<typeof globalThis.fetch>().mockResolvedValue(
      new Response('schemaVersion: 1\n', {
        status: 200,
        headers: { ETag: '7', 'Content-Type': 'application/yaml' }
      })
    );
    vi.stubGlobal('fetch', fetch);

    const result = await pointConfigurationApi.update('room value', 'yaml', 6);

    expect(result).toEqual({ yaml: 'schemaVersion: 1\n', revision: 7 });
    expect(fetch).toHaveBeenCalledWith('/api/points/room%20value', {
      method: 'PUT',
      headers: { 'Content-Type': 'application/yaml', 'If-Match': '6' },
      body: 'yaml'
    });
  });

  it('preserves server diagnostics and conflict status', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn<typeof globalThis.fetch>().mockResolvedValue(
        new Response(
          JSON.stringify({
            message: 'stale revision',
            details: { diagnostics: [{ path: 'id', message: 'invalid' }] }
          }),
          { status: 409, headers: { 'Content-Type': 'application/json' } }
        )
      )
    );

    await expect(pointGroupConfigurationApi.delete('group', 1)).rejects.toMatchObject({
      message: 'stale revision',
      status: 409,
      details: { diagnostics: [{ path: 'id', message: 'invalid' }] }
    } satisfies Partial<YamlResourceError>);
  });

  it('uses dedicated runtime, validation, YAML and make-standalone paths', async () => {
    const fetch = vi
      .fn<typeof globalThis.fetch>()
      .mockResolvedValueOnce(
        new Response(
          JSON.stringify({
            pointId: 'point',
            value: null,
            quality: 'unavailable',
            reliability: 'disconnected',
            connectionState: 'disconnected',
            status: 'unavailable',
            diagnostic: 'offline'
          })
        )
      )
      .mockResolvedValueOnce(new Response(JSON.stringify({ valid: true, diagnostics: [] })))
      .mockResolvedValueOnce(new Response('schemaVersion: 1\n', { headers: { ETag: '1' } }))
      .mockResolvedValueOnce(new Response(null, { status: 200 }));
    vi.stubGlobal('fetch', fetch);

    expect((await pointConfigurationApi.runtime('point')).status).toBe('unavailable');
    expect(await controllerTemplateConfigurationApi.validate('yaml')).toEqual([]);
    expect((await controllerTemplateConfigurationApi.get('default')).revision).toBe(1);
    await pointGroupConfigurationApi.makeStandalone('group', 2);

    expect(fetch.mock.calls.map(([url]) => url)).toEqual([
      '/api/points/point/runtime',
      '/api/controller-templates/validate',
      '/api/controller-templates/default/yaml',
      '/api/point-groups/group/make-points-standalone?revision=2'
    ]);
  });
});
