import { ref } from 'vue';
import { defineStore } from 'pinia';

import {
  listViewDemoApi,
  type DemoDeviceListQuery,
  type DemoDevicePage,
  type DemoDeviceRow
} from '@/features/listViewDemo/api/listViewDemoApi';

/** The empty response lets the view render safely before its first request finishes. */
const emptyPage = (): DemoDevicePage => ({
  items: [],
  totalAvailableItems: 0,
  totalItems: 0,
  page: 1,
  pageSize: 25,
  pageCount: 1
});

/**
 * Store responsibilities:
 * - own the current query and last server response;
 * - call the API whenever list controls change the query;
 * - expose loading/error state to the view;
 * - cancel obsolete requests and ignore stale responses.
 *
 * The store does not filter, sort, or paginate. Those are API/backend concerns.
 */
export const useListViewDemoStore = defineStore('list-view-demo', () => {
  /** This is the single query passed to both AppListView and the API. */
  const query = ref<DemoDeviceListQuery>({
    page: 1,
    pageSize: 25,
    filter: '',
    sort: { column: 'name', direction: 'asc' }
  });

  /** Only the current server page is retained; the store never receives all rows. */
  const result = ref<DemoDevicePage>(emptyPage());
  const loading = ref(false);
  const error = ref('');

  let requestController: AbortController | undefined;
  let requestGeneration = 0;

  /**
   * Load one page using the current query. Each request gets an AbortController
   * and generation number. Aborting saves unnecessary work; the generation check
   * is a second guard against transports that cannot truly cancel a response.
   */
  const load = async (): Promise<void> => {
    requestController?.abort();

    const controller = new AbortController();
    const generation = ++requestGeneration;
    requestController = controller;
    loading.value = true;
    error.value = '';

    try {
      const response = await listViewDemoApi.list({ ...query.value }, controller.signal);
      if (generation !== requestGeneration) return;

      result.value = response;

      // The API may clamp an out-of-range page after records are removed or a
      // filter changes. Reflect its authoritative page metadata in UI state.
      query.value = {
        ...query.value,
        page: response.page,
        pageSize: response.pageSize
      };
    } catch (reason) {
      if (generation !== requestGeneration || controller.signal.aborted) return;
      error.value = reason instanceof Error ? reason.message : 'Unable to load demo devices.';
    } finally {
      if (generation === requestGeneration) {
        loading.value = false;
        requestController = undefined;
      }
    }
  };

  /**
   * AppListView emits a complete query, so the store can replace its query rather
   * than merging individual fields. Returning the Promise also makes the action
   * straightforward to await in tests or other consumers.
   */
  const updateQuery = async (nextQuery: DemoDeviceListQuery): Promise<void> => {
    query.value = nextQuery;
    await load();
  };

  /** Cancel in-flight work when the route is left or the consumer is destroyed. */
  const cancel = (): void => {
    requestController?.abort();
    requestController = undefined;
  };

  return { query, result, loading, error, load, updateQuery, cancel };
});

export type { DemoDeviceListQuery, DemoDeviceRow };
