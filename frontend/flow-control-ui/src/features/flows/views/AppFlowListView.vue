<template>
  <section class="list-page">
    <AppErrorNotice
      id="flows-error-notice"
      :message="error ?? ''"
      :retryable="errorRetry"
      @[EVENTS.RETRY]="loadFlows"
    />

    <p v-if="isSpinnerVisible" class="request-status" role="status">Loading flows…</p>

    <div v-if="!error" class="flow-results">
      <p v-if="totalItems === 0 && hasActiveFilters" class="empty-state" role="status">
        No flows match the selected filters.
      </p>

      <AppFlowTable
        :flows="items"
        :filter="query"
        :statuses="statusFilters"
        :page="page"
        :page-size="pageSize"
        :total-items="totalItems"
        :sort-direction="sortDirection"
        :editing-flow-id="editingFlowId"
        :rename-value="renameValue"
        :renaming="renaming"
        :toggling-disabled-id="togglingDisabledId"
        :loading="isSpinnerVisible"
        @[EVENTS.TOGGLE_SORT]="toggleSortDirection"
        @update:filter="query = $event"
        @update:statuses="statusFilters = $event"
        @[EVENTS.UPDATE_PAGE]="updatePage"
        @[EVENTS.UPDATE_PAGE_SIZE]="updatePageSize"
        @[EVENTS.BEGIN_RENAME]="beginRename"
        @[EVENTS.UPDATE_RENAME_VALUE]="setRenameValue"
        @[EVENTS.SAVE_RENAME]="renameFlow"
        @[EVENTS.CANCEL_RENAME]="cancelRename"
        @[EVENTS.BEGIN_DELETE]="beginDelete"
        @[EVENTS.TOGGLE_DISABLED]="setFlowDisabled"
        @reenable="reenableFlow"
        @[EVENTS.ADD_FLOW]="showCreateFlowDialog"
        @[EVENTS.IMPORT_FLOW]="showImportDialog"
        @[EVENTS.EXPORT_FLOW]="exportFlow"
      />
    </div>

    <AppFlowCreateDialog ref="createFlowDialog" v-model="newFlowName" @confirm="createFlow" />

    <AppDialog ref="importDialog" content-label="Import flow">
      <section class="il-import" aria-labelledby="il-import-title">
        <div>
          <h2 id="il-import-title">Import flow</h2>
          <p>Import an editable JSON flow or recover a compiled Flow IL artifact.</p>
        </div>
        <div class="il-import-controls import-type-control">
          <label for="flow-import-type">Import type</label>
          <select id="flow-import-type" v-model="importType">
            <option value="json">Flow JSON</option>
            <option value="il">Compiled Flow IL</option>
          </select>
        </div>
        <div v-if="importType === 'json'" class="il-import-controls">
          <label for="flow-json-file">Flow JSON file</label>
          <input
            id="flow-json-file"
            :key="importInputKey"
            type="file"
            accept=".json,.flow.json,application/json"
            @change="selectFlowFile"
          />
          <div class="import-dialog-actions">
            <AppButton
              text="Import"
              :icon="importIcon"
              :disabled="!selectedFlowFile || importing"
              @click="importFlow"
            />
            <AppButton text="Cancel" :icon="cancelIcon" @click="cancelImportDialog" />
          </div>
        </div>
        <div v-else class="il-import-controls">
          <label for="flow-il-file">Flow IL artifact</label>
          <input
            id="flow-il-file"
            :key="importInputKey"
            type="file"
            accept=".bin,.fil,application/octet-stream"
            @change="selectILArtifact"
          />
          <label for="flow-il-name">Recovered flow name</label>
          <input
            id="flow-il-name"
            v-model="importName"
            type="text"
            placeholder="Use artifact flow ID"
          />
          <AppButton
            :text="importing ? 'Validating…' : 'Preview recovery'"
            :icon="previewIcon"
            :disabled="!importArtifact || importing"
            @click="previewIL"
          />
          <AppButton text="Cancel" :icon="cancelIcon" @click="cancelImportDialog" />
        </div>
        <div v-if="importPreview" class="il-import-preview" role="status">
          <p>
            <strong>{{ importPreview.flow.name }}</strong> —
            {{ importPreview.flow.nodes.length }} nodes,
            {{ importPreview.flow.connections.length }} connections ({{
              importPreview.recoveryLevel
            }}
            recovery)
          </p>
          <ul v-if="importPreview.warnings.length">
            <li v-for="warning in importPreview.warnings" :key="warning">{{ warning }}</li>
          </ul>
          <AppButton
            text="Save as new editable flow"
            :icon="saveIcon"
            :disabled="importing"
            @click="saveILImport"
          />
        </div>
      </section>
    </AppDialog>

    <AppPromptDialog
      id="overwrite-flow-dialog"
      ref="overwriteFlowDialog"
      content-label="Overwrite existing flow"
      title="Overwrite existing flow?"
      :message="overwriteConfirmationMessage"
      cancel-text="Cancel"
      confirm-text="Yes"
      @cancel="cancelOverwrite"
      @confirm="confirmOverwrite"
    />

    <AppPromptDialog
      id="delete-flow-dialog"
      ref="deleteFlowDialog"
      content-label="Delete flow"
      title="Delete flow?"
      :message="deleteConfirmationMessage"
      cancel-text="Cancel"
      confirm-text="Confirm delete"
      @cancel="closeDeleteConfirmation"
      @confirm="confirmDeleteFlow"
    />
  </section>
