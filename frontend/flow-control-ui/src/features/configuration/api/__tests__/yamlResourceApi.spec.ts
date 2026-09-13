import { afterEach, describe, expect, it, vi } from 'vitest';
import {
  controllerTemplateConfigurationApi,
  YamlResourceError
} from '@/features/configuration/api/yamlResourceApi';

afterEach(() => vi.unstubAllGlobals());

describe('controller template YAML API', () => {
  it('sends revision-safe YAML writes and maps returned revisions', async () => {
    const fetch = vi
      .fn<typeof globalThis.fetch>()
      .mockResolvedValue(
        new Response('schemaVersion: 1\n', { status: 200, headers: { ETag: '7' } })
      );
    vi.stubGlobal('fetch', fetch);

    const result = await controllerTemplateConfigurationApi.update('custom template', 'yaml', 6);

    expect(result).toEqual({ yaml: 'yaml', revision: 7 });
    expect(fetch).toHaveBeenCalledWith('/api/controller-templates/custom%20template', {
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

    await expect(controllerTemplateConfigurationApi.delete('template', 1)).rejects.toMatchObject({
      message: 'stale revision',
      status: 409,
      details: { diagnostics: [{ path: 'id', message: 'invalid' }] }
    } satisfies Partial<YamlResourceError>);
  });

  it('uses dedicated validation and YAML paths', async () => {
    const fetch = vi
      .fn<typeof globalThis.fetch>()
      .mockResolvedValueOnce(new Response(JSON.stringify({ valid: true, diagnostics: [] })))
      .mockResolvedValueOnce(new Response('schemaVersion: 1\n', { headers: { ETag: '1' } }));
    vi.stubGlobal('fetch', fetch);

    expect(await controllerTemplateConfigurationApi.validate('yaml')).toEqual([]);
    expect((await controllerTemplateConfigurationApi.get('default')).revision).toBe(1);
    expect(fetch.mock.calls.map(([url]) => url)).toEqual([
      '/api/controller-templates/validate',
      '/api/controller-templates/default/yaml'
    ]);
  });
});
