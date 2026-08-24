<template>
  <tr v-bind="automation('sort')">
    <th
      v-for="column in columns"
      :key="column.key"
      scope="col"
      :class="`align-${column.align ?? 'start'}`"
      :aria-sort="ariaSort(column)"
      v-bind="automation(`column-${column.automation}`)"
    >
      <slot :name="`column-header-${column.key}`" :column="column" />

      <button
        v-if="column.sortable"
        v-bind="automation(`sort-button-${column.automation}`)"
        type="button"
        class="sort-button"
        :aria-label="sortLabel(column)"
        @click="changeSort(column)"
      >
        <span v-bind="automation(`column-label-${column.automation}`)">
          {{ column.label }}
        </span>

        <span aria-hidden="true">
          {{ sortIndicator(column) }}
        </span>
      </button>

      <span v-else v-bind="automation(`column-label-${column.automation}`)">
        {{ column.label }}
      </span>
    </th>
  </tr>
</template>

<script setup lang="ts" generic="TRow extends ListRow">
import { useAutomation } from '@/composables/useAutomation';

import { ListHeaderRowEmit } from '@/models/listViewEmits';
import type { ListColumn, ListRow, ListSort } from '@/models/listViewModels';

interface Props<TRow extends ListRow> {
  columns: ListColumn<TRow>[];
  sort: ListSort<TRow> | null;
  automation: string;
}

const props = defineProps<Props<TRow>>();

defineOptions({
  inheritAttrs: false
});

type Emits<TRow extends ListRow> = {
  'sort-change': [sort: ListSort<TRow>];
  'sort-clear': [];
};

const emit = defineEmits<Emits<TRow>>();

interface Slots<TRow extends ListRow> {
  [name: `column-header-${string}`]: ((props: { column: ListColumn<TRow> }) => unknown) | undefined;
}

defineSlots<Slots<TRow>>();

const automation = useAutomation(props.automation);

const ariaSort = (
  column: ListColumn<TRow>
): 'none' | 'ascending' | 'descending' | 'other' | undefined => {
  if (!column.sortable) return undefined;

  if (props.sort?.column !== column.key) {
    return 'none';
  }

  return props.sort.direction === 'asc' ? 'ascending' : 'descending';
};

const sortIndicator = (column: ListColumn<TRow>): string => {
  if (props.sort?.column !== column.key) {
    return '↕';
  }

  return props.sort.direction === 'asc' ? '↑' : '↓';
};

const sortLabel = (column: ListColumn<TRow>): string => {
  if (props.sort?.column !== column.key) {
    return `Sort by ${column.label} ascending`;
  }

  if (props.sort.direction === 'asc') {
    return `Sort by ${column.label} descending`;
  }

  return `Sort by ${column.label} ascending`;
};

const changeSort = (column: ListColumn<TRow>): void => {
  const direction =
    props.sort?.column === column.key && props.sort.direction === 'asc' ? 'desc' : 'asc';

  emit(ListHeaderRowEmit.SortChange, {
    column: column.key,
    direction
  });
};
</script>

<style scoped>
th {
  padding: 0.75rem;
  border-block-end: 1px solid var(--color-border-default);
  background: var(--color-surface-disabled);
  text-align: start;
  vertical-align: middle;
}

.align-center {
  text-align: center;
}

.align-end {
  text-align: end;
}

.sort-button {
  display: inline-flex;
  min-height: 2.75rem;
  align-items: center;
  gap: 0.45rem;
  border: 0;
  background: transparent;
  color: inherit;
  font: inherit;
  font-weight: 700;
  cursor: pointer;
}

.sort-button:focus-visible,
.sort-status button:focus-visible {
  outline: 2px solid var(--color-action-primary);
  outline-offset: 2px;
  box-shadow: var(--shadow-focus);
}

.sort-status {
  padding-block: 0.4rem;
  font-weight: 400;
}

.sort-status button {
  min-height: 2.75rem;
  margin-inline-start: 0.5rem;
  border: 0;
  background: transparent;
  color: var(--color-action-primary);
  font: inherit;
  text-decoration: underline;
  cursor: pointer;
}
</style>
