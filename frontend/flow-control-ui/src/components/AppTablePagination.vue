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
        @click="$emit('update:page', page - 1)"
      />
      <span aria-current="page">Page {{ page }} of {{ pageCount }}</span>
      <AppButton
        v-bind="automation('next')"
        text="Next page"
        :icon="chevronRightIcon"
        :disabled="page >= pageCount"
        @click="$emit('update:page', page + 1)"
      />
    </nav>
  </div>
</template>

<script setup lang="ts">
import { useAutomation } from '@/composables/useAutomation';
import chevronLeftIcon from '@/assets/icons/chevron-left-icon.svg';
import chevronRightIcon from '@/assets/icons/chevron-right-icon.svg';
import AppButton from '@/components/AppButton.vue';

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
  'update:page': [page: number];
  'update:pageSize': [pageSize: number];
}>();

const changePageSize = (event: Event): void => {
  emit('update:pageSize', Number((event.target as HTMLSelectElement).value));
};

const automation = useAutomation(props.automation);
</script>

<style scoped>
.pagination,
.pagination nav,
.pagination label {
  display: flex;
  gap: 12px;
  align-items: center;
}

.pagination {
  justify-content: space-between;
  margin-top: 16px;
  color: var(--color-text-secondary);
  font-size: 14px;
}

.pagination label {
  font-weight: 650;
}

select {
  min-width: 70px;
  min-height: 44px;
  padding: 8px;
  color: var(--color-text-primary);
  background: var(--color-surface-raised);
  border: 1px solid var(--color-border-default);
  border-radius: 7px;
}

.range {
  margin: 0;
}

.visually-hidden {
  position: absolute;
  width: 1px;
  height: 1px;
  padding: 0;
  overflow: hidden;
  clip: rect(0, 0, 0, 0);
  white-space: nowrap;
  border: 0;
}

@media (max-width: 720px) {
  .pagination {
    align-items: flex-start;
    flex-direction: column;
  }
}
</style>
