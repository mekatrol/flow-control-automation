<template>
  <div class="pagination">
    <label>
      Items per page
      <select :value="pageSize" @change="changePageSize">
        <option v-for="option in pageSizeOptions" :key="option" :value="option">
          {{ option }}
        </option>
      </select>
    </label>

    <p class="range" aria-live="polite">
      <span class="visually-hidden">Showing </span>{{ firstItem }}–{{ lastItem }} of
      {{ totalItems }}
    </p>

    <nav aria-label="Table pagination">
      <AppButton
        text="Previous page"
        :icon="chevronLeftIcon"
        :disabled="page <= 1"
        @click="goToPage(page - 1)"
      />
      <span aria-current="page">Page {{ page }} of {{ totalItems }}</span>
      <AppButton
        text="Next page"
        :icon="chevronRightIcon"
        :disabled="page >= pageCount"
        @click="goToPage(page + 1)"
      />
    </nav>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue';

import { ListPaginationEmit } from '@/models/listViewEmits';
import AppButton from '@/components/AppButton.vue';

import chevronLeftIcon from '@/assets/icons/chevron-left-icon.svg';
import chevronRightIcon from '@/assets/icons/chevron-right-icon.svg';

interface Props {
  page: number;
  pageCount: number;
  pageSize: number;
  totalItems: number;
  pageSizeOptions?: number[];
  ariaLabel?: string;
}

const props = withDefaults(defineProps<Props>(), {
  pageSizeOptions: () => [10, 25, 50, 100],
  ariaLabel: 'List pagination'
});

type Emits = {
  'page-change': [page: number];
  'page-size-change': [pageSize: number];
};

const emit = defineEmits<Emits>();

const firstItem = computed(() => {
  if (props.totalItems === 0) return 0;
  return (props.page - 1) * props.pageSize + 1;
});

const lastItem = computed(() => Math.min(props.page * props.pageSize, props.totalItems));

const goToPage = (page: number): void => {
  const nextPage = Math.min(Math.max(page, 1), props.pageCount);
  emit(ListPaginationEmit.PageChange, nextPage);
};

const changePageSize = (event: Event): void => {
  const target = event.target as HTMLSelectElement;
  emit(ListPaginationEmit.PageSizeChange, Number(target.value));
};
</script>

<style scoped>
.pagination,
.pagination nav,
.pagination label {
  display: flex;
  gap: var(--space-5-5);
  align-items: center;
}

.pagination {
  justify-content: space-between;
  margin-top: var(--space-8);
  color: var(--color-text-secondary);
  font-size: var(--font-size-xl);
}

.pagination label {
  font-weight: var(--font-weight-semibold);
}

select {
  min-width: 70px;
  min-height: 44px;
  padding: var(--space-3-5);
  color: var(--color-text-primary);
  background: var(--color-surface-raised);
  border: var(--border-width-default) solid var(--color-border-default);
  border-radius: var(--radius-md);
}

.range {
  margin: var(--space-0);
}

.visually-hidden {
  position: absolute;
  width: 1px;
  height: 1px;
  padding: var(--space-0);
  overflow: hidden;
  clip: rect(0, 0, 0, 0);
  white-space: nowrap;
  border: var(--border-width-none);
}

/* Tablet breakpoint (48rem): reflows multi-column controls and workspace panels. */
@media (max-width: 48rem) {
  .pagination {
    align-items: flex-start;
    flex-direction: column;
  }
}
</style>
