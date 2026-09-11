import { computed, ref } from 'vue';
import { defineStore } from 'pinia';
import { controllerTemplateApi } from '@/features/controllerTemplates/api/controllerTemplateApi';
import type { ControllerTemplateSummary } from '@/features/controllerTemplates/api/controllerTemplateDto';

interface Page<T> {
  items: T[];
  totalItems: number;
  page: number;
  pageSize: number;
  pageCount: number;
}

export const useControllerTemplatesStore = defineStore('controllerTemplates', () => {
  const allItems = ref<ControllerTemplateSummary[]>([]);
  const loading = ref(false);
  const error = ref('');
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
    try {
      const items = await controllerTemplateApi.list(controller.signal);
      if (current === generation) allItems.value = items;
    } catch (reason) {
      if (current !== generation || controller.signal.aborted) return;
      error.value =
        reason instanceof Error ? reason.message : 'Unable to load controller templates.';
    } finally {
      if (current === generation) loading.value = false;
    }
  };

  const cancel = (): void => controller?.abort();
  return { allItems, filter, page, pageSize, result, loading, error, load, cancel };
});
