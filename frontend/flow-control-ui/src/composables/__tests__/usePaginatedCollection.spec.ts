import { nextTick, ref } from 'vue';
import { describe, expect, it } from 'vitest';

import { usePaginatedCollection } from '@/composables/usePaginatedCollection';

interface Item {
  name: string;
}

const makeItems = (count: number): Item[] =>
  Array.from({ length: count }, (_, index) => ({ name: `Flow ${String(index + 1).padStart(2, '0')}` }));

describe('usePaginatedCollection', () => {
  it('filters and paginates within the filtered result set', async () => {
    const source = ref(makeItems(25));
    const collection = usePaginatedCollection(source, {
      searchText: (item) => item.name,
      sortValue: (item) => item.name
    });

    expect(collection.items.value).toHaveLength(10);
    expect(collection.pageCount.value).toBe(3);

    collection.query.value = 'Flow 2';
    await nextTick();
    expect(collection.totalItems.value).toBe(6);
    expect(collection.page.value).toBe(1);
    expect(collection.items.value.map(({ name }) => name)).toEqual([
      'Flow 20',
      'Flow 21',
      'Flow 22',
      'Flow 23',
      'Flow 24',
      'Flow 25'
    ]);
  });

  it('sorts names, changes page size, and clamps invalid page requests', async () => {
    const source = ref(makeItems(25));
    const collection = usePaginatedCollection(source, {
      searchText: (item) => item.name,
      sortValue: (item) => item.name
    });

    collection.toggleSortDirection();
    expect(collection.items.value[0]?.name).toBe('Flow 25');

    collection.pageSize.value = 20;
    await nextTick();
    collection.setPage(2);
    expect(collection.items.value).toHaveLength(5);
    expect(collection.rangeStart.value).toBe(21);
    expect(collection.rangeEnd.value).toBe(25);

    collection.setPage(99);
    expect(collection.page.value).toBe(2);
    collection.setPage(0);
    expect(collection.page.value).toBe(1);
  });
});
