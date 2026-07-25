import { computed, ref, shallowRef, type Ref, type ShallowRef } from 'vue';
import { defineStore } from 'pinia';
import {
  catalogueApi,
  CatalogueApiError,
  type CatalogueQuery
} from '@/features/catalogues/api/catalogueApi';
import type {
  ControllerTemplateSummary,
  Page,
  PointGroupSummary,
  PointSummary
} from '@/features/catalogues/api/catalogueDto';

const message = (reason: unknown, resource: string): { text: string; unavailable: boolean } => {
  if (reason instanceof CatalogueApiError && reason.status === 404)
    return {
      text: `${resource} are unavailable because this server does not support them yet.`,
      unavailable: true
    };
  return {
    text: reason instanceof Error ? reason.message : `Unable to load ${resource.toLowerCase()}.`,
    unavailable: false
  };
};

const initialPage = <T>(): Page<T> => ({
  items: [],
  totalItems: 0,
  page: 1,
  pageSize: 10,
  pageCount: 0
});

interface PagedStoreSetup<T> {
  result: ShallowRef<Page<T>>;
  loading: Ref<boolean>;
  error: Ref<string>;
  unavailable: Ref<boolean>;
  load: (query: CatalogueQuery) => Promise<void>;
  cancel: () => void;
}

const definePagedStore = <T>(
  id: string,
  resource: string,
  request: (query: CatalogueQuery, signal: AbortSignal) => Promise<Page<T>>
  // Pinia derives the public store-definition type from this generic factory.
  // eslint-disable-next-line @typescript-eslint/explicit-function-return-type
) =>
  defineStore(id, (): PagedStoreSetup<T> => {
    const result = shallowRef<Page<T>>(initialPage());
    const loading = ref(false);
    const error = ref('');
    const unavailable = ref(false);
    let generation = 0;
    let controller: AbortController | undefined;

    const load = async (query: CatalogueQuery): Promise<void> => {
      const current = ++generation;
      controller?.abort();
      controller = new AbortController();
      loading.value = true;
      error.value = '';
      unavailable.value = false;
      try {
        const next = await request(query, controller.signal);
        if (current === generation) result.value = next;
      } catch (reason) {
        if (current !== generation || controller.signal.aborted) return;
        const failure = message(reason, resource);
        error.value = failure.text;
        unavailable.value = failure.unavailable;
      } finally {
        if (current === generation) loading.value = false;
      }
    };

    const cancel = (): void => controller?.abort();
    return { result, loading, error, unavailable, load, cancel };
  });

export const usePointsCatalogueStore = definePagedStore<PointSummary>(
  'pointsCatalogue',
  'Points',
  (query, signal) => catalogueApi.points(query, signal)
);

export const usePointGroupsCatalogueStore = definePagedStore<PointGroupSummary>(
  'pointGroupsCatalogue',
  'Point groups',
  (query, signal) => catalogueApi.groups(query, signal)
);

export const useControllerTemplatesCatalogueStore = defineStore(
  'controllerTemplatesCatalogue',
  () => {
    const allItems = ref<ControllerTemplateSummary[]>([]);
    const loading = ref(false);
    const error = ref('');
    const unavailable = ref(false);
    const filter = ref('');
    const page = ref(1);
    const pageSize = ref(10);
    let generation = 0;
    let controller: AbortController | undefined;

    const filtered = computed(() => {
      const needle = filter.value.trim().toLowerCase();
      return allItems.value
        .filter(
          (item) =>
            !needle ||
            item.name.toLowerCase().includes(needle) ||
            item.id.toLowerCase().includes(needle)
        )
        .sort(
          (left, right) =>
            left.name.localeCompare(right.name, undefined, { sensitivity: 'base' }) ||
            left.id.localeCompare(right.id)
        );
    });
    const result = computed<Page<ControllerTemplateSummary>>(() => {
      const pageCount = Math.ceil(filtered.value.length / pageSize.value);
      const safePage = Math.min(page.value, Math.max(1, pageCount));
      return {
        items: filtered.value.slice((safePage - 1) * pageSize.value, safePage * pageSize.value),
        totalItems: filtered.value.length,
        page: safePage,
        pageSize: pageSize.value,
        pageCount
      };
    });

    const load = async (): Promise<void> => {
      const current = ++generation;
      controller?.abort();
      controller = new AbortController();
      loading.value = true;
      error.value = '';
      unavailable.value = false;
      try {
        const items = await catalogueApi.controllerTemplates(controller.signal);
        if (current === generation) allItems.value = items;
      } catch (reason) {
        if (current !== generation || controller.signal.aborted) return;
        const failure = message(reason, 'Controller templates');
        error.value = failure.text;
        unavailable.value = failure.unavailable;
      } finally {
        if (current === generation) loading.value = false;
      }
    };

    const cancel = (): void => controller?.abort();
    return {
      allItems,
      filter,
      page,
      pageSize,
      result,
      loading,
      error,
      unavailable,
      load,
      cancel
    };
  }
);
