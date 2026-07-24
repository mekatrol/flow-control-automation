<template>
  <section class="flow-library">
    <div class="page-heading">
      <div>
        <p class="eyebrow">Automation workspace</p>
        <h1>Flows</h1>
        <p>Design, inspect, and deploy independent automation flows.</p>
      </div>
      <form class="create-flow" @submit.prevent="createFlow">
        <label for="new-flow-name">New flow name</label>
        <input
          id="new-flow-name"
          v-model="newFlowName"
          autocomplete="off"
          name="new-flow-name"
          type="text"
          placeholder="Enter new flow name"
        />
        <AppButton
          type="submit"
          :disabled="creating || !newFlowName.trim()"
          :text="creating ? 'Creating…' : 'New flow'"
          :icon="newFlowIcon"
        />
      </form>
    </div>

    <p v-if="loading" class="request-status" role="status">Loading flows…</p>
    <div v-if="error" class="request-error" role="alert">
      <span>{{ error }}</span>
      <AppButton text="Retry" :icon="retryIcon" @click="loadFlows" />
    </div>

    <div
      v-if="!loading && !error && totalItems === 0 && !hasActiveFilters"
      class="empty-state"
    >
      <h2>No flows yet</h2>
      <p>Create a flow to start designing an automation.</p>
    </div>

    <div v-if="!loading && !error" class="flow-results">
      <div class="table-tools">
        <div class="filter-control">
          <label for="flow-filter">Filter by name</label>
          <input
            id="flow-filter"
            v-model="query"
            type="search"
            autocomplete="off"
            placeholder="Search flow names"
          />
        </div>
        <MultiSelectDropdown
          v-model="statusFilters"
          label="Deployment status"
          all-label="All"
          :options="statusOptions"
        />
      </div>

      <p v-if="totalItems === 0 && hasActiveFilters" class="empty-state" role="status">
        No flows match the selected filters.
      </p>

      <template v-if="totalItems > 0">
        <FlowTable
          :flows="items"
          :sort-direction="sortDirection"
          :editing-flow-id="editingFlowId"
          :rename-value="renameValue"
          :renaming="renaming"
          :confirming-delete-id="confirmingDeleteId"
          :deleting="deleting"
          @toggle-sort="toggleSortDirection"
          @begin-rename="beginRename"
          @update:rename-value="renameValue = $event"
          @save-rename="renameFlow"
          @cancel-rename="editingFlowId = undefined"
          @begin-delete="beginDelete"
          @confirm-delete="deleteFlow"
          @cancel-delete="closeDeleteConfirmation"
        />
        <TablePagination
          :page="page"
          :page-count="pageCount"
          :page-size="pageSize"
          :range-start="rangeStart"
          :range-end="rangeEnd"
          :total-items="totalItems"
          @update:page="setPage"
          @update:page-size="pageSize = $event"
        />
      </template>
    </div>
  </section>
</template>

<script setup lang="ts">
import { storeToRefs } from 'pinia';
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';

import newFlowIcon from '@/assets/new-flow-icon.svg';
import retryIcon from '@/assets/retry-icon.svg';
import AppButton from '@/components/AppButton.vue';
import MultiSelectDropdown, {
  type MultiSelectOption
} from '@/components/MultiSelectDropdown.vue';
import TablePagination from '@/components/TablePagination.vue';
import { useServerPagination } from '@/composables/useServerPagination';
import { flowApi, type FlowListParameters } from '@/features/flows/api/flowApi';
import FlowTable from '@/features/flows/components/FlowTable.vue';
import { useFlowsStore } from '@/features/flows/stores/flows';

const route = useRoute();
const router = useRouter();
const flowStore = useFlowsStore();
const { flows } = storeToRefs(flowStore);
const loading = ref(false);
const error = ref<string>();
const newFlowName = ref('');
const creating = ref(false);
const editingFlowId = ref<string>();
const renameValue = ref('');
const renaming = ref(false);
const confirmingDeleteId = ref<string>();
const deleting = ref(false);
let listController: AbortController | undefined;
let listTimer: ReturnType<typeof setTimeout> | undefined;

