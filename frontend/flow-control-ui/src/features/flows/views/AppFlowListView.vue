<template>
  <section v-bind="automation()" class="flow-library">
    <AppErrorNotice
      id="flows-error-notice"
      v-bind="automation('error')"
      :message="error ?? ''"
      :retryable="errorRetry"
      @[EVENTS.RETRY]="loadFlows"
    />

    <div class="page-heading">
      <div>
        <p>Automation workspace</p>
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
          v-bind="automation('create')"
          type="submit"
          :disabled="creating || !newFlowName.trim()"
          :text="creating ? 'Creating…' : 'New flow'"
          :icon="newFlowIcon"
        />
      </form>
    </div>

    <section class="il-import" aria-labelledby="il-import-title">
      <div>
        <h2 id="il-import-title">Import compiled Flow IL</h2>
        <p>Preview a validated artifact before saving it as a new editable draft.</p>
      </div>
      <div class="il-import-controls">
        <label for="flow-il-file">Flow IL artifact</label>
        <input
          id="flow-il-file"
          type="file"
          accept=".bin,.fil,application/octet-stream"
          @change="selectIlArtifact"
        />
        <label for="flow-il-name">Recovered flow name</label>
        <input
          id="flow-il-name"
          v-model="importName"
          type="text"
          placeholder="Use artifact flow ID"
        />
        <button data-app-button type="button" :disabled="!importArtifact || importing" @click="previewIl">
          {{ importing ? 'Validating…' : 'Preview recovery' }}
        </button>
      </div>
      <div v-if="importPreview" class="il-import-preview" role="status">
        <p>
          <strong>{{ importPreview.flow.name }}</strong> —
          {{ importPreview.flow.nodes.length }} nodes,
          {{ importPreview.flow.connections.length }} connections ({{ importPreview.recoveryLevel }}
          recovery)
        </p>
        <ul v-if="importPreview.warnings.length">
          <li v-for="warning in importPreview.warnings" :key="warning">{{ warning }}</li>
        </ul>
        <button data-app-button type="button" :disabled="importing" @click="saveIlImport">
          Save as new editable flow
        </button>
      </div>
    </section>

    <p v-if="loading" class="request-status" role="status">Loading flows…</p>
    <div v-if="!error" class="flow-results">
      <AppFilter
        v-bind="automation('filter')"
        class="table-tools"
        @[EVENTS.APPLY_FILTER]="applyFilters"
      >
        <label class="app-filter-field flow-name-filter" for="flow-filter">
          <span>Filter by name</span>
          <input
            id="flow-filter"
            v-model="filterQuery"
            type="search"
            autocomplete="off"
            placeholder="Search flow names"
          />
        </label>
        <AppMultiSelectDropdown
          v-model="filterStatuses"
          v-bind="automation('status-filter')"
          class="app-filter-field app-filter-field--content"
          label="Deployment status"
          all-label="All"
          :options="statusOptions"
        />
      </AppFilter>

      <p v-if="totalItems === 0 && hasActiveFilters" class="empty-state" role="status">
        No flows match the selected filters.
      </p>

      <AppFlowTable
        v-bind="automation('table')"
        :flows="items"
        :sort-direction="sortDirection"
        :editing-flow-id="editingFlowId"
        :rename-value="renameValue"
        :renaming="renaming"
        :confirming-delete-id="confirmingDeleteId"
        :deleting="deleting"
        :toggling-disabled-id="togglingDisabledId"
        @[EVENTS.TOGGLE_SORT]="toggleSortDirection"
        @[EVENTS.BEGIN_RENAME]="beginRename"
        @[EVENTS.UPDATE_RENAME_VALUE]="setRenameValue"
        @[EVENTS.SAVE_RENAME]="renameFlow"
        @[EVENTS.CANCEL_RENAME]="cancelRename"
        @[EVENTS.BEGIN_DELETE]="beginDelete"
        @[EVENTS.CONFIRM_DELETE]="deleteFlow"
        @[EVENTS.CANCEL_DELETE]="closeDeleteConfirmation"
        @[EVENTS.TOGGLE_DISABLED]="setFlowDisabled"
      />
      <AppTablePagination
        v-if="totalItems > 0"
        v-bind="automation('pagination')"
        :page="page"
        :page-count="pageCount"
        :page-size="pageSize"
        :range-start="rangeStart"
        :range-end="rangeEnd"
        :total-items="totalItems"
        @[EVENTS.UPDATE_PAGE]="setPage"
        @[EVENTS.UPDATE_PAGE_SIZE]="setPageSize"
      />
    </div>
  </section>
