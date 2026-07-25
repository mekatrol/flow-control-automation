import { createPinia, setActivePinia } from 'pinia';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { CatalogueApiError, catalogueApi } from '@/features/catalogues/api/catalogueApi';
import { usePointsCatalogueStore } from '@/features/catalogues/stores/catalogues';

describe('catalogue stores', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    setActivePinia(createPinia());
  });

  it('keeps the newest request result when responses arrive out of order', async () => {
    const pending: Array<(value: never) => void> = [];
    vi.spyOn(catalogueApi, 'points').mockImplementation(
      () => new Promise((resolve) => pending.push(resolve as (value: never) => void))
    );
    const store = usePointsCatalogueStore();
    const first = store.load({ filter: 'first', page: 1, pageSize: 10 });
    const second = store.load({ filter: 'second', page: 1, pageSize: 10 });
    pending[1]?.({
      items: [],
      totalItems: 0,
      page: 1,
      pageSize: 10,
      pageCount: 0
    } as never);
    await second;
    pending[0]?.({
      items: [],
      totalItems: 9,
      page: 1,
      pageSize: 10,
      pageCount: 1
    } as never);
    await first;
    expect(store.result.totalItems).toBe(0);
  });

  it('presents an actionable unavailable state for older servers', async () => {
    vi.spyOn(catalogueApi, 'points').mockRejectedValue(new CatalogueApiError('not found', 404));
    const store = usePointsCatalogueStore();
    await store.load({ filter: '', page: 1, pageSize: 10 });
    expect(store.unavailable).toBe(true);
    expect(store.error).toMatch(/does not support/);
  });
});