const queryValue = (value: unknown): string => (typeof value === 'string' ? value : '');
const positiveInteger = (value: unknown, fallback: number): number => {
  const parsed = Number(value);
  return Number.isInteger(parsed) && parsed > 0 ? parsed : fallback;
};
const requestedPageSize = positiveInteger(route.query.pageSize, 10);
const initialPageSize = [10, 20, 50].includes(requestedPageSize) ? requestedPageSize : 10;
const initialSortDirection: FlowListParameters['sort'] =
  route.query.sort === 'descending' ? 'descending' : 'ascending';
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
const {
  query,
  page,
  pageSize,
  sortDirection,
  totalItems,
  pageCount,
  rangeStart,
  rangeEnd,
  setPage,
  toggleSortDirection,
  applyPageMetadata
} = useServerPagination({
  initialQuery: queryValue(route.query.filter),
  initialPage: positiveInteger(route.query.page, 1),
  initialPageSize,
  initialSortDirection
});
const hasActiveFilters = computed(
  () =>
    query.value.trim().length > 0 ||
    !statusOptions.every(({ value }) => statusFilters.value.includes(value))
);
const items = computed(() => flows.value);

watch(
  statusFilters,
  () => {
    page.value = 1;
  },
  { deep: true, flush: 'sync' }
);

watch(
  [query, statusFilters, page, pageSize, sortDirection],
  ([filter, statuses, currentPage, currentPageSize, sort]) => {
    const nextQuery: Record<string, string | string[]> = {};
    if (filter.trim()) nextQuery.filter = filter;
    if (statuses.length > 0) nextQuery.status = [...statuses];
    if (currentPage > 1) nextQuery.page = String(currentPage);
    if (currentPageSize !== 10) nextQuery.pageSize = String(currentPageSize);
    if (sort !== 'ascending') nextQuery.sort = sort;
    void router.replace({ query: nextQuery });
    clearTimeout(listTimer);
    listTimer = setTimeout(() => void loadFlows(), 200);
  },
  { deep: true }
);

const closeDeleteConfirmation = (): void => {
  confirmingDeleteId.value = undefined;
};

const loadFlows = async (): Promise<void> => {
  listController?.abort();
  const controller = new AbortController();
  listController = controller;
  loading.value = true;
  error.value = undefined;
  try {
    const result = await flowApi.listFlows({
      filter: query.value.trim(),
        statuses: statusFilters.value.filter(
          (status): status is 'draft' | 'deployed' => status === 'draft' || status === 'deployed'
        ),
      page: page.value,
      pageSize: pageSize.value,
      sort: sortDirection.value
    }, controller.signal);
    if (listController === controller) {
      flowStore.replaceAllFlowsFromPayloads(result.items);
      applyPageMetadata(result);
    }
  } catch (caught) {
    if (listController === controller) {
      error.value = caught instanceof Error ? caught.message : 'Unable to load flows.';
    }
  } finally {
    if (listController === controller) loading.value = false;
  }
};

const createFlow = async (): Promise<void> => {
  const name = newFlowName.value.trim();
  if (!name) {
    error.value = 'Enter a name for the new flow.';
    return;
  }
  creating.value = true;
  error.value = undefined;
  try {
    await flowApi.createFlow(name);
    newFlowName.value = '';
    await loadFlows();
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to create the flow.';
  } finally {
    creating.value = false;
  }
};

const beginRename = (flowId: string, name: string): void => {
  editingFlowId.value = flowId;
  renameValue.value = name;
};

const beginDelete = (flowId: string): void => {
  confirmingDeleteId.value = flowId;
};