</template>

<script setup lang="ts">
import { storeToRefs } from 'pinia';
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';

import newFlowIcon from '@/assets/icons/new-flow-icon.svg';
import AppButton from '@/components/AppButton.vue';
import AppErrorNotice from '@/components/AppErrorNotice.vue';
import AppFilter from '@/components/AppFilter.vue';
import AppMultiSelectDropdown, {
  type MultiSelectOption
} from '@/components/AppMultiSelectDropdown.vue';
import AppTablePagination from '@/components/AppTablePagination.vue';
import { useServerPagination } from '@/composables/useServerPagination';
import { useAutomation } from '@/composables/useAutomation';
import { EVENTS } from '@/constants/events';
import {
  flowApi,
  type FlowIlImportResult,
  type FlowListParameters
} from '@/features/flows/api/flowApi';
import AppFlowTable from '@/features/flows/components/AppFlowTable.vue';
import { useFlowsStore } from '@/features/flows/stores/flows';

const route = useRoute();
const automation = useAutomation('flows');
const router = useRouter();
const flowStore = useFlowsStore();
const { flows } = storeToRefs(flowStore);
const loading = ref(false);
const error = ref<string>();
const errorRetry = ref(false);
const newFlowName = ref('');
const creating = ref(false);
const importArtifact = ref('');
const importName = ref('');
const importPreview = ref<FlowIlImportResult>();
const importing = ref(false);
const editingFlowId = ref<string>();
const renameValue = ref('');
const renaming = ref(false);
const confirmingDeleteId = ref<string>();
const deleting = ref(false);
const togglingDisabledId = ref<string>();
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
const filterQuery = ref(query.value);
const filterStatuses = ref([...statusFilters.value]);
const applyFilters = (): void => {
  query.value = filterQuery.value;
  statusFilters.value = [...filterStatuses.value];
  page.value = 1;
};
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
  errorRetry.value = false;
  try {
    const result = await flowApi.listFlows(
      {
        filter: query.value.trim(),
        statuses: statusFilters.value.filter(
          (status): status is 'draft' | 'deployed' => status === 'draft' || status === 'deployed'
        ),
        page: page.value,
        pageSize: pageSize.value,
        sort: sortDirection.value
      },
      controller.signal
    );
    if (listController === controller) {
      flowStore.replaceAllFlowsFromPayloads(result.items);
      applyPageMetadata(result);
    }
  } catch (caught) {
    if (listController === controller) {
      error.value = caught instanceof Error ? caught.message : 'Unable to load flows.';
      errorRetry.value = true;
    }
  } finally {
    if (listController === controller) loading.value = false;
  }
};

const createFlow = async (): Promise<void> => {
  const name = newFlowName.value.trim();
  errorRetry.value = false;
  if (!name) {
    error.value = 'Enter a name for the new flow.';
    return;
  }
  creating.value = true;
  error.value = undefined;
  try {
    const createdFlow = await flowApi.createFlow(name);
    newFlowName.value = '';
    await router.push({
      name: 'flow-designer',
      params: { flowId: createdFlow.id }
    });
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to create the flow.';
  } finally {
    creating.value = false;
  }
};

const selectIlArtifact = async (event: Event): Promise<void> => {
  const file = (event.target as HTMLInputElement).files?.[0];
  importPreview.value = undefined;
  importArtifact.value = '';
  if (!file) return;
  if (file.size > 8192) {
    error.value = 'Flow IL artifacts must not exceed 8192 bytes.';
    return;
  }
  const bytes = new Uint8Array(await file.arrayBuffer());
  importArtifact.value = btoa(String.fromCharCode(...bytes));
};

const previewIl = async (): Promise<void> => {
  if (!importArtifact.value) return;
  importing.value = true;
  error.value = undefined;
  try {
    importPreview.value = await flowApi.importFlowIl(
      importArtifact.value,
      importName.value.trim() || undefined,
      false
    );
  } catch (caught) {
    error.value =
      caught instanceof Error ? caught.message : 'Unable to preview the Flow IL artifact.';
  } finally {
    importing.value = false;
  }
};

const saveIlImport = async (): Promise<void> => {
  if (!importArtifact.value || !importPreview.value) return;
  importing.value = true;
  error.value = undefined;
  try {
    const result = await flowApi.importFlowIl(
      importArtifact.value,
      importName.value.trim() || importPreview.value.flow.name,
      true
    );
    await router.push({ name: 'flow-designer', params: { flowId: result.flow.id } });
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to save the recovered flow.';
  } finally {
    importing.value = false;
  }
};

