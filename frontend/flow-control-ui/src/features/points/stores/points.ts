import { ref, shallowRef } from 'vue';
import { defineStore } from 'pinia';
import { PointApiError, pointApi, type PointQuery } from '@/features/points/api/pointApi';
import type { Page, PointSummary } from '@/features/points/api/pointDto';
import { useSpinner } from '@/composables/useSpinner';

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
  const { withSpinner } = useSpinner();

  const load = async (query: PointQuery): Promise<void> => {
    const current = ++generation;
    controller?.abort();
    const requestController = new AbortController();
    controller = requestController;
    try {
      await withSpinner(
        () => {
          error.value = '';
          errorStatus.value = undefined;
        },
        () => pointApi.list(query, requestController.signal),
        (next) => {
          if (current === generation) result.value = next;
        }
      );
    } catch (reason) {
      if (current !== generation || requestController.signal.aborted) return;
      error.value = reason instanceof Error ? reason.message : 'Unable to load points.';
      errorStatus.value = reason instanceof PointApiError ? reason.status : undefined;
    } finally {
      if (controller === requestController) controller = undefined;
    }
  };

  const cancel = (): void => controller?.abort();
  return { result, error, errorStatus, load, cancel };
});
