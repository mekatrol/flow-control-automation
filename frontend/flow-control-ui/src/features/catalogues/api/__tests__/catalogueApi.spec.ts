import { afterEach, describe, expect, it, vi } from 'vitest';
import { catalogueApi, CatalogueApiError } from '@/features/catalogues/api/catalogueApi';

afterEach(() => vi.unstubAllGlobals());

describe('catalogue API', () => {
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

    expect(fetch.mock.calls[0]?.[0]).toBe(
      '/api/points?page=2&pageSize=20&sort=descending&filter=Room+%26+roof'
    );
  });

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
    await expect(catalogueApi.controllerTemplates()).rejects.toMatchObject({
      message: 'Not supported',
      status: 404
    } satisfies Partial<CatalogueApiError>);
  });
});
