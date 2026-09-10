# List view components

The list-view suite provides a typed, accessible table for displaying a server-side or
client-side collection. It combines filtering, sortable column headers, row and cell
customisation, result totals, loading and empty states, and matching pagination controls
above and below the table.

The main component is `AppListView`. In most features it is the only component that
should be imported directly; it composes `AppListFilter`, `AppListHeaderRow`,
`AppListFooterRow`, and the shared `AppPagination` component.

## Feature summary

- Strongly typed rows, columns, query state, slot values, and emitted payloads.
- Controlled pagination for locally or remotely loaded data.
- Page-size selection and previous/next navigation above and below the results.
- Sortable headers with ascending/descending toggling and `aria-sort` state.
- A built-in text filter, with either explicit Apply behaviour or live filtering.
- Complete replacement slots for the heading, filter controls, pagination areas,
  individual headers, individual cells, all default cells, and footer content.
- Per-column widths and start, centre, or end alignment.
- Loading and empty states that span the entire table.
- A clickable-row event that does not interfere with links, buttons, or form controls
  placed inside cells.
- A footer reset action whenever filtering or sorting is active.
- A horizontally scrollable, keyboard-focusable table container for narrow viewports.

`AppListView` does not fetch, filter, sort, or paginate data itself. The consumer owns
the query and rows. User actions emit a new query; the parent applies that query and
passes the resulting state and page of rows back into the component.

## Files and responsibilities

| Component | Location | Responsibility |
| --- | --- | --- |
| `AppListView` | `frontend/flow-control-ui/src/components/list-view/AppListView.vue` | Public composition component and table rendering |
| `AppListFilter` | `frontend/flow-control-ui/src/components/list-view/AppListFilter.vue` | Search form and filter input slot |
| `AppListHeaderRow` | `frontend/flow-control-ui/src/components/list-view/AppListHeaderRow.vue` | Column headings and sort buttons |
| `AppListFooterRow` | `frontend/flow-control-ui/src/components/list-view/AppListFooterRow.vue` | Result total and query reset action |
| `AppPagination` | `frontend/flow-control-ui/src/components/AppPagination.vue` | Page size, result range, and previous/next controls |
| List types | `frontend/flow-control-ui/src/models/listViewModels.ts` | Shared row, column, query, sort, and slot types |

## Core data model

Import the public types from `@/models`:

```ts
import type { ListColumn, ListQuery, ListRow } from '@/models';
```

Every row must have a unique string or number `id`. Column keys are restricted to
string keys of the row type.

```ts
interface ListRow {
  id: string | number;
}

interface ListColumn<TRow extends ListRow> {
  key: Extract<keyof TRow, string>;
  label: string;
  sortable?: boolean;
  width?: string;
  align?: 'start' | 'center' | 'end';
}

interface ListSort<TRow extends ListRow> {
  column: Extract<keyof TRow, string>;
  direction: 'asc' | 'desc';
}

interface ListQuery<TRow extends ListRow> {
  page: number;
  pageSize: number;
  filter: string;
  sort: ListSort<TRow> | null;
}
```

Pages are one-based. The component expects `rows` to contain only the rows rendered on
the current page, while `totalItems` is the total matching result count across all
pages. For an unpaginated local collection, slice the processed collection before
passing it as `rows`.

The query type may be extended with feature-specific state. `AppListView` preserves
additional fields when it emits a changed query:

```ts
interface ProductRow extends ListRow {
  id: string;
  name: string;
  category: string;
  price: number;
  actions: null;
}

interface ProductQuery extends ListQuery<ProductRow> {
  categories: string[];
  includeArchived: boolean;
}
```

## Minimal controlled example

