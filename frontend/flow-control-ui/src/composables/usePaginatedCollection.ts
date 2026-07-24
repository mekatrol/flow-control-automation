import { computed, ref, watch, type ComputedRef, type Ref } from 'vue';

export type SortDirection = 'ascending' | 'descending';

interface PaginatedCollectionOptions<T> {
  searchText: (item: T) => string;
  sortValue: (item: T) => string;
  initialPage?: number;
  initialPageSize?: number;
  initialQuery?: string;
  initialSortDirection?: SortDirection;
}

interface PaginatedCollection<T> {
  query: Ref<string>;
  page: Ref<number>;
  pageSize: Ref<number>;
  sortDirection: Ref<SortDirection>;
  filteredItems: ComputedRef<T[]>;
  totalItems: ComputedRef<number>;
  pageCount: ComputedRef<number>;
  items: ComputedRef<T[]>;
  rangeStart: ComputedRef<number>;
  rangeEnd: ComputedRef<number>;
  setPage: (nextPage: number) => void;
  toggleSortDirection: () => void;
}

export const usePaginatedCollection = <T>(
  source: Ref<readonly T[]>,
  options: PaginatedCollectionOptions<T>
): PaginatedCollection<T> => {
  const query = ref(options.initialQuery ?? '');
  const page = ref(Math.max(1, options.initialPage ?? 1));
  const pageSize = ref(options.initialPageSize ?? 10);
  const sortDirection = ref<SortDirection>(options.initialSortDirection ?? 'ascending');

  const filteredItems = computed(() => {
    const normalizedQuery = query.value.trim().toLocaleLowerCase();
    const matchingItems = normalizedQuery
      ? source.value.filter((item) =>
          options.searchText(item).toLocaleLowerCase().includes(normalizedQuery)
        )
      : [...source.value];

    return matchingItems.sort((left, right) => {
      const comparison = options.sortValue(left).localeCompare(options.sortValue(right), undefined, {
        numeric: true,
        sensitivity: 'base'
      });
      return sortDirection.value === 'ascending' ? comparison : -comparison;
    });
  });

  const totalItems = computed(() => filteredItems.value.length);
  const pageCount = computed(() => Math.max(1, Math.ceil(totalItems.value / pageSize.value)));
  const items = computed(() => {
    const start = (page.value - 1) * pageSize.value;
    return filteredItems.value.slice(start, start + pageSize.value);
  });
  const rangeStart = computed(() => (totalItems.value === 0 ? 0 : (page.value - 1) * pageSize.value + 1));
  const rangeEnd = computed(() => Math.min(page.value * pageSize.value, totalItems.value));

  const setPage = (nextPage: number): void => {
    page.value = Math.min(Math.max(1, nextPage), pageCount.value);
  };

  const toggleSortDirection = (): void => {
    sortDirection.value = sortDirection.value === 'ascending' ? 'descending' : 'ascending';
    page.value = 1;
  };

  watch(query, () => {
    page.value = 1;
  });
  watch(pageSize, () => {
    page.value = 1;
  });
  watch(pageCount, (nextPageCount) => {
    if (page.value > nextPageCount) page.value = nextPageCount;
  });

  return {
    query,
    page,
    pageSize,
    sortDirection,
    filteredItems,
    totalItems,
    pageCount,
    items,
    rangeStart,
    rangeEnd,
    setPage,
    toggleSortDirection
  };
};
