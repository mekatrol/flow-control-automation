<template>
  <nav v-bind="automation()" class="pagination" :aria-label="ariaLabel">
    <p v-bind="automation('page-summary')" class="pagination__summary" aria-live="polite">
      {{ summary }}
    </p>

    <div class="pagination__controls">
      <label v-bind="automation('page-size-label')" :for="pageSizeId">Items per page</label>
      <select
        v-bind="automation('page-size-select')"
        :id="pageSizeId"
        :value="pageSize"
        @change="changePageSize"
      >
        <option
          v-for="size in pageSizeOptions"
          :key="size"
          :value="size"
          v-bind="automation(`page-size-option-${size}`)"
        >
          {{ size }}
        </option>
      </select>

      <button
        v-bind="automation('goto-page-prev')"
        type="button"
        :disabled="page <= 1"
        @click="goToPage(page - 1)"
      >
        Previous
      </button>
      <span v-bind="automation('page-info')">Page {{ page }} of {{ totalPages }}</span>
      <button
        v-bind="automation('goto-page-next')"
        type="button"
        :disabled="page >= totalPages"
        @click="goToPage(page + 1)"
      >
        Next
      </button>
    </div>
  </nav>
</template>

<script setup lang="ts">
import { computed } from 'vue';
import { useAutomation } from '@/composables/useAutomation';

import { ListPaginationEmit } from '@/models/listViewEmits';

interface Props {
  page: number;
  pageSize: number;
  totalItems: number;
  pageSizeOptions?: number[];
  ariaLabel?: string;
  pageSizeId?: string;
  automation: string;
}

const props = withDefaults(defineProps<Props>(), {
  pageSizeOptions: () => [10, 25, 50, 100],
  ariaLabel: 'List pagination',
  pageSizeId: 'list-page-size'
});

type Emits = {
  'page-change': [page: number];
  'page-size-change': [pageSize: number];
};

const emit = defineEmits<Emits>();

const automation = useAutomation(props.automation);

const totalPages = computed(() => Math.max(1, Math.ceil(props.totalItems / props.pageSize)));

const firstItem = computed(() => {
  if (props.totalItems === 0) return 0;
  return (props.page - 1) * props.pageSize + 1;
});

const lastItem = computed(() => Math.min(props.page * props.pageSize, props.totalItems));

const summary = computed(
  () => `${firstItem.value}–${lastItem.value} of ${props.totalItems} results`
);

const goToPage = (page: number): void => {
  const nextPage = Math.min(Math.max(page, 1), totalPages.value);
  emit(ListPaginationEmit.PageChange, nextPage);
};

const changePageSize = (event: Event): void => {
  const target = event.target as HTMLSelectElement;
  emit(ListPaginationEmit.PageSizeChange, Number(target.value));
};
</script>

<style scoped>
.pagination {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  justify-content: space-between;
  gap: 0.75rem 1rem;
}

.pagination__summary {
  margin: 0;
  color: var(--color-text-muted);
}

.pagination__controls {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 0.5rem;
}

select,
button {
  min-height: 2.75rem;
  border: 1px solid var(--color-border-default);
  border-radius: 0.35rem;
  background: var(--color-surface-subtle);
  color: var(--color-text-primary);
  font: inherit;
}

select {
  padding-inline: 0.5rem;
}

button {
  padding-inline: 0.9rem;
  cursor: pointer;
}

button:hover:not(:disabled) {
  border-color: var(--color-border-default);
}

button:disabled {
  cursor: not-allowed;
  opacity: 0.55;
}

select:focus-visible,
button:focus-visible {
  outline: 2px solid var(--color-action-primary);
  outline-offset: 2px;
  box-shadow: var(--shadow-focus);
}
</style>