```vue
<template>
  <AppListView
    id="product-list"
    title="Products"
    description="Products available to the current account."
    :columns="columns"
    :rows="rows"
    :query="query"
    :total-items="totalItems"
    :loading="loading"
    @query-change="handleQueryChange"
    @row-click="openProduct"
  />
</template>

<script setup lang="ts">
import { onMounted, ref } from 'vue';

import AppListView from '@/components/list-view/AppListView.vue';
import type { ListColumn, ListQuery, ListRow } from '@/models';

interface ProductRow extends ListRow {
  id: string;
  name: string;
  category: string;
  price: number;
}

const columns: ListColumn<ProductRow>[] = [
  { key: 'name', label: 'Name', sortable: true },
  { key: 'category', label: 'Category', width: '14rem' },
  { key: 'price', label: 'Price', sortable: true, width: '9rem', align: 'end' }
];

const query = ref<ListQuery<ProductRow>>({
  page: 1,
  pageSize: 25,
  filter: '',
  sort: { column: 'name', direction: 'asc' }
});
const rows = ref<ProductRow[]>([]);
const totalItems = ref(0);
const loading = ref(false);

const loadProducts = async (): Promise<void> => {
  loading.value = true;
  try {
    const result = await productApi.list(query.value);
    rows.value = result.items;
    totalItems.value = result.totalItems;
  } finally {
    loading.value = false;
  }
};

const handleQueryChange = async (nextQuery: ListQuery<ProductRow>): Promise<void> => {
  query.value = nextQuery;
  await loadProducts();
};

const openProduct = (row: ProductRow): void => {
  // Navigate to or select the row.
};

onMounted(loadProducts);
</script>
```

The parent must replace its query with the emitted value. If it does not, the controls
appear to do nothing because the component never mutates the `query` prop.

## `AppListView` API

### Props

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `title` | `string` | required | Default visible heading and table caption text |
| `description` | `string` | `''` | Optional text below the default heading; also referenced by the table with `aria-describedby` |
| `rows` | `TRow[]` | required | Rows for the current page; every row requires a unique `id` |
| `columns` | `ListColumn<TRow>[]` | required | Ordered column definitions |
| `query` | `TQuery` | required | Current controlled page, page size, filter, and sort state |
| `totalItems` | `number` | required | Total matching items across all pages, not merely `rows.length` |
| `loading` | `boolean` | `false` | Sets `aria-busy` and replaces body rows with “Loading results…” |
| `emptyMessage` | `string` | `'No results found.'` | Body message when loading is false and `rows` is empty |
| `pageSizeOptions` | `number[]` | `[10, 25, 50, 100]` | Options shown in both default page-size selectors |
| `id` | `string` | `'list-view'` | Base for generated title, description, and filter IDs |
| `showFilterApply` | `boolean` | `true` | Shows or hides the built-in filter Apply button |

Use a unique `id` when more than one list appears on a page. The generated IDs are
`${id}-title`, `${id}-description`, and `${id}-filter`.

### Events

| Event | Payload | When emitted |
| --- | --- | --- |
| `query-change` | `TQuery` | Apply/clear filter, page change, page-size change, sort change/clear, or reset |
| `filter-clear` | none | After the built-in filter clear action resets filter text and page |
| `sort-clear` | none | After a sort-clear action |
| `row-click` | `TRow` | A table row is clicked outside an interactive descendant |
| `reset` | none | After the footer “Reset filters and sorting” button is used |

For filter clear, sort clear, and reset, `query-change` is emitted as well as the
specific notification event. Use `query-change` to update data; use the notification
events only for extra effects such as analytics.

Query changes follow these rules:

| User action | Changed fields |
| --- | --- |
| Apply filter | `page: 1`, `filter: trimmed draft value` |
| Clear filter | `page: 1`, `filter: ''` |
| Change page | `page` |
| Change page size | `page: 1`, `pageSize` |
| Change sort | `page: 1`, `sort` |
| Clear sort | `page: 1`, `sort: null` |
| Reset | `page: 1`, `filter: ''`, `sort: null` |

All other fields, including fields on an extended query type, are copied unchanged.

### Slots

| Slot | Scope | Behaviour |
| --- | --- | --- |
| `header` | none | Replaces the default title and description block |
| `filter-options` | none | Replaces the default text input inside the filter form |
| `top-pagination` | none | Replaces the entire top `AppPagination`; an empty slot hides it |
| `bottom-pagination` | none | Replaces the entire bottom `AppPagination`; an empty slot hides it |
| `cell` | `{ row, column, value }` | Fallback renderer for every cell without a column-specific slot |
| `cell-<key>` | `{ row, column, value }` | Renders cells for one column and takes priority over `cell` |
| `column-header-<key>` | `{ column }` | Replaces the complete header contents, including built-in sort control |
| `column-header-<key>-pre` | `{ column }` | Inserts content before the built-in label or sort button |
| `footer` | `{ totalItems }` | Replaces the default result-total text; reset remains available |

