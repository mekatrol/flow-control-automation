import { afterEach, describe, expect, it, vi } from 'vitest';
import {
  controllerTemplateConfigurationApi,
  pointConfigurationApi,
  pointGroupConfigurationApi,
  YamlResourceError
} from '@/features/configuration/api/yamlResourceApi';

afterEach(() => vi.unstubAllGlobals());

describe('YAML resource APIs', () => {

  /**
   * Purpose: Protects the behavioral contract that sends revision-safe YAML writes and maps returned revisions.
   * Description: Exercises sends revision-safe YAML writes and maps returned revisions from its arranged starting state and
   * verifies the observable results required by the scenario.
   */
  it('sends revision-safe YAML writes and maps returned revisions', async () => {
    const fetch = vi.fn<typeof globalThis.fetch>().mockResolvedValue(
      new Response('schemaVersion: 1\n', {
        status: 200,
        headers: { ETag: '7', 'Content-Type': 'application/yaml' }
      })
    );
    vi.stubGlobal('fetch', fetch);

    const result = await pointConfigurationApi.update('room value', 'yaml', 6);

    // Expected outcome: `result` matches the required structure.
    // Acceptance criteria: `result` must equal `{ yaml: 'schemaVersion: 1\n', revision: 7 }`, because this condition proves that
    // sends revision-safe YAML writes and maps returned revisions.
    expect(result).toEqual({ yaml: 'schemaVersion: 1\n', revision: 7 });

    // Expected outcome: `fetch` receives the required arguments.
    // Acceptance criteria: `fetch` must be called with `'/api/points/room%20value', { method: 'PUT', headers: { 'Content-Type': 'application/yaml', 'If-Match': '6' }, body: 'ya`, because this condition proves that
    // sends revision-safe YAML writes and maps returned revisions.
    expect(fetch).toHaveBeenCalledWith('/api/points/room%20value', {
      method: 'PUT',
      headers: { 'Content-Type': 'application/yaml', 'If-Match': '6' },
      body: 'yaml'
    });
  });

  /**
   * Purpose: Protects the behavioral contract that preserves server diagnostics and conflict status.
   * Description: Exercises preserves server diagnostics and conflict status from its arranged starting state and
   * verifies the observable results required by the scenario.
   */
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

    // Expected outcome: `pointGroupConfigurationApi.delete('group', 1)` contains the required object fields.
    // Acceptance criteria: `pointGroupConfigurationApi.delete('group', 1)` must match the object `{ message: 'stale revision', status: 409, details: { diagnostics: [{ path: 'id', message: 'invalid' }] } } satisfies Par`, because this condition proves that
    // preserves server diagnostics and conflict status.
    await expect(pointGroupConfigurationApi.delete('group', 1)).rejects.toMatchObject({
      message: 'stale revision',
      status: 409,
      details: { diagnostics: [{ path: 'id', message: 'invalid' }] }
    } satisfies Partial<YamlResourceError>);
  });

  /**
   * Purpose: Protects the behavioral contract that uses dedicated runtime, validation, YAML and make-standalone paths.
   * Description: Exercises uses dedicated runtime, validation, YAML and make-standalone paths from its arranged starting state and
   * verifies the observable results required by the scenario.
   */
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

    // Expected outcome: `(await pointConfigurationApi.runtime('point')` has the required value.
    // Acceptance criteria: `(await pointConfigurationApi.runtime('point')` must be `'unavailable'`, because this condition proves that
    // uses dedicated runtime, validation, YAML and make-standalone paths.
    expect((await pointConfigurationApi.runtime('point')).status).toBe('unavailable');

    // Expected outcome: `await controllerTemplateConfigurationApi.validate('yaml')` matches the required structure.
    // Acceptance criteria: `await controllerTemplateConfigurationApi.validate('yaml')` must equal `[]`, because this condition proves that
    // uses dedicated runtime, validation, YAML and make-standalone paths.
    expect(await controllerTemplateConfigurationApi.validate('yaml')).toEqual([]);

    // Expected outcome: `(await controllerTemplateConfigurationApi.get('default')` has the required value.
    // Acceptance criteria: `(await controllerTemplateConfigurationApi.get('default')` must be `1`, because this condition proves that
    // uses dedicated runtime, validation, YAML and make-standalone paths.
    expect((await controllerTemplateConfigurationApi.get('default')).revision).toBe(1);
    await pointGroupConfigurationApi.makeStandalone('group', 2);

    // Expected outcome: `fetch.mock.calls.map(([url]) => url)` matches the required structure.
    // Acceptance criteria: `fetch.mock.calls.map(([url]) => url)` must equal `[ '/api/points/point/runtime', '/api/controller-templates/validate', '/api/controller-templates/default/yaml', '/api/poi`, because this condition proves that
    // uses dedicated runtime, validation, YAML and make-standalone paths.
    expect(fetch.mock.calls.map(([url]) => url)).toEqual([
      '/api/points/point/runtime',
      '/api/controller-templates/validate',
      '/api/controller-templates/default/yaml',
      '/api/point-groups/group/make-points-standalone?revision=2'
    ]);
  });
});
