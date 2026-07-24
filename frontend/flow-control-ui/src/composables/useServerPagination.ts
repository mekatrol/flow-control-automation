import { computed, ref, watch, type ComputedRef, type Ref } from 'vue';

import type { SortDirection } from '@/composables/usePaginatedCollection';

interface ServerPaginationOptions {
  initialQuery?: string;
  initialPage?: number;
  initialPageSize?: number;
  initialSortDirection?: SortDirection;
}

interface PageMetadata {
  totalItems: number;
  page: number;
  pageCount: number;
}

interface ServerPagination {
  query: Ref<string>;
  page: Ref<number>;
  pageSize: Ref<number>;
  sortDirection: Ref<SortDirection>;
  totalItems: Ref<number>;
  pageCount: Ref<number>;
  rangeStart: ComputedRef<number>;
  rangeEnd: ComputedRef<number>;
  setPage: (nextPage: number) => void;
  toggleSortDirection: () => void;
  applyPageMetadata: (metadata: PageMetadata) => void;
}

export const useServerPagination = (options: ServerPaginationOptions = {}): ServerPagination => {
  const query = ref(options.initialQuery ?? '');
  const page = ref(Math.max(1, options.initialPage ?? 1));
  const pageSize = ref(options.initialPageSize ?? 10);
  const sortDirection = ref<SortDirection>(options.initialSortDirection ?? 'ascending');
  const totalItems = ref(0);
  const pageCount = ref(1);
  const rangeStart = computed(() =>
    totalItems.value === 0 ? 0 : (page.value - 1) * pageSize.value + 1
  );
  const rangeEnd = computed(() => Math.min(page.value * pageSize.value, totalItems.value));

  const setPage = (nextPage: number): void => {
    page.value = Math.min(Math.max(1, nextPage), pageCount.value);
  };
  const toggleSortDirection = (): void => {
    sortDirection.value = sortDirection.value === 'ascending' ? 'descending' : 'ascending';
  };
  const applyPageMetadata = (metadata: PageMetadata): void => {
    totalItems.value = metadata.totalItems;
    pageCount.value = metadata.pageCount;
    page.value = metadata.page;
  };

  watch(
    [query, pageSize, sortDirection],
    () => {
      page.value = 1;
    },
    { flush: 'sync' }
  );

  return {
    query,
    page,
    pageSize,
    sortDirection,
    totalItems,
    pageCount,
    rangeStart,
    rangeEnd,
    setPage,
    toggleSortDirection,
    applyPageMetadata
  };
};