Slot names are generated from column keys. A column with key `updatedAt` uses
`#cell-updatedAt`, `#column-header-updatedAt`, and
`#column-header-updatedAt-pre`.

The component's TypeScript slot declaration includes a `message` slot, but the current
template does not render it. Do not depend on `#message`.

## Columns, widths, and alignment

Columns are rendered in array order. Each definition creates a `<col>`, `<th>`, and a
cell for every row.

```ts
const columns: ListColumn<ProductRow>[] = [
  // Flexible: takes the remaining table width.
  { key: 'name', label: 'Name', sortable: true },

  // Fixed CSS width and centred contents.
  { key: 'category', label: 'Category', width: '12rem', align: 'center' },

  // Right/end aligned numeric content.
  { key: 'price', label: 'Price', width: '8rem', align: 'end' }
];
```

`width` accepts any valid CSS width string, such as `'12rem'`, `'160px'`, or `'25%'`.
The table uses fixed layout and `min-width: max-content`; on a narrow viewport its
focusable wrapper scrolls horizontally. Prefer `rem` values consistent with the
project design system and leave at least one content column without a fixed width.

`align` defaults to `start` and is applied to both headings and cells. `start` and
`end` respect the document writing direction.

## Sorting

Set `sortable: true` on a column to render an accessible sort button. Clicking it:

1. selects ascending order when the column is not currently sorted;
2. switches from ascending to descending on the same column; and
3. switches to ascending when a different sortable column is selected.

The interaction does not cycle from descending to no sort. Clearing sort is currently
available through the footer reset action, which also clears the filter. The component
declares `sort-clear`, but its built-in header renders no independent clear-sort
control.

```vue
<AppListView
  :columns="columns"
  :rows="rows"
  :query="query"
  :total-items="totalItems"
  title="Products"
  @query-change="query = $event"
  @sort-clear="recordSortCleared"
/>
```

The component reports sort intent only. Apply the sort in the API request or to the
local collection before setting `rows`. The active sortable header receives
`aria-sort="ascending"` or `aria-sort="descending"`; inactive sortable headers receive
`aria-sort="none"`.

### Adding content without replacing sorting

Use the `-pre` slot to add an icon or action while retaining the label, sort indicator,
keyboard interaction, and accessible sort name:

```vue
<template #column-header-name-pre>
  <AppButton
    text="Add product"
    :icon="addIcon"
    hide-text
    aria-label="Add a product"
    @click="addProduct"
  />
</template>
```

Use `#column-header-name` only when the entire built-in header should be replaced. If
the column remains marked sortable, a replacement slot must implement its own sort
interaction; the built-in button is not rendered inside that slot.

## Filtering

By default, `AppListFilter` renders a labelled search input and Apply button. Typing
updates only an internal draft. Submitting the form trims the value and emits a query
with page reset to 1. The draft is synchronised whenever `query.filter` changes.

```vue
<AppListView
  title="Products"
  :columns="columns"
  :rows="rows"
  :query="query"
  :total-items="totalItems"
  @query-change="applyQuery"
  @filter-clear="announceFilterCleared"
/>
```

Clearing through the built-in input emits `query-change` with an empty filter, followed
by `filter-clear`.

### Live filtering without an Apply button

`show-filter-apply="false"` hides Apply. It does not automatically emit draft text from
the default input as a query, so use a custom `filter-options` slot whose state is owned
by the parent, then debounce or immediately update the parent query.

```vue
<template>
  <AppListView
    title="Products"
    :show-filter-apply="false"
    :columns="columns"
    :rows="rows"
    :query="query"
    :total-items="totalItems"
    @query-change="applyQuery"
  >
    <template #filter-options>
      <AppInputActions
        v-model="filterText"
        type="search"
        placeholder="Enter a product name"
        autocomplete="off"
        @clear="setFilter('')"
      />
    </template>
  </AppListView>
</template>

<script setup lang="ts">
import { computed } from 'vue';

const filterText = computed({
  get: () => query.value.filter,
  set: (value: string) => setFilter(value)
});

const setFilter = (filter: string): void => {
  applyQuery({ ...query.value, page: 1, filter });
};
</script>
```