const renameFlow = async (flowId: string): Promise<void> => {
  const payload = flowStore.flowPayload(flowId);
  const name = renameValue.value.trim();
  if (!payload || !name) {
    error.value = 'Flow name is required.';
    return;
  }
  renaming.value = true;
  error.value = undefined;
  try {
    await flowApi.saveFlow({ ...payload, name });
    editingFlowId.value = undefined;
    await loadFlows();
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to rename the flow.';
  } finally {
    renaming.value = false;
  }
};

const deleteFlow = async (flowId: string): Promise<void> => {
  deleting.value = true;
  error.value = undefined;
  try {
    await flowApi.deleteFlow(flowId);
    closeDeleteConfirmation();
    await loadFlows();
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to delete the flow.';
  } finally {
    deleting.value = false;
  }
};

onMounted(() => void loadFlows());
onBeforeUnmount(() => {
  clearTimeout(listTimer);
  listController?.abort();
});
</script>

<style scoped>
.flow-library {
  width: min(1180px, calc(100% - 40px));
  margin: 0 auto;
  padding: 58px 0;
}

.page-heading {
  display: flex;
  gap: 32px;
  align-items: end;
  justify-content: space-between;
  margin-bottom: 34px;
}

.eyebrow {
  margin: 0 0 8px;
  color: var(--color-action-primary);
  font-size: 11px;
  font-weight: 800;
  letter-spacing: 0.13em;
  text-transform: uppercase;
}

h1 {
  margin: 0;
  color: var(--color-text-primary);
  font-size: clamp(34px, 5vw, 52px);
  letter-spacing: -0.04em;
}

.page-heading p:last-child {
  max-width: 560px;
  margin: 10px 0 0;
  color: var(--color-text-secondary);
}

.create-flow,
.request-error {
  display: flex;
  gap: 8px;
  align-items: center;
}

.create-flow label,
.table-tools label {
  color: var(--color-text-primary);
  font-size: 12px;
  font-weight: 700;
}

.create-flow input,
.table-tools input {
  min-height: 44px;
  padding: 9px;
  color: var(--color-text-primary);
  background: var(--color-surface-raised);
  border: 1px solid var(--color-border-default);
  border-radius: 7px;
}

.create-flow input {
  min-width: 180px;
}

.create-flow :deep(button),
.request-error :deep(button) {
  min-height: 44px;
  padding: 10px 14px;
  color: var(--color-text-secondary);
  font-weight: 700;
  background: var(--color-surface-neutral);
  border: 1px solid var(--color-border-default);
  border-radius: 9px;
}

.create-flow :deep(button:not(:disabled)) {
  color: var(--color-text-on-primary);
  cursor: pointer;
  background: var(--color-action-primary);
  border-color: var(--color-action-primary);
}

.create-flow :deep(button:disabled) {
  color: var(--color-text-muted);
  cursor: not-allowed;
  background: var(--color-surface-disabled);
  border-style: dashed;
}

.request-status,
.request-error,
.empty-state {
  margin-bottom: 22px;
  padding: 16px;
  border-radius: 10px;
}

.request-status {
  color: var(--color-info-text);
  background: var(--color-info-surface);
}

.request-error {
  justify-content: space-between;
  color: var(--color-danger-text);
  background: var(--color-danger-surface);
}

.empty-state {
  color: var(--color-text-muted);
  background: var(--color-surface-raised);
  border: 1px dashed var(--color-border-empty);
}

.empty-state h2 {
  margin-top: 0;
}

.table-tools {
  display: flex;
  gap: 8px;
  align-items: center;
  margin-bottom: 16px;
}

.filter-control {
  display: flex;
  gap: 8px;
  align-items: center;
}

.table-tools input {
  width: min(360px, 100%);
}

@media (max-width: 640px) {
  .flow-library {
    width: min(100% - 28px, 1180px);
    padding: 38px 0;
  }

  .page-heading {
    align-items: start;
    flex-direction: column;
  }

  .create-flow {
    flex-wrap: wrap;
  }

  .table-tools {
    align-items: stretch;
    flex-direction: column;
  }

  .filter-control {
    align-items: stretch;
    flex-direction: column;
  }

}
</style>
