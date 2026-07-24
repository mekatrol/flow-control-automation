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
      <span class="visually-hidden">Showing </span>{{ rangeStart }}–{{ rangeEnd }} of
      {{ totalItems }}
    </p>

    <nav aria-label="Table pagination">
      <AppButton
        text="Previous page"
        :icon="chevronLeftIcon"
        :disabled="page <= 1"
        @click="$emit('update:page', page - 1)"
      />
      <span aria-current="page">Page {{ page }} of {{ pageCount }}</span>
      <AppButton
        text="Next page"
        :icon="chevronRightIcon"
        :disabled="page >= pageCount"
        @click="$emit('update:page', page + 1)"
      />
    </nav>
  </div>
</template>

<script setup lang="ts">
import chevronLeftIcon from '@/assets/chevron-left-icon.svg';
import chevronRightIcon from '@/assets/chevron-right-icon.svg';
import AppButton from '@/components/AppButton.vue';

withDefaults(
  defineProps<{
    page: number;
    pageCount: number;
    pageSize: number;
    rangeStart: number;
    rangeEnd: number;
    totalItems: number;
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

.pagination :deep(button) {
  min-height: 44px;
  padding: 9px 12px;
  color: var(--color-text-secondary);
  font-weight: 700;
  background: var(--color-surface-raised);
  border: 1px solid var(--color-border-default);
  border-radius: 8px;
}

.pagination :deep(button:disabled) {
  color: var(--color-text-muted);
  cursor: not-allowed;
  background: var(--color-surface-disabled);
  border-style: dashed;
}

.pagination :deep(button:not(:disabled)) {
  cursor: pointer;
}

.pagination :deep(button:not(:disabled):hover) {
  color: var(--color-action-primary-strong);
  background: var(--color-action-primary-surface);
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