The custom controls remain inside the component's `<form role="search">`. Pressing
Enter submits that form, but the emitted built-in filter value remains the list's
internal draft. With fully custom/live controls, handle their state directly as above.

### Multiple filter controls

Extend the query for structured filters and keep those fields in the parent:

```vue
<template #filter-options>
  <div class="filter-options">
    <AppMultiSelectDropdown
      v-model="selectedCategories"
      label="Categories"
      all-label="All"
      :options="categoryOptions"
    />
    <AppInputActions
      v-model="filterText"
      type="search"
      placeholder="Enter a product name"
    />
  </div>
</template>
```

When updating a custom query field, spread the current query and normally reset
`page` to 1:

```ts
const setCategories = (categories: string[]): void => {
  applyQuery({ ...query.value, page: 1, categories });
};
```

## Pagination and navigation

The default top and bottom pagination controls receive the same values and emit the
same query changes. Each contains:

- an “Items per page” selector;
- the visible item range, such as `26–50 of 243`;
- previous and next buttons; and
- a current-page status.

`pageCount` is calculated as `max(1, ceil(totalItems / query.pageSize))`. Previous is
disabled on page 1 and Next is disabled on the last page. Page changes are clamped to
the range from 1 through `pageCount`. Changing page size returns to page 1.

```vue
<AppListView
  title="Audit events"
  :columns="columns"
  :rows="rows"
  :query="query"
  :total-items="totalItems"
  :page-size-options="[10, 20, 50]"
  @query-change="applyQuery"
/>
```

Keep the query valid when a data refresh reduces `totalItems`. `AppListView` does not
automatically move an already-selected out-of-range page back into range.

### Hide top or bottom pagination

Provide an empty replacement slot. To hide only the bottom controls:

```vue
<AppListView v-bind="listProps" @query-change="applyQuery">
  <template #bottom-pagination />
</AppListView>
```

To hide the top controls instead:

```vue
<AppListView v-bind="listProps" @query-change="applyQuery">
  <template #top-pagination />
</AppListView>
```

To hide both, provide both empty slots. Pagination is not inferred from whether
`totalItems` fits on one page; default controls render even for one page.

### Replace pagination or page navigation

The pagination slots replace their entire region, allowing numbered pages, compact
navigation, or a feature-specific toolbar. They expose no slot props, so bind to the
same parent-owned query and update it explicitly:

```vue
<AppListView
  title="Products"
  :columns="columns"
  :rows="rows"
  :query="query"
  :total-items="totalItems"
  @query-change="applyQuery"
>
  <template #top-pagination>
    <MyNumberedPagination
      :page="query.page"
      :page-size="query.pageSize"
      :total-items="totalItems"
      aria-label="Product pages"
      @page-change="setPage"
      @page-size-change="setPageSize"
    />
  </template>

  <template #bottom-pagination />
</AppListView>
```

```ts
const setPage = (page: number): void => {
  applyQuery({ ...query.value, page });
};

const setPageSize = (pageSize: number): void => {
  applyQuery({ ...query.value, page: 1, pageSize });
};
```

Current implementation note: `AppPagination` accepts an `ariaLabel` prop, but its
internal `<nav>` currently has the fixed label “Table pagination”. Also, its visible
page status renders `Page <page> of <totalItems>` rather than using `pageCount` for the
second number. Custom pagination is appropriate when either distinction matters.

## Cell rendering and slots

Without slots, each cell renders `row[column.key]` as text. Use a column-specific slot
for formatting, links, controls, badges, dates, or derived presentation:

```vue
<AppListView ...>
  <template #cell-price="{ value }">
    {{ currency.format(value as number) }}
  </template>

  <template #cell-updatedAt="{ row }">
    <time :datetime="row.updatedAt">
      {{ formatDate(row.updatedAt) }}
    </time>
  </template>

  <template #cell-name="{ row }">
    <RouterLink :to="{ name: 'product', params: { productId: row.id } }">
      {{ row.name }}
    </RouterLink>
  </template>
</AppListView>
```

The scoped values are:

- `row`: the complete typed row;
- `column`: the complete typed column definition; and
- `value`: `row[column.key]`.

