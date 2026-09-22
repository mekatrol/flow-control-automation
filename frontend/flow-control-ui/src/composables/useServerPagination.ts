import { computed, ref, watch, type ComputedRef, type Ref } from 'vue';
import {
  useRoute,
  useRouter,
  type LocationQuery,
  type LocationQueryRaw,
  type LocationQueryValue
} from 'vue-router';

import type { SortDirection } from '@/composables/usePaginatedCollection';
import type { ListQuery, ListRow, ListSort } from '@/models';

/** Initial values for lists whose filtering and pagination are performed by an API. */
interface ServerPaginationOptions {
  /** Text sent to the server's filter/search parameter. */
  initialQuery?: string;
  /** One-based page number. Values below one are normalised to the first page. */
  initialPage?: number;
  /** Number of records requested per page. */
  initialPageSize?: number;
  /** Initial direction for the list's fixed sort field. */
  initialSortDirection?: SortDirection;
}

/** Pagination values returned by the server with a page of records. */
interface PageMetadata {
  /** Number of records matching the current server-side filter. */
  totalItems: number;
  /** Authoritative one-based page, which may have been clamped by the server. */
  page: number;
  /** Number of pages available for the current filter and page size. */
  pageCount: number;
}

/** Reactive state and operations used by a conventional server-paginated list. */
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

/** Rules needed to validate and serialise a complete AppListView query. */
interface ServerListQueryOptions<TRow extends ListRow> {
  /** Canonical state used whenever a URL value is absent or invalid. */
  defaults: ListQuery<TRow>;
  /** Page sizes accepted by both the list control and its backing endpoint. */
  pageSizeOptions: readonly number[];
  /** Row keys the endpoint allows clients to sort by. */
  sortableColumns: readonly Extract<keyof TRow, string>[];
}

/** A bookmarkable list query and its explicit update operation. */
interface ServerListQuery<TRow extends ListRow> {
  query: Ref<ListQuery<TRow>>;
  setQuery: (nextQuery: ListQuery<TRow>) => void;
}

// Vue Router represents repeated query parameters as arrays. List settings are
// deliberately single-valued, so arrays are treated as malformed rather than
// silently choosing the first or last value.
type QueryValue = LocationQueryValue | LocationQueryValue[] | undefined;

const singleQueryValue = (value: QueryValue): string | undefined =>
  typeof value === 'string' ? value : undefined;

/** Parse a strictly positive, safe integer without accepting repeated values. */
const positiveInteger = (value: QueryValue): number | undefined => {
  const parsed = Number(singleQueryValue(value));
  return Number.isSafeInteger(parsed) && parsed > 0 ? parsed : undefined;
};

// Comparing fields avoids a route-to-state write when navigation resolves to the
// state already held locally. In particular, this prevents the two watches below
// from repeatedly triggering each other after router.replace().
const queriesEqual = <TRow extends ListRow>(
  left: ListQuery<TRow>,
  right: ListQuery<TRow>
): boolean =>
  left.page === right.page &&
  left.pageSize === right.pageSize &&
  left.filter === right.filter &&
  left.sort?.column === right.sort?.column &&
  left.sort?.direction === right.sort?.direction;

/**
 * Keeps an API-backed AppListView query bookmarkable. Invalid or repeated URL
 * values fall back safely, and values equal to the supplied defaults are omitted.
 *
 * The URL format is:
 * `?page=2&pageSize=25&filter=pump&sort=status&direction=desc`.
 * A deliberately cleared sort is encoded as `sort=none`, because omitting `sort`
 * means "use the configured default sort".
 *
 * Existing query parameters not owned by the list are preserved, allowing the
 * composable to coexist with tabs, feature flags, and other page-level state.
 */
