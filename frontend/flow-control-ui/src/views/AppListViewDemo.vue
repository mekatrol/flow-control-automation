<template>
  <main class="demo-page">
    <!--
      This introductory block is outside AppListView. It is not required by the
      component; it makes the demo's current controlled query state visible.
    -->
    <div class="demo-intro">
      <p class="demo-intro__eyebrow">Component showcase</p>
      <h1>AppListView demo</h1>
      <p>
        A client-side example with 207 generated records. Try the text filter, sortable headings,
        page-size selector, pagination, row selection, and reset action.
      </p>

      <dl class="query-state" aria-label="Current list query">
        <div>
          <dt>Matching rows</dt>
          <dd>{{ filteredRows.length }} / {{ allRows.length }}</dd>
        </div>
        <div>
          <dt>Page</dt>
          <dd>{{ query.page }}</dd>
        </div>
        <div>
          <dt>Page size</dt>
          <dd>{{ query.pageSize }}</dd>
        </div>
        <div>
          <dt>Sort</dt>
          <dd>{{ sortSummary }}</dd>
        </div>
      </dl>

      <!--
        This demo-only configurator changes the values passed to
        `page-size-options`. Both AppPagination instances update immediately.
      -->
      <fieldset class="page-size-config">
        <legend>Available page sizes</legend>
        <label v-for="option in availablePageSizes" :key="option">
          <input
            type="checkbox"
            :checked="pageSizeOptions.includes(option)"
            :disabled="pageSizeOptions.length === 1 && pageSizeOptions.includes(option)"
            @change="togglePageSizeOption(option)"
          />
          {{ option }}
        </label>
      </fieldset>
    </div>

    <!--
      AppListView is a controlled component: the parent supplies rows and query,
      and handles `query-change` to update them. AppListView renders the controls,
      table, empty/loading states, and pagination, but deliberately does not fetch,
      filter, sort, or paginate data itself.

      Important props:
      - `id` prefixes accessible title, description, and filter element IDs. Use a
        unique value when more than one list appears on a page.
      - `columns` describes table keys, labels, widths, alignment, and sortability.
      - `rows` must contain only the rows for the current page.
      - `query` is the current page/filter/sort state.
      - `total-items` is the filtered total before pagination, not `rows.length`.
      - `page-size-options` configures the pagination size selector.
      - `loading` can be supplied while an API request is in progress.
      - `show-filter-apply="false"` can hide the filter Apply button if desired.

      Events:
      - `query-change` carries the complete next query after every list control.
      - `row-click` carries the selected row.
      - `filter-clear`, `sort-clear`, and `reset` are optional notifications for
        analytics or other side effects; query-change still performs the update.

      Filter customisation:
      - The default filter is a searchable text input with Apply and Clear actions.
      - The `filter-options` slot can replace that input area with application-
        specific controls. Those controls should update the parent query because
        the slot does not receive AppListView's internal draft text binding.
    -->
    <AppListView
      id="list-view-demo"
      title="Generated devices"
      description="A feature-complete AppListView example using local mock data."
      :columns="columns"
      :rows="visibleRows"
      :query="query"
      :total-items="filteredRows.length"
      :page-size-options="pageSizeOptions"
      empty-message="No generated devices match that filter."
      @query-change="updateQuery"
      @filter-clear="lastEvent = 'Filter cleared'"
      @sort-clear="lastEvent = 'Sort cleared'"
      @reset="lastEvent = 'Query reset'"
      @row-click="selectRow"
    >
      <!--
        `header` replaces the default heading. When replacing it, preserve the IDs
        generated from the list's `id` so the section's aria-labelledby and the
        table's aria-describedby relationships continue to work.
      -->
      <template #header>
        <div class="list-heading">
          <div>
            <p class="list-heading__eyebrow">207 mock records</p>
            <h2 id="list-view-demo-title">Generated devices</h2>
            <p id="list-view-demo-description">
              Search names, locations, owners, statuses, or device types.
            </p>
          </div>
          <output class="event-output" aria-live="polite">{{ lastEvent }}</output>
        </div>
      </template>

      <!--
        `column-header-{key}-pre` inserts content before the component's default
        sortable label. Use `column-header-{key}` instead to replace the complete
        header cell contents (including its sort control).
      -->
      <template #column-header-name-pre>
        <span class="featured-column" title="Custom content from the header pre-slot">★</span>
      </template>

      <!--
        `cell-{key}` customises one column. Each cell slot receives `row`, `column`,
        and `value`. Here the full row is used to render two fields in one cell.
      -->
      <template #cell-name="{ row }">
        <strong>{{ row.name }}</strong>
        <small>{{ row.serialNumber }}</small>
      </template>

      <!-- A second typed cell slot presents plain data as a status badge. -->
      <template #cell-status="{ row }">
        <span class="status-pill" :class="`status-pill--${row.status.toLowerCase()}`">
          {{ row.status }}
        </span>
      </template>

      <!-- Native semantic elements can be used inside custom cell slots. -->
      <template #cell-utilisation="{ row }">
        <div class="utilisation">
          <progress :value="row.utilisation" max="100">{{ row.utilisation }}%</progress>
          <span>{{ row.utilisation }}%</span>
        </div>
      </template>

      <!-- Dates remain machine-readable through the time element's datetime. -->
      <template #cell-lastSeen="{ row }">
        <time :datetime="row.lastSeen">{{ formatDate(row.lastSeen) }}</time>
      </template>

      <!--
        The generic `cell` slot is the fallback for columns without a matching
        `cell-{key}` slot. Omitting it uses AppListView's plain-text fallback.
      -->
      <template #cell="{ value }">
        <span>{{ value }}</span>
      </template>

      <!--
        `footer` replaces the result-count text and receives `totalItems`. The
        built-in "Reset filters and sorting" button remains alongside this slot
        whenever a filter or sort is active.

        `top-pagination` and `bottom-pagination` slots are also available when an
        application needs to replace either built-in AppPagination instance.
      -->
      <template #footer="{ totalItems }">
        <span>
          Showing <strong>{{ visibleRange }}</strong> of <strong>{{ totalItems }}</strong> matches
          <template v-if="selectedRow">
            · Selected: <strong>{{ selectedRow.name }}</strong></template
          >
        </span>
      </template>
    </AppListView>
  </main>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue';