</template>

<script setup lang="ts">
import { storeToRefs } from 'pinia';
import { computed, nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';

import previewIcon from '@/assets/icons/visibility-icon.svg';
import saveIcon from '@/assets/icons/save-icon.svg';
import cancelIcon from '@/assets/icons/cancel-icon.svg';
import importIcon from '@/assets/icons/import-icon.svg';
import AppButton from '@/components/AppButton.vue';
import AppDialog from '@/components/AppDialog.vue';
import AppErrorNotice from '@/components/AppErrorNotice.vue';
import AppPromptDialog from '@/components/AppPromptDialog.vue';
import AppFlowCreateDialog from '@/features/flows/components/AppFlowCreateDialog.vue';
import { type MultiSelectOption } from '@/components/AppMultiSelectDropdown.vue';
import { useServerPagination } from '@/composables/useServerPagination';
import { EVENTS } from '@/constants/events';
import {
  FlowApiError,
  flowApi,
  type FlowIlImportResult,
  type FlowListParameters
} from '@/features/flows/api/flowApi';
import AppFlowTable from '@/features/flows/components/AppFlowTable.vue';
import { useFlowsStore } from '@/features/flows/stores/flows';
import { useSpinner } from '@/composables/useSpinner';
import { FlowDtoValidationError } from '@/features/flows/api/flowDto';
import { flowExportFilename, parseFlowExport } from '@/features/flows/flowTransfer';

const route = useRoute();
const router = useRouter();
const flowStore = useFlowsStore();
const { flows } = storeToRefs(flowStore);
const error = ref<string>();
const errorRetry = ref(false);
const newFlowName = ref('');
const creating = ref(false);
const importArtifact = ref('');
const importName = ref('');
const importPreview = ref<FlowIlImportResult>();
const importing = ref(false);
const importType = ref<'json' | 'il'>('json');
const selectedFlowFile = ref<File>();
const importInputKey = ref(0);
const pendingImportDocument = ref<string>();
const overwriteConfirmationMessage = ref('');
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

const statusFilters = ref<Array<'draft' | 'deployed'>>(
  validRequestedStatuses.length > 0
    ? validRequestedStatuses
    : statusOptions.map(({ value }) => value as 'draft' | 'deployed')
);

const { query, page, pageSize, sortDirection, totalItems, toggleSortDirection, applyPageMetadata } =
  useServerPagination({
    initialQuery: queryValue(route.query.filter),
    initialPage: positiveInteger(route.query.page, 1),
    initialPageSize,
    initialSortDirection
  });

const updatePage = (nextPage: number): void => {
  page.value = nextPage;
};

const updatePageSize = (nextPageSize: number): void => {
  pageSize.value = nextPageSize;
};

const hasActiveFilters = computed(
  () =>
    query.value.trim().length > 0 ||
    !statusOptions.every(({ value }) => statusFilters.value.includes(value as 'draft' | 'deployed'))
);

const items = computed(() => flows.value);

const createFlowDialog = ref<InstanceType<typeof AppFlowCreateDialog>>();
const importDialog = ref<InstanceType<typeof AppDialog>>();
const deleteFlowDialog = ref<InstanceType<typeof AppPromptDialog>>();
const overwriteFlowDialog = ref<InstanceType<typeof AppPromptDialog>>();

const deleteConfirmationMessage = computed(() => {
  const flow = flows.value.find(({ id }) => id === confirmingDeleteId.value);
  return flow ? `Delete “${flow.name}”? This action cannot be undone.` : 'Delete this flow?';
});

const showCreateFlowDialog = async (): Promise<void> => {
  await nextTick();
  createFlowDialog.value?.showModal();
};

const showImportDialog = async (): Promise<void> => {
  await nextTick();
  importDialog.value?.showModal();
};

const resetImportDialog = (): void => {
  selectedFlowFile.value = undefined;
  importArtifact.value = '';
  importName.value = '';
  importPreview.value = undefined;
  importInputKey.value += 1;
};

const cancelImportDialog = (): void => {
  importDialog.value?.close();
  resetImportDialog();
};

const selectFlowFile = (event: Event): void => {
  selectedFlowFile.value = (event.target as HTMLInputElement).files?.[0];
};

const exportFlow = async (flowId: string): Promise<void> => {
  const flow = flowStore.flowPayload(flowId);
  if (!flow) {
    error.value = 'Unable to find the flow to export.';
    return;
  }

  try {
    const exported = await flowApi.exportFlow(flowId);
    const url = URL.createObjectURL(new Blob([exported], { type: 'application/json' }));
    const link = document.createElement('a');
    link.href = url;
    link.download = flowExportFilename(flow.name);
    link.click();
    URL.revokeObjectURL(url);
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to export the flow.';
  }
};

const saveFlowImport = async (document: string, overwrite = false): Promise<void> => {
  try {
    await withSpinner(
      () => {
        importing.value = true;
        error.value = undefined;
        errorRetry.value = false;
      },
      () => flowApi.importFlow(document, overwrite),
      async (saved) => {
        pendingImportDocument.value = undefined;
        flowStore.replaceFlowFromPayload(saved);
        await router.push({ name: 'flow-designer', params: { flowId: saved.id } });
      }
    );
  } catch (caught) {
    if (!overwrite && caught instanceof FlowApiError && caught.status === 409) {
      pendingImportDocument.value = document;
      overwriteConfirmationMessage.value = `${caught.message.replace(/^Flow request failed:\s*/, '')} Do you want to overwrite it?`;
      await nextTick();
      overwriteFlowDialog.value?.showModal();
      return;
    }
    error.value =
      caught instanceof FlowDtoValidationError
        ? `The flow JSON is invalid: ${caught.message}`
        : caught instanceof Error
          ? caught.message
          : 'Unable to import the flow.';
  } finally {
    importing.value = false;
  }
};

const importFlow = async (): Promise<void> => {
  const file = selectedFlowFile.value;
  if (!file) return;
  importDialog.value?.close();

  try {
    const document = await file.text();
    parseFlowExport(document);
    selectedFlowFile.value = undefined;
    importInputKey.value += 1;
    await saveFlowImport(document);
  } catch (caught) {
    error.value =
      caught instanceof FlowDtoValidationError
        ? `The flow JSON is invalid: ${caught.message}`
        : caught instanceof Error
          ? caught.message
          : 'Unable to import the flow.';
  }
};

const cancelOverwrite = (): void => {
  pendingImportDocument.value = undefined;
  overwriteConfirmationMessage.value = '';
};

const confirmOverwrite = (): void => {
  const document = pendingImportDocument.value;
  if (document) void saveFlowImport(document, true);
};

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

const { isSpinnerVisible, withSpinner } = useSpinner();

const loadFlows = async (): Promise<void> => {
  const controller = new AbortController();

  try {
    await withSpinner(
      () => {
        listController?.abort();
        listController = controller;
        error.value = undefined;
        errorRetry.value = false;
      },
      () =>
        flowApi.listFlows(
          {
            filter: query.value.trim(),
            statuses: statusFilters.value.filter(
              (status): status is 'draft' | 'deployed' =>
                status === 'draft' || status === 'deployed'
            ),
            page: page.value,
            pageSize: pageSize.value,
            sort: sortDirection.value
          },
          controller.signal
        ),
      (result) => {
        if (listController === controller) {
          flowStore.replaceAllFlowsFromPayloads(result.items);
          applyPageMetadata(result);
        }
      }
    );
  } catch (caught) {
    if (listController === controller) {
      error.value = caught instanceof Error ? caught.message : 'Unable to load flows.';
      errorRetry.value = true;
    }
  } finally {
    if (listController === controller) {
      listController = undefined;
    }
  }
};

const createFlow = async (): Promise<void> => {
  const name = newFlowName.value.trim();

  if (!name) {
    error.value = 'Enter a name for the new flow.';
    return;
  }

  try {
    await withSpinner(
      () => {
        errorRetry.value = false;
        creating.value = true;
        error.value = undefined;
      },
      () => flowApi.createFlow(name),
      async (createdFlow) => {
        newFlowName.value = '';
        await router.push({
          name: 'flow-designer',
          params: { flowId: createdFlow.id }
        });
      }
    );
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to create the flow.';
  } finally {
    creating.value = false;
  }
};

const selectILArtifact = async (event: Event): Promise<void> => {
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

const previewIL = async (): Promise<void> => {
  if (!importArtifact.value) return;

  try {
    await withSpinner(
      () => {
        importing.value = true;
        error.value = undefined;
      },
      () => flowApi.importFlowIl(importArtifact.value, importName.value.trim() || undefined, false),
      (result) => {
        importPreview.value = result;
      }
    );
  } catch (caught) {
    error.value =
      caught instanceof Error ? caught.message : 'Unable to preview the Flow IL artifact.';
  } finally {
    importing.value = false;
  }
};

const saveILImport = async (): Promise<void> => {
  if (!importArtifact.value || !importPreview.value) return;

  try {
    await withSpinner(
      () => {
        importing.value = true;
        error.value = undefined;
      },
      () =>
        flowApi.importFlowIl(
          importArtifact.value,
          importName.value.trim() || importPreview.value!.flow.name,
          true
        ),
      async (result) => {
        await router.push({ name: 'flow-designer', params: { flowId: result.flow.id } });
      }
    );
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

const beginDelete = async (flowId: string): Promise<void> => {
  confirmingDeleteId.value = flowId;
  await nextTick();
  deleteFlowDialog.value?.showModal();
};

const confirmDeleteFlow = (): void => {
  if (confirmingDeleteId.value) void deleteFlow(confirmingDeleteId.value);
};

const renameFlow = async (flowId: string): Promise<void> => {
  const payload = flowStore.flowPayload(flowId);
  const name = renameValue.value.trim();

  if (!payload || !name) {
    error.value = 'Flow name is required.';
    return;
  }

  try {
    await withSpinner(
      () => {
        errorRetry.value = false;
        renaming.value = true;
        error.value = undefined;
      },
      () => flowApi.saveFlow({ ...payload, name }),
      async () => {
        editingFlowId.value = undefined;
        await loadFlows();
      }
    );
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to rename the flow.';
  } finally {
    renaming.value = false;
  }
};

const deleteFlow = async (flowId: string): Promise<void> => {
  try {
    await withSpinner(
      () => {
        errorRetry.value = false;
        deleting.value = true;
        error.value = undefined;
      },
      () => flowApi.deleteFlow(flowId),
      async () => {
        closeDeleteConfirmation();
        await loadFlows();
      }
    );
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to delete the flow.';
  } finally {
    deleting.value = false;
  }
};

const setFlowDisabled = async (flowId: string, disabled: boolean): Promise<void> => {
  try {
    await withSpinner(
      () => {
        errorRetry.value = false;
        togglingDisabledId.value = flowId;
        error.value = undefined;
      },
      () => flowApi.setFlowDisabled(flowId, disabled),
      (saved) => {
        flowStore.replaceFlowFromPayload(saved);
      }
    );
  } catch (caught) {
    error.value =
      caught instanceof Error ? caught.message : 'Unable to change the flow execution state.';
  } finally {
    togglingDisabledId.value = undefined;
  }
};

const reenableFlow = async (flowId: string): Promise<void> => {
  try {
    await withSpinner(
      () => {
        errorRetry.value = false;
        togglingDisabledId.value = flowId;
        error.value = undefined;
      },
      () => flowApi.reenableFlow(flowId),
      (saved) => {
        flowStore.replaceFlowFromPayload(saved);
      }
    );
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to re-enable the flow.';
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
.il-import-controls select,
.il-import button {
  min-height: var(--control-min-height);
}

.il-import-controls select {
  padding: var(--space-3) var(--space-4);
  color: var(--color-text-primary);
  background: var(--color-surface-raised);
  border: var(--border-width-default) solid var(--color-border-default);
  border-radius: var(--radius-md);
}

.il-import-preview {
  padding-top: var(--space-5);
  border-top: var(--border-width-default) solid var(--color-border-default);
}

.import-dialog-actions {
  display: flex;
  gap: var(--space-3-5);
  width: 100%;
  justify-content: flex-end;
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

.accessible-input::-webkit-search-cancel-button {
  font-size: 1.8rem;
}

/* Mobile breakpoint (40rem): stacks page and navigation content for phone layouts. */
@media (max-width: 40rem) {
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
