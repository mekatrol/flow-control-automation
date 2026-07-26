import { nextTick, ref } from 'vue';
import { describe, expect, it } from 'vitest';

import { usePaginatedCollection } from '@/composables/usePaginatedCollection';

interface Item {
  name: string;
}

const makeItems = (count: number): Item[] =>
  Array.from({ length: count }, (_, index) => ({
    name: `Flow ${String(index + 1).padStart(2, '0')}`
  }));

describe('usePaginatedCollection', () => {

  /**
   * Purpose: Protects the behavioral contract that filters and paginates within the filtered result set.
   * Description: Exercises filters and paginates within the filtered result set from its arranged starting state and
   * verifies the observable results required by the scenario.
   */
  it('filters and paginates within the filtered result set', async () => {
    const source = ref(makeItems(25));
    const collection = usePaginatedCollection(source, {
      searchText: (item) => item.name,
      sortValue: (item) => item.name
    });

    // Expected outcome: `collection.items.value` contains the required number of entries.
    // Acceptance criteria: `collection.items.value` must contain exactly 10 entries, because this condition proves that
    // filters and paginates within the filtered result set.
    expect(collection.items.value).toHaveLength(10);

    // Expected outcome: `collection.pageCount.value` has the required value.
    // Acceptance criteria: `collection.pageCount.value` must be `3`, because this condition proves that
    // filters and paginates within the filtered result set.
    expect(collection.pageCount.value).toBe(3);

    collection.query.value = 'Flow 2';
    await nextTick();

    // Expected outcome: `collection.totalItems.value` has the required value.
    // Acceptance criteria: `collection.totalItems.value` must be `6`, because this condition proves that
    // filters and paginates within the filtered result set.
    expect(collection.totalItems.value).toBe(6);

    // Expected outcome: `collection.page.value` has the required value.
    // Acceptance criteria: `collection.page.value` must be `1`, because this condition proves that
    // filters and paginates within the filtered result set.
    expect(collection.page.value).toBe(1);

    // Expected outcome: `collection.items.value.map(({ name }) => name)` matches the required structure.
    // Acceptance criteria: `collection.items.value.map(({ name }) => name)` must equal `[ 'Flow 20', 'Flow 21', 'Flow 22', 'Flow 23', 'Flow 24', 'Flow 25' ]`, because this condition proves that
    // filters and paginates within the filtered result set.
    expect(collection.items.value.map(({ name }) => name)).toEqual([
      'Flow 20',
      'Flow 21',
      'Flow 22',
      'Flow 23',
      'Flow 24',
      'Flow 25'
    ]);
  });

  /**
   * Purpose: Protects the behavioral contract that sorts names, changes page size, and clamps invalid page requests.
   * Description: Exercises sorts names, changes page size, and clamps invalid page requests from its arranged starting state and
   * verifies the observable results required by the scenario.
   */
  it('sorts names, changes page size, and clamps invalid page requests', async () => {
    const source = ref(makeItems(25));
    const collection = usePaginatedCollection(source, {
      searchText: (item) => item.name,
      sortValue: (item) => item.name
    });

    collection.toggleSortDirection();

    // Expected outcome: `collection.items.value[0]?.name` has the required value.
    // Acceptance criteria: `collection.items.value[0]?.name` must be `'Flow 25'`, because this condition proves that
    // sorts names, changes page size, and clamps invalid page requests.
    expect(collection.items.value[0]?.name).toBe('Flow 25');

    collection.pageSize.value = 20;
    await nextTick();
    collection.setPage(2);

    // Expected outcome: `collection.items.value` contains the required number of entries.
    // Acceptance criteria: `collection.items.value` must contain exactly 5 entries, because this condition proves that
    // sorts names, changes page size, and clamps invalid page requests.
    expect(collection.items.value).toHaveLength(5);

    // Expected outcome: `collection.rangeStart.value` has the required value.
    // Acceptance criteria: `collection.rangeStart.value` must be `21`, because this condition proves that
    // sorts names, changes page size, and clamps invalid page requests.
    expect(collection.rangeStart.value).toBe(21);

    // Expected outcome: `collection.rangeEnd.value` has the required value.
    // Acceptance criteria: `collection.rangeEnd.value` must be `25`, because this condition proves that
    // sorts names, changes page size, and clamps invalid page requests.
    expect(collection.rangeEnd.value).toBe(25);

    collection.setPage(99);

    // Expected outcome: `collection.page.value` has the required value.
    // Acceptance criteria: `collection.page.value` must be `2`, because this condition proves that
    // sorts names, changes page size, and clamps invalid page requests.
    expect(collection.page.value).toBe(2);
    collection.setPage(0);

    // Expected outcome: `collection.page.value` has the required value.
    // Acceptance criteria: `collection.page.value` must be `1`, because this condition proves that
    // sorts names, changes page size, and clamps invalid page requests.
    expect(collection.page.value).toBe(1);
  });
});
