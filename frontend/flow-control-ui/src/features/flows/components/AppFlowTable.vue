<template>
  <div>
    <AppMultiSelectDropdown
      v-model="filterStatuses"
      v-bind="automation('status-filter')"
      class="app-filter-field app-filter-field--content"
      label="Deployment status"
      all-label="All"
      :options="statusOptions"
    />

    <AppListView
      id="flow-list"
      v-bind="automation()"
      title="Flows"
      description="List of defined flows."
      class="flow-list"
      :columns="columns"
      :rows="sortedRows"
      :query="query"
      :total-items="totalItems"
      :page-size-options="[5, 10, 25]"
      @query-change="query = $event"
    >
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
            v-bind="automation('save-name')"
            type="submit"
            text="Save name"
            :icon="saveIcon"
            hide-text
            :disabled="renaming"
          />
          <AppButton
            v-bind="automation('cancel-rename')"
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
        <span class="nodes" :class="[row.nodes, { disabled: row.disabled }]">
          {{ row.nodes.length }}
        </span>
      </template>
      <template #cell-updatedAt="{ row }">
        <time :datetime="row.updatedAt">{{ formattedUpdatedAt(row) }}</time>
      </template>
      <template #cell-disabled="{ row }">
        <a :href="`tel:${row.disabled}`">{{ row.disabled }}</a>
      </template>
      <template #cell-actions="{ row }">
        <div class="actions">
          <AppButton
            v-bind="automation('toggle-disabled')"
            class="light-weight"
            :text="row.disabled ? 'Enable' : 'Disable'"
            :icon="row.disabled ? enableFlowIcon : disableFlowIcon"
            :disabled="togglingDisabledId === row.id"
            @click="emit(EVENTS.TOGGLE_DISABLED, row.id, !row.disabled)"
          />
          <AppButton
            v-bind="automation('rename')"
            class="light-weight"
            text="Rename"
            :icon="renameFlowIcon"
            @click="emit(EVENTS.BEGIN_RENAME, row.id, row.name)"
          />
          <AppButton
            v-bind="automation('delete')"
            class="light-weight"
            text="Delete"
            :icon="deleteFlowIcon"
            @click="emit(EVENTS.BEGIN_DELETE, row.id)"
          />
          <div
            v-if="confirmingDeleteId === row.id"
            :ref="setDeleteDialog"
            class="delete-confirmation"
            role="alertdialog"
            :aria-label="`Delete ${row.name}?`"
            :aria-describedby="`delete-description-${row.id}`"
            aria-modal="true"
            tabindex="-1"
            @keydown="handleDeleteDialogKeydown"
          >
            <span :id="`delete-description-${row.id}`">Delete this flow?</span>
            <AppButton
              v-bind="automation('confirm-delete')"
              text="Confirm delete"
              :icon="deleteFlowIcon"
              :disabled="deleting"
              @click="$emit(EVENTS.CONFIRM_DELETE, row.id.toString())"
            />
            <AppButton
              v-bind="automation('cancel-delete')"
              text="Cancel"
              :icon="cancelIcon"
              data-dialog-initial-focus
              @click="$emit(EVENTS.CANCEL_DELETE)"
            />
          </div>
        </div>
      </template>
    </AppListView>
  </div>
</template>

<script setup lang="ts">
import { computed, ref, type ComponentPublicInstance } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { useAutomation } from '@/composables/useAutomation';
import type { SortDirection } from '@/composables/usePaginatedCollection';
import { useModalFocus } from '@/features/flows/composables/useModalFocus';

import { EVENTS } from '@/constants/events';
import type { FlowDefinition, FlowNode } from '@/features/flows/types';
import AppListView from '@/components/list-view/AppListView.vue';
import AppButton from '@/components/AppButton.vue';
import type { ListColumn, ListQuery, ListRow } from '@/models';
import AppMultiSelectDropdown, {
  type MultiSelectOption
} from '@/components/AppMultiSelectDropdown.vue';

import cancelIcon from '@/assets/icons/cancel-icon.svg';
import deleteFlowIcon from '@/assets/icons/delete-flow-icon.svg';
import disableFlowIcon from '@/assets/icons/disable-flow-icon.svg';
import enableFlowIcon from '@/assets/icons/enable-flow-icon.svg';
import renameFlowIcon from '@/assets/icons/rename-flow-icon.svg';
import saveIcon from '@/assets/icons/save-icon.svg';