export const useServerListQuery = <TRow extends ListRow>(
  options: ServerListQueryOptions<TRow>
): ServerListQuery<TRow> => {
  const route = useRoute();
  const router = useRouter();
  const { defaults } = options;

  /** Convert untrusted URL values into a complete, API-safe list query. */
  const fromRoute = (routeQuery: LocationQuery): ListQuery<TRow> => {
    const requestedPageSize = positiveInteger(routeQuery.pageSize);
    const pageSize =
      requestedPageSize !== undefined && options.pageSizeOptions.includes(requestedPageSize)
        ? requestedPageSize
        : defaults.pageSize;
    const filter = singleQueryValue(routeQuery.filter) ?? defaults.filter;
    const sortColumn = singleQueryValue(routeQuery.sort);
    const sortDirection = singleQueryValue(routeQuery.direction);

    // `none` is the sole special sort value. It is only valid without a direction;
    // combinations such as `sort=none&direction=asc` fall back to the default.
    const hasExplicitlyClearedSort = sortColumn === 'none' && sortDirection === undefined;

    // A sort is applied only when the column and direction form a valid pair. This
    // prevents arbitrary property names from reaching a server query builder.
    const hasValidSort =
      sortColumn !== undefined &&
      options.sortableColumns.includes(sortColumn as Extract<keyof TRow, string>) &&
      (sortDirection === 'asc' || sortDirection === 'desc');

    return {
      page: positiveInteger(routeQuery.page) ?? defaults.page,
      pageSize,
      filter,
      sort: hasExplicitlyClearedSort
        ? null
        : hasValidSort
          ? ({ column: sortColumn, direction: sortDirection } as ListSort<TRow>)
          : defaults.sort
    };
  };

  // Read synchronously during setup so consumers can issue their first request
  // with bookmarked values instead of briefly requesting the defaults first.
  const query = ref<ListQuery<TRow>>(fromRoute(route.query)) as Ref<ListQuery<TRow>>;

  /** Build the canonical URL representation of the current list state. */
  const toRoute = (value: ListQuery<TRow>): LocationQueryRaw => {
    // Start with the current route to retain parameters owned by the parent page,
    // then remove every list-owned key before selectively adding non-defaults.
    const next: LocationQueryRaw = { ...route.query };
    delete next.page;
    delete next.pageSize;
    delete next.filter;
    delete next.sort;
    delete next.direction;

    if (value.page !== defaults.page) next.page = String(value.page);
    if (value.pageSize !== defaults.pageSize) next.pageSize = String(value.pageSize);
    if (value.filter !== defaults.filter) next.filter = value.filter;

    // Sorting is a pair: serialise both values or neither. A null sort must remain
    // distinguishable from an omitted sort when the configured default is non-null.
    if (
      value.sort?.column !== defaults.sort?.column ||
      value.sort?.direction !== defaults.sort?.direction
    ) {
      if (value.sort) {
        next.sort = value.sort.column;
        next.direction = value.sort.direction;
      } else {
        next.sort = 'none';
      }
    }

    return next;
  };

  const setQuery = (nextQuery: ListQuery<TRow>): void => {
    query.value = nextQuery;
  };

  // Navigation can change the query without interacting with the list (for
  // example browser Back/Forward or a bookmarked link opened in the same view).
  watch(
    () => route.query,
    (routeQuery) => {
      const nextQuery = fromRoute(routeQuery);
      if (!queriesEqual(query.value, nextQuery)) query.value = nextQuery;
    },
    { deep: true }
  );

  // Replace rather than push so typing filters or paging does not create a noisy
  // browser-history entry for every interaction. Running immediately also cleans
  // invalid and default-valued parameters from the initial URL.
  watch(
    query,
    (value) => {
      void router.replace({ query: toRoute(value) });
    },
    { deep: true, immediate: true }
  );

  return { query, setQuery };
};

/**
 * Owns the basic state for a list whose rows and page metadata come from a server.
 *
 * This lower-level composable is useful when the endpoint has one fixed sort field.
 * Use `useServerListQuery` alongside it when the complete list state must also be
 * represented in the route.
 */
export const useServerPagination = (options: ServerPaginationOptions = {}): ServerPagination => {
  // Client state begins with safe display values; server metadata replaces the
  // totals and may correct the requested page after the first response arrives.
  const query = ref(options.initialQuery ?? '');
  const page = ref(Math.max(1, options.initialPage ?? 1));
  const pageSize = ref(options.initialPageSize ?? 10); 
  const sortDirection = ref<SortDirection>(options.initialSortDirection ?? 'ascending');
  const totalItems = ref(0);
  const pageCount = ref(1);

  // Empty result sets use a 0–0 range. Non-empty ranges are one-based for display,
  // while the end is capped for a partially filled final page.
  const rangeStart = computed(() =>
    totalItems.value === 0 ? 0 : (page.value - 1) * pageSize.value + 1
  );
  const rangeEnd = computed(() => Math.min(page.value * pageSize.value, totalItems.value));

  /** Move to a page while respecting the latest bounds reported by the server. */
  const setPage = (nextPage: number): void => {
    page.value = Math.min(Math.max(1, nextPage), pageCount.value);
  };

  /** Toggle the only two directions supported by the server contract. */
  const toggleSortDirection = (): void => {
    sortDirection.value = sortDirection.value === 'ascending' ? 'descending' : 'ascending';
  };

  /** Apply authoritative pagination metadata from a completed server response. */
  const applyPageMetadata = (metadata: PageMetadata): void => {
    totalItems.value = metadata.totalItems;
    pageCount.value = metadata.pageCount;
    page.value = metadata.page;
  };

  // A changed filter, page size, or sort can invalidate the current page. Reset
  // synchronously so request watchers never observe the new criteria with an old
  // page number and accidentally issue an unnecessary out-of-range request.
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
