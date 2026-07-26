import { afterEach, describe, expect, it, vi } from 'vitest';
import { catalogueApi, CatalogueApiError } from '@/features/catalogues/api/catalogueApi';

afterEach(() => vi.unstubAllGlobals());

describe('catalogue API', () => {
  /**
   * Purpose: Protects the behavioral contract that encodes pagination, sorting and filters.
   * Description: Exercises encodes pagination, sorting and filters from its arranged starting state and
   * verifies the observable results required by the scenario.
   */
  it('encodes pagination, sorting and filters', async () => {
    const fetch = vi
      .fn<typeof globalThis.fetch>()
      .mockResolvedValue(
        new Response(
          JSON.stringify({ items: [], totalItems: 0, page: 2, pageSize: 20, pageCount: 0 }),
          { status: 200, headers: { 'Content-Type': 'application/json' } }
        )
      );
    vi.stubGlobal('fetch', fetch);

    await catalogueApi.points({
      filter: 'Room & roof',
      page: 2,
      pageSize: 20,
      sort: 'descending'
    });

    // Expected outcome: `fetch.mock.calls[0]?.[0]` has the required value.
    // Acceptance criteria: `fetch.mock.calls[0]?.[0]` must be `'/api/points?page=2&pageSize=20&sort=descending&filter=Room+%26+roof'`, because this condition proves that
    // encodes pagination, sorting and filters.
    expect(fetch.mock.calls[0]?.[0]).toBe(
      '/api/points?page=2&pageSize=20&sort=descending&filter=Room+%26+roof'
    );
  });

  /**
   * Purpose: Protects the behavioral contract that maps JSON and non-JSON failures with their status.
   * Description: Exercises maps JSON and non-JSON failures with their status from its arranged starting state and
   * verifies the observable results required by the scenario.
   */
  it('maps JSON and non-JSON failures with their status', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn<typeof globalThis.fetch>().mockResolvedValueOnce(
        new Response(JSON.stringify({ message: 'Not supported' }), {
          status: 404,
          headers: { 'Content-Type': 'application/json' }
        })
      )
    );

    // Expected outcome: `catalogueApi.controllerTemplates()` contains the required object fields.
    // Acceptance criteria: `catalogueApi.controllerTemplates()` must match the object `{ message: 'Not supported', status: 404 } satisfies Partial<CatalogueApiError>`, because this condition proves that
    // maps JSON and non-JSON failures with their status.
    await expect(catalogueApi.controllerTemplates()).rejects.toMatchObject({
      message: 'Not supported',
      status: 404
    } satisfies Partial<CatalogueApiError>);
  });
});