Use the generic `cell` slot when several columns share a renderer. A matching
`cell-<key>` slot always takes precedence:

```vue
<template #cell="{ value }">
  <span :title="String(value)">{{ value }}</span>
</template>

<template #cell-price="{ value }">
  {{ currency.format(value as number) }}
</template>
```

For an actions column, include a field in the row interface and each row object even
when it has no meaningful data, because column keys must be row keys:

```ts
interface ProductRow extends ListRow {
  id: string;
  name: string;
  actions: null;
}

const rows: ProductRow[] = products.map((product) => ({
  id: product.id,
  name: product.name,
  actions: null
}));
```

```vue
<template #cell-actions="{ row }">
  <AppButton text="Edit" @click="editProduct(row.id)" />
  <AppButton text="Delete" @click="deleteProduct(row.id)" />
</template>
```

Interactive descendants do not cause `row-click`; the component ignores clicks whose
target is inside an `a`, `button`, `input`, `select`, `textarea`, `[role="button"]`, or
`[contenteditable]`. For another custom interactive element, use a native element when
possible or stop propagation explicitly.

## Heading and footer customisation

### Custom heading

The `header` slot replaces both the default heading and description. Preserve heading
semantics and provide any actions alongside it:

```vue
<template #header>
  <div class="list-heading">
    <div>
      <h2 id="products-title">Products</h2>
      <p>Products available to the current account.</p>
    </div>
    <AppButton text="Add product" @click="addProduct" />
  </div>
</template>
```

The outer section still has `aria-labelledby="${id}-title"`. When replacing the
header, give its heading that generated ID (for example `products-title` when
`id="products"`) so the section retains an accessible name. The `title` prop remains
required and is still used in the visually hidden table caption.

If a `description` prop is supplied, the table references `${id}-description`; ensure
the custom header renders an element with that ID. Alternatively omit `description`
and render custom descriptive text without the automatic reference.

### Custom footer

```vue
<template #footer="{ totalItems }">
  <span>{{ totalItems.toLocaleString() }} matching products</span>
</template>
```

This replaces only the default “N total results” content. When `query.filter` is
non-empty or `query.sort` is non-null, the built-in reset button remains alongside the
slot. Reset clears filter and sort, returns to page 1, and preserves page size and any
extended query fields.

There is no slot that replaces the complete footer row. To hide or restructure it,
the component implementation must be extended.

## Loading, empty, and result states

```vue
<AppListView
  title="Products"
  empty-message="No products match the current filters."
  :loading="loading"
  :columns="columns"
  :rows="rows"
  :query="query"
  :total-items="totalItems"
  @query-change="applyQuery"
/>
```

When `loading` is true, the body contains one full-width loading row regardless of the
contents of `rows`, and the section exposes `aria-busy="true"`. When loading is false
and `rows` is empty, the configured empty message is shown. Pagination and the footer
remain rendered in both states.

For refreshes, applications may keep the previous `rows` and `totalItems` while setting
`loading`; the loading row temporarily replaces them visually. Handle request races in
the owning composable or view so an older response cannot replace a newer query.

The component has no dedicated error prop or error slot. Render an error outside the
list or add an error-state API before depending on errors inside the table.

## Client-side filtering, sorting, and pagination

For small in-memory collections, derive processed rows and total count from the query:

```ts
const filteredAndSorted = computed<ProductRow[]>(() => {
  const filter = query.value.filter.toLocaleLowerCase();
  const filtered = allRows.value.filter((row) =>
    row.name.toLocaleLowerCase().includes(filter)
  );

  const sort = query.value.sort;
  if (!sort) return filtered;

  return [...filtered].sort((left, right) => {
    const result = String(left[sort.column]).localeCompare(String(right[sort.column]));
    return sort.direction === 'asc' ? result : -result;
  });
});

const totalItems = computed(() => filteredAndSorted.value.length);

const rows = computed(() => {
  const start = (query.value.page - 1) * query.value.pageSize;
  return filteredAndSorted.value.slice(start, start + query.value.pageSize);
});

const applyQuery = (nextQuery: ListQuery<ProductRow>): void => {
  query.value = nextQuery;
};
```

Use domain-aware comparisons for numbers, dates, booleans, and nullable values rather
than the generic string comparison shown in this compact example.

## Lower-level component APIs

