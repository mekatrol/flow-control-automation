import { createPinia, setActivePinia } from 'pinia';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { CatalogueApiError, catalogueApi } from '@/features/catalogues/api/catalogueApi';
import { usePointsCatalogueStore } from '@/features/catalogues/stores/catalogues';

describe('catalogue stores', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    setActivePinia(createPinia());
  });

  /**
   * Purpose: Protects the behavioral contract that keeps the newest request result when responses arrive out of order.
   * Description: Exercises keeps the newest request result when responses arrive out of order from its arranged starting state and
   * verifies the observable results required by the scenario.
   */
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

    // Expected outcome: `store.result.totalItems` has the required value.
    // Acceptance criteria: `store.result.totalItems` must be `0`, because this condition proves that
    // keeps the newest request result when responses arrive out of order.
    expect(store.result.totalItems).toBe(0);
  });

  /**
   * Purpose: Protects the behavioral contract that presents the server's error message.
   * Description: Exercises presentation of an API error from its arranged starting state and
   * verifies the observable results required by the scenario.
   */
  it("presents the server's error message", async () => {
    vi.spyOn(catalogueApi, 'points').mockRejectedValue(
      new CatalogueApiError('Point catalogue was not found.', 404)
    );
    const store = usePointsCatalogueStore();
    await store.load({ filter: '', page: 1, pageSize: 10 });

    expect(store.error).toBe('Point catalogue was not found.');
  });
});
