<template>
  <div class="pagination" v-bind="automation()">
    <label>
      Items per page
      <select :value="pageSize" @change="changePageSize">
        <option v-for="option in pageSizeOptions" :key="option" :value="option">
          {{ option }}
        </option>
      </select>
    </label>

    <p class="range" aria-live="polite">
      <span class="visually-hidden">Showing </span>{{ rangeStart }}–{{ rangeEnd }} of
      {{ totalItems }}
    </p>

    <nav aria-label="Table pagination">
      <AppButton
        v-bind="automation('prev')"
        text="Previous page"
        :icon="chevronLeftIcon"
        :disabled="page <= 1"
        @click="$emit(EVENTS.UPDATE_PAGE, page - 1)"
      />
      <span aria-current="page">Page {{ page }} of {{ pageCount }}</span>
      <AppButton
        v-bind="automation('next')"
        text="Next page"
        :icon="chevronRightIcon"
        :disabled="page >= pageCount"
        @click="$emit(EVENTS.UPDATE_PAGE, page + 1)"
      />
    </nav>
  </div>
</template>

<script setup lang="ts">
import { useAutomation } from '@/composables/useAutomation';
import chevronLeftIcon from '@/assets/icons/chevron-left-icon.svg';
import chevronRightIcon from '@/assets/icons/chevron-right-icon.svg';
import AppButton from '@/components/AppButton.vue';
import { EVENTS } from '@/constants/events';

const props = withDefaults(
  defineProps<{
    page: number;
    pageCount: number;
    pageSize: number;
    rangeStart: number;
    rangeEnd: number;
    totalItems: number;
    automation: string;
    pageSizeOptions?: readonly number[];
  }>(),
  {
    pageSizeOptions: () => [10, 20, 50]
  }
);

const emit = defineEmits<{
  (event: typeof EVENTS.UPDATE_PAGE, page: number): void;
  (event: typeof EVENTS.UPDATE_PAGE_SIZE, pageSize: number): void;
}>();

const changePageSize = (event: Event): void => {
  emit(EVENTS.UPDATE_PAGE_SIZE, Number((event.target as HTMLSelectElement).value));
};

const automation = useAutomation(props.automation);
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