const props = defineProps<{
  automation: string;
  flows: FlowDefinition[];
  sortDirection: SortDirection;
  editingFlowId?: string;
  renameValue: string;
  renaming: boolean;
  confirmingDeleteId?: string;
  deleting: boolean;
  togglingDisabledId?: string;
}>();

const route = useRoute();
const router = useRouter();
const automation = useAutomation(props.automation);
const emit = defineEmits<{
  (event: typeof EVENTS.TOGGLE_SORT): void;
  (event: typeof EVENTS.BEGIN_RENAME, flowId: string, name: string): void;
  (event: typeof EVENTS.UPDATE_RENAME_VALUE, value: string): void;
  (event: typeof EVENTS.SAVE_RENAME, flowId: string): void;
  (event: typeof EVENTS.CANCEL_RENAME): void;
  (event: typeof EVENTS.BEGIN_DELETE, flowId: string): void;
  (event: typeof EVENTS.CONFIRM_DELETE, flowId: string): void;
  (event: typeof EVENTS.CANCEL_DELETE): void;
  (event: typeof EVENTS.TOGGLE_DISABLED, flowId: string, disabled: boolean): void;
}>();

export interface FlowRow extends ListRow {
  id: string;
  name: string;
  updatedAt: string;
  nodes: FlowNode[];
  status: string;
  disabled: boolean;
  actions: string;
}

const columns: ListColumn<FlowRow>[] = [
  {
    key: 'name',
    label: 'Name',
    automation: 'name',
    sortable: true
  },
  {
    key: 'status',
    label: 'Status',
    automation: 'status',
    sortable: true,
    width: '12rem'
  },
  {
    key: 'nodes',
    label: 'Nodes',
    automation: 'nodes',
    width: '12rem'
  },
  {
    key: 'updatedAt',
    label: 'Updated',
    automation: 'updated-at',
    width: '12rem'
  },
  {
    key: 'actions',
    label: 'Actions',
    automation: 'actions',
    sortable: false,
    width: '24rem'
  }
];

const statusOptions: MultiSelectOption[] = [
  { label: 'Draft', value: 'draft' },
  { label: 'Deployed', value: 'deployed' }
];

const requestedStatuses = Array.isArray(route.query.status)
  ? route.query.status
  : route.query.status
    ? [route.query.status]
    : [];

const validRequestedStatuses = requestedStatuses.filter(
  (status): status is 'draft' | 'deployed' => status === 'draft' || status === 'deployed'
);

const statusFilters = ref<string[]>(
  validRequestedStatuses.length > 0
    ? validRequestedStatuses
    : statusOptions.map(({ value }) => value)
);

const query = ref<ListQuery<FlowRow>>({
  page: 1,
  pageSize: 5,
  filter: '',
  sort: null
});
const filterStatuses = ref([...statusFilters.value]);

const rows = computed<FlowRow[]>(() =>
  props.flows.map((flow) => ({
    id: flow.id,
    name: flow.name,
    updatedAt: flow.updatedAt,
    nodes: flow.nodes,
    status: flow.status,
    disabled: flow.disabled,
    automation: `row-${flow.id}`,
    actions: ''
  }))
);

const deleteDialog = ref<HTMLElement>();
const deleteDialogOpen = computed(() => !!props.confirmingDeleteId);
const setDeleteDialog = (element: Element | ComponentPublicInstance | null): void => {
  deleteDialog.value = element instanceof HTMLElement ? element : undefined;
};
const { handleKeydown: handleDeleteDialogKeydown } = useModalFocus(
  deleteDialog,
  deleteDialogOpen,
  () => emit(EVENTS.CANCEL_DELETE)
);

const filteredRows = computed(() => {
  const filter = query.value.filter.trim().toLocaleLowerCase();

  if (!filter) {
    return rows.value;
  }

  return rows.value.filter((row) =>
    [row.name, row.status, row.disabled.toString()].some((value) =>
      value.toLocaleLowerCase().includes(filter)
    )
  );
});

const sortedRows = computed(() => {
  const sort = query.value.sort;

  if (!sort) {
    return filteredRows.value;
  }

  return [...filteredRows.value].sort((left, right): number => {
    const leftValue = String(left[sort.column]);
    const rightValue = String(right[sort.column]);

    const result = leftValue.localeCompare(rightValue, undefined, {
      numeric: true,
      sensitivity: 'base'
    });

    return sort.direction === 'asc' ? result : -result;
  });
});

const totalItems = computed(() => sortedRows.value.length);

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
</style>