import AppListView from '@/components/list-view/AppListView.vue';
import type { ListColumn, ListQuery, ListRow } from '@/models';

// A row may contain any application fields, but must extend ListRow and have an
// `id` (string or number). AppListView uses that ID as the Vue row key.
type DeviceStatus = 'Active' | 'Idle' | 'Offline' | 'Maintenance';

interface DemoRow extends ListRow {
  id: number;
  name: string;
  serialNumber: string;
  type: string;
  location: string;
  owner: string;
  status: DeviceStatus;
  utilisation: number;
  lastSeen: string;
}

const deviceTypes = ['Flow meter', 'Pressure sensor', 'Valve controller', 'Temperature probe'];
const locations = ['Sydney', 'Melbourne', 'Brisbane', 'Perth', 'Adelaide', 'Newcastle'];
const owners = ['Operations', 'Facilities', 'Quality', 'Engineering', 'Production'];
const statuses: DeviceStatus[] = ['Active', 'Idle', 'Offline', 'Maintenance'];

// Mock data is generated once when the module is loaded. Replace this array with
// API results in a real page. The formulas make all 207 rows deterministic while
// still producing enough variation to demonstrate filtering and sorting.
const allRows: DemoRow[] = Array.from({ length: 207 }, (_, index) => {
  const number = index + 1;
  const date = new Date(Date.UTC(2026, 8, 22, 6, 0));
  date.setUTCHours(date.getUTCHours() - index * 5);

  return {
    id: number,
    name: `Device ${String(number).padStart(3, '0')}`,
    serialNumber: `FC-${2026 + (index % 3)}-${String(1000 + number)}`,
    type: deviceTypes[index % deviceTypes.length]!,
    location: locations[(index * 5) % locations.length]!,
    owner: owners[(index * 3) % owners.length]!,
    status: statuses[(index * 7) % statuses.length]!,
    utilisation: (index * 17 + 23) % 101,
    lastSeen: date.toISOString()
  };
});

