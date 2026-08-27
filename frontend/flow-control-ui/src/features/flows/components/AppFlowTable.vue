<template>
  <div>
    <AppListView
      id="flow-list"
      title="Flows"
      class="flow-list"
      :show-filter-apply="false"
      :columns="columns"
      :rows="rows"
      :query="query"
      :total-items="props.totalItems"
      :page-size-options="[10, 20, 50]"
      @query-change="updateQuery"
    >
      <template #header>
        <h2 class="flow-list-heading">Flow List</h2>
      </template>
      <template #filter-options>
        <div class="filter-options">
          <AppMultiSelectDropdown
            v-model="filterStatuses"
            class="app-filter-field app-filter-field--content"
            label="Deployment status"
            all-label="All"
            :options="statusOptions"
          />

          <AppInputActions
            v-model="filterText"
            placeholder="Enter a flow name"
            autocomplete="off"
            show-action
            type="search"
            :action-disabled="filterText.length === 0"
          />
        </div>
      </template>

      <template #column-header-name-pre>
        <AppButton
          type="button"
          class="add-flow-btn"
          text="Add flow"
          :icon="newIcon"
          aria-label="Add a new flow"
          hide-text
          @click="$emit(EVENTS.ADD_FLOW)"
        />
      </template>

      <template #cell-name="{ row }">
        <form
          v-if="editingFlowId === row.id"
          class="rename-flow"
          @click.stop
          @submit.prevent="$emit(EVENTS.SAVE_RENAME, row.id)"
        >
          <label :for="`rename-${row.id}`">Rename {{ row.name }}</label>

          <input
            :id="`rename-${row.id}`"
            :value="renameValue"
            type="text"
            @input="$emit(EVENTS.UPDATE_RENAME_VALUE, ($event.target as HTMLInputElement).value)"
          />

          <AppButton
            type="submit"
            text="Save name"
            :icon="saveIcon"
            hide-text
            :disabled="renaming"
          />

          <AppButton
            text="Cancel"
            :icon="cancelIcon"
            hide-text
            @click="$emit(EVENTS.CANCEL_RENAME)"
          />
        </form>

        <RouterLink
          v-else
          class="flow-name"
          :to="{ name: 'flow-designer', params: { flowId: row.id } }"
          @click.stop
        >
          {{ row.name }}
        </RouterLink>
      </template>

      <template #cell-status="{ row }">
        <span class="status" :class="[row.status, { disabled: row.disabled }]">
          {{ row.disabled ? `${row.status} · disabled` : row.status }}
        </span>
      </template>

      <template #cell-nodes="{ row }">
        <span class="nodes" :class="{ disabled: row.disabled }">
          {{ row.nodes.length }}
        </span>
      </template>

      <template #cell-updatedAt="{ row }">
        <time :datetime="row.updatedAt">
          {{ formattedUpdatedAt(row) }}
        </time>
      </template>

      <template #cell-disabled="{ row }">
        <a :href="`tel:${row.disabled}`">{{ row.disabled }}</a>
      </template>

      <template #cell-actions="{ row }">
        <div class="actions">
          <AppButton
            class="light-weight"
            :text="row.disabled ? 'Enable' : 'Disable'"
            :icon="row.disabled ? enableFlowIcon : disableFlowIcon"
            :disabled="togglingDisabledId === row.id"
            @click="emit(EVENTS.TOGGLE_DISABLED, row.id, !row.disabled)"
          />

          <AppButton
            class="light-weight"
            text="Rename"
            :icon="renameFlowIcon"
            @click="emit(EVENTS.BEGIN_RENAME, row.id, row.name)"
          />

          <AppButton
            class="light-weight"
            text="Delete"
            :icon="deleteFlowIcon"
            @click="emit(EVENTS.BEGIN_DELETE, row.id)"
          />
        </div>
      </template>
    </AppListView>
  </div>
</template>

<script setup lang="ts">
import { computed, ref, watch } from 'vue';

import type { SortDirection } from '@/composables/usePaginatedCollection';

import { EVENTS } from '@/constants/events';
import type { FlowDefinition, FlowNode } from '@/features/flows/types';
import type { ListColumn, ListQuery, ListRow } from '@/models';

import AppListView from '@/components/list-view/AppListView.vue';
import AppButton from '@/components/AppButton.vue';
import AppInputActions from '@/components/AppInputActions.vue';
import AppMultiSelectDropdown, {
  type MultiSelectOption
} from '@/components/AppMultiSelectDropdown.vue';

import cancelIcon from '@/assets/icons/cancel-icon.svg';
import deleteFlowIcon from '@/assets/icons/delete-flow-icon.svg';
import disableFlowIcon from '@/assets/icons/disable-flow-icon.svg';
import enableFlowIcon from '@/assets/icons/enable-flow-icon.svg';
import renameFlowIcon from '@/assets/icons/rename-flow-icon.svg';
import saveIcon from '@/assets/icons/save-icon.svg';
import newIcon from '@/assets/icons/new-icon.svg';

type FlowStatus = 'draft' | 'deployed';

