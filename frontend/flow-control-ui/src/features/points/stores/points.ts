import { ref, shallowRef } from 'vue';
import { defineStore } from 'pinia';
import { PointApiError, pointApi, type PointQuery } from '@/features/points/api/pointApi';
import type { Page, PointSummary } from '@/features/points/api/pointDto';
import { useWait } from '@/composables/useWait';

const initialPage = (): Page<PointSummary> => ({
  items: [],
  totalItems: 0,
  page: 1,
  pageSize: 10,
  pageCount: 0
});

export const usePointsStore = defineStore('points', () => {
  const result = shallowRef<Page<PointSummary>>(initialPage());
  const error = ref('');
  const errorStatus = ref<number>();
  let generation = 0;
  let controller: AbortController | undefined;
  const { wait, endWait } = useWait();

  const load = async (query: PointQuery): Promise<void> => {
    const current = ++generation;
    controller?.abort();
    controller = new AbortController();
    error.value = '';
    errorStatus.value = undefined;
    wait();
    try {
      const next = await pointApi.list(query, controller.signal);
      if (current === generation) result.value = next;
    } catch (reason) {
      if (current !== generation || controller.signal.aborted) return;
      error.value = reason instanceof Error ? reason.message : 'Unable to load points.';
      errorStatus.value = reason instanceof PointApiError ? reason.status : undefined;
    } finally {
      if (current === generation) { 
        endWait();
      }
    }
  };

  const cancel = (): void => controller?.abort();
  return { result, error, errorStatus, load, cancel };
});