// Column `key` values are type-checked against DemoRow. Set `sortable` only when
// the parent can sort that field. Widths populate the table colgroup, and `align`
// controls the matching header and body cell alignment.
const columns: ListColumn<DemoRow>[] = [
  { key: 'name', label: 'Device', sortable: true, width: '15rem' },
  { key: 'type', label: 'Type', sortable: true, width: '12rem' },
  { key: 'location', label: 'Location', sortable: true, width: '10rem' },
  { key: 'owner', label: 'Owner', sortable: true, width: '10rem' },
  { key: 'status', label: 'Status', sortable: true, width: '10rem', align: 'center' },
  { key: 'utilisation', label: 'Utilisation', sortable: true, width: '12rem' },
  { key: 'lastSeen', label: 'Last seen', sortable: true, width: '13rem' }
];

// The parent owns the complete query. Page numbers are one-based. A null `sort`
// displays the source order; this demo starts sorted by name ascending.
const query = ref<ListQuery<DemoRow>>({
  page: 1,
  pageSize: 25,
  filter: '',
  sort: { column: 'name', direction: 'asc' }
});

// The available values can come from application configuration. This demo lets
// the user change the prop interactively; keep at least one value enabled.
const availablePageSizes = [2, 5, 10, 20];
const pageSizeOptions = ref<number[]>([2, 5, 10, 20]);
const selectedRow = ref<DemoRow | null>(null);
const lastEvent = ref('Ready — select a row or change the query');

// FILTERING: AppListView emits the submitted text in `query-change` and resets
// the page to 1. The parent decides which fields participate in matching. For
// server-side lists, send query.value.filter to the API instead of filtering here.
const filteredRows = computed(() => {
  const filter = query.value.filter.trim().toLocaleLowerCase();
  if (!filter) return allRows;

  return allRows.filter((row) =>
    [row.name, row.serialNumber, row.type, row.location, row.owner, row.status].some((value) =>
      value.toLocaleLowerCase().includes(filter)
    )
  );
});

// SORTING: clicking a sortable heading emits its key and direction and resets the
// page to 1. Copy before sorting so the filtered array is never mutated. A real
// server-backed page would translate these values into its API's sort parameters.
const sortedRows = computed(() => {
  const sort = query.value.sort;
  if (!sort) return filteredRows.value;

  const direction = sort.direction === 'asc' ? 1 : -1;
  return [...filteredRows.value].sort((left, right) => {
    const leftValue = left[sort.column];
    const rightValue = right[sort.column];

    if (typeof leftValue === 'number' && typeof rightValue === 'number') {
      return (leftValue - rightValue) * direction;
    }

    return (
      String(leftValue).localeCompare(String(rightValue), undefined, {
        numeric: true,
        sensitivity: 'base'
      }) * direction
    );
  });
});

// PAGINATION: AppListView needs only the current page's rows. The unpaginated
// filtered count is passed separately as `total-items`, allowing it to calculate
// page count and render both pagination controls correctly.
const visibleRows = computed(() => {
  const start = (query.value.page - 1) * query.value.pageSize;
  return sortedRows.value.slice(start, start + query.value.pageSize);
});

// These two values are demo-only display helpers; AppListView does not require
// either of them.
const sortSummary = computed(() => {
  if (!query.value.sort) return 'None';
  const column = columns.find(({ key }) => key === query.value.sort?.column);
  return `${column?.label ?? query.value.sort.column} ${query.value.sort.direction === 'asc' ? '↑' : '↓'}`;
});

const visibleRange = computed(() => {
  if (filteredRows.value.length === 0) return '0';
  const first = (query.value.page - 1) * query.value.pageSize + 1;
  const last = first + visibleRows.value.length - 1;
  return `${first}–${last}`;
});

// QUERY EVENTS: every filter, sorting, page, and page-size interaction produces
// one complete query object. Assigning it here causes the computed data pipeline
// and AppListView props to update reactively.
const updateQuery = (nextQuery: ListQuery<DemoRow>): void => {
  query.value = nextQuery;
  selectedRow.value = null;
  lastEvent.value = `Query changed: page ${nextQuery.page}, ${nextQuery.pageSize} rows per page`;
};

