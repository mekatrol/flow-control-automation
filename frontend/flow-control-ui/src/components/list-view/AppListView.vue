<template>
  <section v-bind="automation()" class="list-view" :aria-labelledby="titleId" :aria-busy="loading">
    <div class="list-view__heading">
      <slot name="header">
        <div v-if="!$slots['header']">
          <h2 :id="titleId">{{ title }}</h2>
          <p v-if="description" :id="descriptionId">{{ description }}</p>
        </div>
      </slot>

      <AppListFilter
        v-bind="automation('filter')"
        :model-value="draftFilter"
        :active="Boolean(query.filter)"
        :input-id="filterId"
        :show-filter-apply="showFilterApply"
        @update:model-value="draftFilter = $event"
        @apply="applyFilter"
        @clear="clearFilter"
      >
        <template v-if="$slots['filter-options']" #filter-options>
          <slot name="filter-options" />
        </template>
      </AppListFilter>
    </div>

    <slot name="top-pagination">
      <AppPagination
        v-if="!$slots['top-pagination']"
        v-bind="automation('top-pagination')"
        :page="query.page"
        :page-count="pageCount"
        :page-size="query.pageSize"
        :total-items="totalItems"
        :page-size-options="pageSizeOptions"
        aria-label="Top list pagination"
        @page-change="changePage"
        @page-size-change="changePageSize"
      />
    </slot>

    <slot name="message">
      <p v-if="!$slots['message']" class="list-view__status" aria-live="polite" aria-atomic="true">
        {{ statusMessage }}
      </p>
    </slot>

    <div class="list-view__table-scroll" tabindex="0" aria-label="Scrollable list results">
      <table :aria-describedby="description ? descriptionId : undefined">
        <caption class="visually-hidden">
          {{
            title
          }}.
          {{
            totalItems
          }}
          total results.
        </caption>

        <colgroup>
          <col
            v-for="column in columns"
            :key="column.key"
            :style="column.width ? { width: column.width } : undefined"
          />
        </colgroup>

        <thead>
          <AppListHeaderRow
            v-bind="automation('header')"
            :columns="columns"
            :sort="query.sort"
            @sort-change="changeSort"
            @sort-clear="clearSort"
          />
        </thead>

        <tbody>
          <tr v-if="loading">
            <td :colspan="columns.length">Loading results…</td>
          </tr>
          <tr v-else-if="rows.length === 0">
            <td :colspan="columns.length">{{ emptyMessage }}</td>
          </tr>
          <template v-else>
            <tr
              v-for="row in rows"
              :key="row.id"
              class="list-view__row"
              v-bind="automation(`row-${row.automation}`)"
              @click="rowClick($event, row)"
            >
              <td
                v-for="column in columns"
                :key="column.key"
                :class="`align-${column.align ?? 'start'}`"
              >
                <slot
                  :name="`cell-${column.key}`"
                  :row="row"
                  :column="column"
                  :value="row[column.key]"
                >
                  <slot name="cell" :row="row" :column="column" :value="row[column.key]">
                    {{ row[column.key] }}
                  </slot>
                </slot>
              </td>
            </tr>
          </template>
        </tbody>

        <tfoot>
          <AppListFooterRow
            v-bind="automation('footer')"
            :column-count="columns.length"
            :total-items="totalItems"
            :show-reset="hasActiveQuery"
            @reset="resetQuery"
          >
            <slot name="footer" :total-items="totalItems"></slot>
          </AppListFooterRow>
        </tfoot>
      </table>
    </div>

    <slot name="bottom-pagination">
      <AppPagination
        v-if="!$slots['bottom-pagination']"
        v-bind="automation('bottom-pagination')"
        :page="query.page"
        :page-count="pageCount"
        :page-size="query.pageSize"
        :total-items="totalItems"
        :page-size-options="pageSizeOptions"
        aria-label="Bottom list pagination"
        @page-change="changePage"
        @page-size-change="changePageSize"
      />
    </slot>
  </section>
</template>

<script
  setup
  lang="ts"
  generic="TRow extends ListRow, TQuery extends ListQuery<TRow> = ListQuery<TRow>"
>
import { computed, ref, watch } from 'vue';
import AppListFilter from '@/components/list-view/AppListFilter.vue';
import AppListFooterRow from '@/components/list-view/AppListFooterRow.vue';
import AppListHeaderRow from '@/components/list-view/AppListHeaderRow.vue';
import AppPagination from '@/components/AppPagination.vue';
import { ListViewEmit } from '@/models/listViewEmits';
import type {
  ListCellContext,
  ListColumn,
  ListQuery,
  ListRow,
  ListSort
} from '@/models/listViewModels';
import { useAutomation } from '@/composables/useAutomation';

