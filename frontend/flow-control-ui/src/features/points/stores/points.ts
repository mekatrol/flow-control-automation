import { ref, shallowRef } from 'vue';
import { defineStore } from 'pinia';
import { PointApiError, pointApi, type PointQuery } from '@/features/points/api/pointApi';
import type { Page, PointSummary } from '@/features/points/api/pointDto';

const initialPage = (): Page<PointSummary> => ({
  items: [],
  totalItems: 0,
  page: 1,
  pageSize: 10,
  pageCount: 0
});

export const usePointsStore = defineStore('points', () => {
  const result = shallowRef<Page<PointSummary>>(initialPage());
  const loading = ref(false);
  const error = ref('');
  const errorStatus = ref<number>();
  let generation = 0;
  let controller: AbortController | undefined;

  const load = async (query: PointQuery): Promise<void> => {
    const current = ++generation;
    controller?.abort();
    controller = new AbortController();
    loading.value = true;
    error.value = '';
    errorStatus.value = undefined;
    try {
      const next = await pointApi.list(query, controller.signal);
      if (current === generation) result.value = next;
    } catch (reason) {
      if (current !== generation || controller.signal.aborted) return;
      error.value = reason instanceof Error ? reason.message : 'Unable to load points.';
      errorStatus.value = reason instanceof PointApiError ? reason.status : undefined;
    } finally {
      if (current === generation) loading.value = false;
    }
  };

  const cancel = (): void => controller?.abort();
  return { result, loading, error, errorStatus, load, cancel };
});