// If the current page size is removed, select the first remaining option and
// return to page 1 so the query always agrees with the pagination control.
const togglePageSizeOption = (option: number): void => {
  const isEnabled = pageSizeOptions.value.includes(option);
  const nextOptions = isEnabled
    ? pageSizeOptions.value.filter((pageSize) => pageSize !== option)
    : [...pageSizeOptions.value, option].sort((left, right) => left - right);

  if (nextOptions.length === 0) return;

  pageSizeOptions.value = nextOptions;

  if (!nextOptions.includes(query.value.pageSize)) {
    query.value = { ...query.value, page: 1, pageSize: nextOptions[0]! };
  }

  lastEvent.value = `Page-size options changed: ${nextOptions.join(', ')}`;
};

// ROW EVENTS: row-click fires for the row background/cells, but AppListView avoids
// firing it when the original click came from an interactive child such as a link,
// button, input, or select.
const selectRow = (row: DemoRow): void => {
  selectedRow.value = row;
  lastEvent.value = `Row clicked: ${row.name}`;
};

const formatDate = (value: string): string =>
  new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'short' }).format(
    new Date(value)
  );
</script>

<style scoped>
/* Page-level presentation is intentionally separate from AppListView's own styles. */
.demo-page {
  display: grid;
  gap: var(--space-6, 1.5rem);
  padding: var(--space-6, 1.5rem);
}

.demo-intro,
.demo-page :deep(.list-view) {
  width: min(100%, 1400px);
  margin-inline: auto;
}

.demo-intro h1,
.demo-intro p,
.list-heading h2,
.list-heading p {
  margin: 0;
}

.demo-intro {
  display: grid;
  gap: 0.75rem;
}

.demo-intro > p:not(.demo-intro__eyebrow) {
  max-width: 72ch;
  color: var(--color-text-secondary);
}

.demo-intro__eyebrow,
.list-heading__eyebrow {
  color: var(--color-action-primary);
  font-size: var(--font-size-xs, 0.8rem);
  font-weight: var(--font-weight-black, 800);
  letter-spacing: 0.08em;
  text-transform: uppercase;
}

.query-state {
  display: grid;
  grid-template-columns: repeat(4, minmax(8rem, 1fr));
  gap: 0.5rem;
  margin: 0.5rem 0 0;
}

.query-state div {
  padding: 0.75rem 1rem;
  border: var(--border);
  border-radius: var(--radius-md, 0.5rem);
  background: var(--color-surface-raised);
}

.query-state dt {
  color: var(--color-text-secondary);
  font-size: var(--font-size-xs, 0.8rem);
}

.query-state dd {
  margin: 0.25rem 0 0;
  font-weight: 700;
}

.page-size-config {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 0.75rem 1rem;
  padding: 0.75rem 1rem;
  border: var(--border);
  border-radius: var(--radius-md, 0.5rem);
}

.page-size-config legend {
  padding-inline: 0.25rem;
  font-weight: 700;
}

.page-size-config label {
  display: inline-flex;
  align-items: center;
  gap: 0.35rem;
  cursor: pointer;
}

.page-size-config label:has(input:disabled) {
  cursor: not-allowed;
  opacity: 0.65;
}

.list-heading {
  display: flex;
  flex-wrap: wrap;
  align-items: end;
  justify-content: space-between;
  gap: 1rem;
}

.list-heading > div {
  display: grid;
  gap: 0.3rem;
}

.event-output {
  color: var(--color-text-secondary);
  font-size: 0.9rem;
}

.featured-column {
  margin-inline-end: 0.35rem;
  color: var(--color-action-primary);
}

/* Custom cell slots own their visual presentation, so their styles live here. */
small {
  display: block;
  margin-top: 0.2rem;
  color: var(--color-text-secondary);
}

.status-pill {
  display: inline-block;
  padding: 0.3rem 0.55rem;
  border-radius: var(--radius-pill, 999px);
  background: var(--color-surface-neutral);
  font-size: var(--font-size-xs, 0.8rem);
  font-weight: 700;
}

.status-pill--active {
  color: var(--color-action-primary-strong);
  background: var(--color-action-primary-surface);
}

.status-pill--offline,
.status-pill--maintenance {
  color: var(--color-text-secondary);
}

.utilisation {
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.utilisation progress {
  width: 6rem;
  accent-color: var(--color-action-primary);
}

@media (max-width: 700px) {
  /* The table itself remains horizontally scrollable on narrow viewports. */
  .demo-page {
    padding: 1rem;
  }

  .query-state {
    grid-template-columns: repeat(2, 1fr);
  }
}
</style>