const beginRename = (flowId: string, name: string): void => {
  editingFlowId.value = flowId;
  renameValue.value = name;
};
const setRenameValue = (value: string): void => {
  renameValue.value = value;
};
const cancelRename = (): void => {
  editingFlowId.value = undefined;
};
const setPageSize = (value: number): void => {
  pageSize.value = value;
};

const beginDelete = (flowId: string): void => {
  confirmingDeleteId.value = flowId;
};

const renameFlow = async (flowId: string): Promise<void> => {
  const payload = flowStore.flowPayload(flowId);
  const name = renameValue.value.trim();
  errorRetry.value = false;
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
  errorRetry.value = false;
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

const setFlowDisabled = async (flowId: string, disabled: boolean): Promise<void> => {
  togglingDisabledId.value = flowId;
  errorRetry.value = false;
  error.value = undefined;
  try {
    const saved = await flowApi.setFlowDisabled(flowId, disabled);
    flowStore.replaceFlowFromPayload(saved);
  } catch (caught) {
    error.value =
      caught instanceof Error ? caught.message : 'Unable to change the flow execution state.';
  } finally {
    togglingDisabledId.value = undefined;
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
  margin: var(--space-0) auto;
  padding: var(--space-29) var(--space-0);
}

.page-heading {
  display: flex;
  gap: var(--space-16);
  align-items: end;
  justify-content: space-between;
  margin-bottom: var(--space-17);
}

.eyebrow {
  margin: var(--space-0) var(--space-0) var(--space-3-5);
  color: var(--color-action-primary);
  font-size: var(--font-size-sm);
  font-weight: var(--font-weight-black);
  letter-spacing: 0.13em;
  text-transform: uppercase;
}

h1 {
  margin: var(--space-0);
  color: var(--color-text-primary);
  font-size: var(--font-size-hero-fluid);
  letter-spacing: -0.04em;
}

.page-heading p:last-child {
  max-width: 560px;
  margin: var(--space-4-5) var(--space-0) var(--space-0);
  color: var(--color-text-secondary);
}

.create-flow {
  display: flex;
  gap: var(--space-3-5);
  align-items: center;
}

.create-flow label {
  color: var(--color-text-primary);
  font-size: var(--font-size-md);
  font-weight: var(--font-weight-bold);
}

.create-flow input {
  min-height: var(--control-min-height);
  padding: var(--space-4);
  color: var(--color-text-primary);
  background: var(--color-surface-raised);
  border: var(--border-width-default) solid var(--color-border-default);
  border-radius: var(--radius-md);
}

.il-import {
  display: grid;
  gap: var(--space-6);
  margin-bottom: var(--space-11);
  padding: var(--space-8);
  background: var(--color-surface-raised);
  border: var(--border-width-default) solid var(--color-border-default);
  border-radius: var(--radius-xl);
}

.il-import h2,
.il-import p {
  margin: 0;
}

.il-import-controls {
  display: flex;
  gap: var(--space-3-5);
  align-items: center;
  flex-wrap: wrap;
}

.il-import-controls label {
  font-weight: var(--font-weight-bold);
}

.il-import-controls input,
.il-import button {
  min-height: var(--control-min-height);
}

.il-import-preview {
  padding-top: var(--space-5);
  border-top: var(--border-width-default) solid var(--color-border-default);
}

.create-flow input {
  min-width: 180px;
}

.request-status,
.empty-state {
  margin-bottom: var(--space-11);
  padding: var(--space-8);
  border-radius: var(--radius-xl);
}

.request-status {
  color: var(--color-info-text);
  background: var(--color-info-surface);
}

.empty-state {
  color: var(--color-text-muted);
  background: var(--color-surface-raised);
  border: var(--border-width-default) dashed var(--color-border-empty);
}

.empty-state h2 {
  margin-top: var(--space-0);
}

.table-tools {
  display: flex;
  gap: var(--space-3-5);
  align-items: center;
  margin-bottom: var(--space-8);
}

.filter-control {
  display: flex;
  gap: var(--space-3-5);
  align-items: center;
}

/* Mobile breakpoint (40rem): stacks page and navigation content for phone layouts. */
@media (max-width: 40rem) {
  .flow-library {
    width: min(100% - 28px, 1180px);
    padding: var(--space-19) var(--space-0);
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
}
</style>