const props = defineProps<{
  flows: FlowDefinition[];
  filter: string;
  statuses: FlowStatus[];
  page: number;
  pageSize: number;
  totalItems: number;
  sortDirection: SortDirection;
  editingFlowId?: string;
  renameValue: string;
  renaming: boolean;
  togglingDisabledId?: string;
}>();

const emit = defineEmits<{
  'add-flow': [];
  'toggle-sort': [];
  'update:filter': [filter: string];
  'update:statuses': [statuses: FlowStatus[]];
  'update:page': [page: number];
  'update:pageSize': [pageSize: number];
  'begin-rename': [flowId: string, name: string];
  'update:renameValue': [value: string];
  'save-rename': [flowId: string];
  'cancel-rename': [];
  'begin-delete': [flowId: string];
  'toggle-disabled': [flowId: string, disabled: boolean];
}>();

export interface FlowRow extends ListRow {
  id: string;
  name: string;
  updatedAt: string;
  nodes: FlowNode[];
  status: FlowStatus;
  disabled: boolean;
  actions: string;
}

interface FlowListQuery extends ListQuery<FlowRow> {
  statuses: FlowStatus[];
}

const columns: ListColumn<FlowRow>[] = [
  {
    key: 'name',
    label: 'Name',
    sortable: true
  },
  {
    key: 'status',
    label: 'Status',
    sortable: true,
    width: '12rem'
  },
  {
    key: 'nodes',
    label: 'Nodes',
    width: '12rem'
  },
  {
    key: 'updatedAt',
    label: 'Updated',
    width: '12rem'
  },
  {
    key: 'actions',
    label: 'Actions',
    sortable: false,
    width: '24rem'
  }
];

const statusOptions: MultiSelectOption[] = [
  { label: 'Draft', value: 'draft' },
  { label: 'Deployed', value: 'deployed' }
];

const query = ref<FlowListQuery>({
  page: props.page,
  pageSize: props.pageSize,
  filter: props.filter,
  statuses: [...props.statuses],
  sort: {
    column: 'name',
    direction: props.sortDirection === 'ascending' ? 'asc' : 'desc'
  }
});

const updateQuery = (nextQuery: FlowListQuery): void => {
  const requestedDirection = nextQuery.sort?.direction;
  const currentDirection = query.value.sort?.direction;
  query.value = nextQuery;
  if (nextQuery.page !== props.page) emit(EVENTS.UPDATE_PAGE, nextQuery.page);
  if (nextQuery.pageSize !== props.pageSize) emit(EVENTS.UPDATE_PAGE_SIZE, nextQuery.pageSize);
  if (nextQuery.sort?.column === 'name' && requestedDirection !== currentDirection) {
    emit(EVENTS.TOGGLE_SORT);
  }
};

watch(
  () => [props.filter, props.statuses, props.page, props.pageSize, props.sortDirection] as const,
  ([filter, statuses, page, pageSize, sortDirection]) => {
    query.value = {
      ...query.value,
      filter,
      statuses: [...statuses],
      page,
      pageSize,
      sort: {
        column: 'name',
        direction: sortDirection === 'ascending' ? 'asc' : 'desc'
      }
    };
  }
);

const filterText = computed({
  get: () => props.filter,
  set: (filter: string) => {
    emit('update:filter', filter);
  }
});

const filterStatuses = computed({
  get: () => props.statuses,
  set: (statuses: FlowStatus[]) => {
    emit('update:statuses', statuses);
  }
});

const rows = computed<FlowRow[]>(() =>
  props.flows.map((flow) => ({
    id: flow.id,
    name: flow.name,
    updatedAt: flow.updatedAt,
    nodes: flow.nodes,
    status: flow.status as FlowStatus,
    disabled: flow.disabled,
    actions: ''
  }))
);

const formattedUpdatedAt = (row: FlowRow): string =>
  new Intl.DateTimeFormat(undefined, {
    dateStyle: 'medium',
    timeStyle: 'short'
  }).format(new Date(row.updatedAt));
</script>

<style lang="css">
.status {
  display: inline-block;
  padding: var(--space-2) var(--space-3-5);
  color: var(--color-text-secondary);
  font-size: var(--font-size-xs);
  font-weight: var(--font-weight-black);
  letter-spacing: 0.08em;
  background: var(--color-surface-neutral);
  border-radius: var(--radius-pill);
  text-transform: uppercase;
}

.status.deployed {
  color: var(--color-action-primary-strong);
  background: var(--color-action-primary-surface);
}

.flow-list {
  background: var(--color-surface-raised);
}

.actions {
  display: flex;
  justify-content: space-evenly;
}

.filter-options {
  display: flex;
  flex: 1;
  gap: 1.5em;
}

.flow-list-heading {
  font-size: 1.5rem;
  padding: 0.5rem;
  background-color: var(--color-surface-raised);
  border-radius: var(--radius-md);
  border: var(--border-width-default) solid var(--color-border-default);
}

.add-flow-btn {
  margin-right: 0.5rem;
}
</style>