Most consumers should use `AppListView`. The following APIs are useful when maintaining
the suite or deliberately composing a different table.

### `AppListFilter`

Props:

| Prop | Type | Default |
| --- | --- | --- |
| `modelValue` | `string` | required |
| `active` | `boolean` | required |
| `inputId` | `string` | `'list-filter'` |
| `label` | `string` | `'Filter list'` |
| `placeholder` | `string` | `'Enter filter text'` |
| `disabled` | `boolean` | `false` |
| `showFilterApply` | `boolean` | `true` |

Events are `update:modelValue(value: string)`, `apply(value: string)`, and `clear()`.
Its `filter-options` slot replaces the default `AppInputActions`. The current
implementation accepts `active` and `disabled` but does not use them in rendering or
behaviour.

### `AppListHeaderRow`

Props are `columns: ListColumn<TRow>[]` and `sort: ListSort<TRow> | null`. It emits
`sort-change(sort)` from built-in sort buttons and declares `sort-clear`, although no
built-in interaction currently emits the latter. Its generated header slots are
normally forwarded through `AppListView`.

### `AppListFooterRow`

Props are `columnCount: number`, `totalItems: number`, and optional
`showReset: boolean` (default `false`). Its default slot replaces the result-total text;
it emits `reset()` from the optional reset button.

### `AppPagination`

Props are `page`, `pageCount`, `pageSize`, `totalItems`, optional `pageSizeOptions`, and
optional `ariaLabel`. It emits `page-change(page)` and
`page-size-change(pageSize)`. Prefer consuming it through `AppListView` unless building
a custom layout.

## Accessibility and integration checklist

- Give every list on a page a unique `id`.
- Keep the required `title` meaningful, even when using a custom header, because it is
  included in the table caption.
- When replacing the header, preserve the generated heading/description IDs described
  above.
- Use native buttons, links, and inputs in cell slots so row-click suppression and
  keyboard behaviour work correctly.
- Do not replace a sortable header without recreating an accessible sort control and
  `aria-sort` behaviour.
- Keep `totalItems`, `query.page`, and the current `rows` mutually consistent.
- Reset to page 1 whenever custom filter or sort criteria change.
- Provide meaningful empty and external error messages.
- Test horizontal keyboard scrolling and slotted controls at narrow widths.
- Do not add `@click.stop` to standard interactive descendants solely to suppress
  `row-click`; the list already handles them. It is harmless when another local reason
  requires it.

## Complete customisation example

```vue
<template>
  <AppListView
    id="product-catalogue"
    title="Product catalogue"
    :columns="columns"
    :rows="rows"
    :query="query"
    :total-items="totalItems"
    :loading="loading"
    :page-size-options="[20, 50, 100]"
    :show-filter-apply="false"
    empty-message="No products match the selected filters."
    @query-change="applyQuery"
    @row-click="openProduct"
    @reset="recordReset"
  >
    <template #header>
      <div class="catalogue-heading">
        <h2 id="product-catalogue-title">Product catalogue</h2>
        <AppButton text="Add product" @click="addProduct" />
      </div>
    </template>

    <template #filter-options>
      <AppInputActions
        v-model="filterText"
        type="search"
        placeholder="Filter by name"
        @clear="setFilter('')"
      />
    </template>

    <template #column-header-name-pre>
      <AppSvg automation="product-catalogue-header-icon" :src="productIcon" size="1rem" />
    </template>

    <template #cell-name="{ row }">
      <RouterLink :to="{ name: 'product', params: { productId: row.id } }">
        {{ row.name }}
      </RouterLink>
    </template>

    <template #cell-price="{ value }">
      {{ currency.format(value as number) }}
    </template>

    <template #cell-actions="{ row }">
      <AppButton text="Edit" @click="editProduct(row.id)" />
    </template>

    <template #footer="{ totalItems: count }">
      <span>{{ count.toLocaleString() }} matching products</span>
    </template>

    <!-- Keep top controls and suppress the duplicated bottom controls. -->
    <template #bottom-pagination />
  </AppListView>
</template>
```

This pattern leaves query ownership and data loading outside the presentation
component, retains the built-in top pagination and sorting behaviour, customises the
parts that carry feature-specific meaning, and removes only the unwanted bottom
pagination region.