interface Props<TRow extends ListRow, TQuery extends ListQuery<TRow>> {
  title: string;
  description?: string;
  rows: TRow[];
  columns: ListColumn<TRow>[];
  query: TQuery;
  totalItems: number;
  loading?: boolean;
  emptyMessage?: string;
  pageSizeOptions?: number[];
  id?: string;
  automation: string;
  showFilterApply?: boolean;
}

const props = withDefaults(defineProps<Props<TRow, TQuery>>(), {
  description: '',
  loading: false,
  emptyMessage: 'No results found.',
  pageSizeOptions: () => [10, 25, 50, 100],
  id: 'list-view',
  showFilterApply: true
});

type Emits<TRow extends ListRow, TQuery extends ListQuery<TRow>> = {
  'query-change': [query: TQuery];
  'filter-clear': [];
  'sort-clear': [];
  'row-click': [row: TRow];
  reset: [];
};

const emit = defineEmits<Emits<TRow, TQuery>>();

interface Slots<TRow extends ListRow> {
  header?: () => unknown;
  'filter-options'?: () => unknown;
  'top-pagination'?: () => unknown;
  'bottom-pagination'?: () => unknown;
  message?: () => unknown;
  cell?: (props: ListCellContext<TRow>) => unknown;
  footer?: (props: { totalItems: number }) => unknown;
  [name: `cell-${string}`]: ((props: ListCellContext<TRow>) => unknown) | undefined;
}

defineSlots<Slots<TRow>>();

const draftFilter = ref(props.query.filter);

const automation = useAutomation(props.automation);

const titleId = computed(() => `${props.id}-title`);
const descriptionId = computed(() => `${props.id}-description`);
const filterId = computed(() => `${props.id}-filter`);
const hasActiveQuery = computed(() => Boolean(props.query.filter || props.query.sort));
const pageCount = computed(() => Math.max(1, Math.ceil(props.totalItems / props.query.pageSize)));

const statusMessage = computed(() => {
  if (props.loading) return 'Loading results.';
  if (props.totalItems === 0) return 'No results found.';
  return `${props.totalItems} results available.`;
});

watch(
  () => props.query.filter,
  (filter) => {
    draftFilter.value = filter;
  }
);

const rowClick = (event: MouseEvent, row: TRow): void => {
  const target = event.target;

  if (
    target instanceof Element &&
    target.closest('a, button, input, select, textarea, [role="button"], [contenteditable]')
  ) {
    return;
  }

  emit('row-click', row);
};

const updateQuery = (changes: Partial<ListQuery<TRow>>): void => {
  emit(ListViewEmit.QueryChange, {
    ...props.query,
    ...changes
  } as TQuery);
};

const applyFilter = (filter: string): void => {
  updateQuery({
    page: 1,
    filter
  });
};

const clearFilter = (): void => {
  draftFilter.value = '';

  updateQuery({
    page: 1,
    filter: ''
  });
  emit(ListViewEmit.FilterClear);
};

const changePage = (page: number): void => {
  updateQuery({ page });
};

const changePageSize = (pageSize: number): void => {
  updateQuery({
    page: 1,
    pageSize
  });
};

const changeSort = (sort: ListSort<TRow>): void => {
  updateQuery({
    page: 1,
    sort
  });
};

const clearSort = (): void => {
  updateQuery({
    page: 1,
    sort: null
  });
  emit(ListViewEmit.SortClear);
};

const resetQuery = (): void => {
  draftFilter.value = '';
  emit(ListViewEmit.QueryChange, {
    ...props.query,
    page: 1,
    filter: '',
    sort: null
  });
  emit(ListViewEmit.Reset);
};
</script>

<style scoped>
.list-view {
  display: grid;
  gap: 1rem;
  width: min(100%, 1400px);
  margin-inline: auto;
  border-radius: 24px;
  background-color: var(--surface-1);
  color: var(--text-1);
}

.list-view__heading {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.list-view__heading h2,
.list-view__heading p,
.list-view__status {
  margin: 0;
}

.list-view__heading p,
.list-view__status {
  color: var(--text-2);
}

.list-view__table-scroll {
  overflow-x: auto;
  border: var(--border);
  border-radius: 16px;
}

.list-view__table-scroll:focus-visible {
  outline: 3px solid var(--accent);
  outline-offset: 3px;
}

table {
  width: 100%;
  min-width: max-content;
  border-collapse: collapse;
  table-layout: fixed;
  background: var(--color-surface-raised);
}

td {
  padding: 0.75rem 1rem;
  border-block-end: var(--border);
  vertical-align: top;
  overflow-wrap: anywhere;
}

.list-view__row {
  cursor: pointer;
}

.list-view__row:hover td {
  background-color: var(--color-surface-neutral);
}

.align-center {
  text-align: center;
}

.align-end {
  text-align: end;
}

.visually-hidden {
  position: absolute;
  width: 1px;
  height: 1px;
  padding: 0;
  margin: -1px;
  overflow: hidden;
  clip: rect(0, 0, 0, 0);
  white-space: nowrap;
  border: 0;
}
</style>
