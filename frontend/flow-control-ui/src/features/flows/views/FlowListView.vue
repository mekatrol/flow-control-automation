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

    <div v-if="!loading && !error && flows.length === 0" class="empty-state">
      <h2>No flows yet</h2>
      <p>Create a flow to start designing an automation.</p>
    </div>

    <div class="flow-grid">
      <article v-for="flow in flows" :key="flow.id" class="flow-card">
        <div class="flow-card-heading">
          <span class="status" :class="flow.status">{{ flow.status }}</span>
          <div class="card-actions">
            <AppButton
              text="Rename"
              :icon="renameFlowIcon"
              @click="beginRename(flow.id, flow.name)"
            />
            <AppButton text="Delete" :icon="deleteFlowIcon" @click="beginDelete(flow.id)" />
          </div>
        </div>
        <form
          v-if="editingFlowId === flow.id"
          class="rename-flow"
          @submit.prevent="renameFlow(flow.id)"
        >
          <label :for="`rename-${flow.id}`">Rename {{ flow.name }}</label>
          <input :id="`rename-${flow.id}`" v-model="renameValue" type="text" />
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
            @click="editingFlowId = undefined"
          />
        </form>
        <h2 v-else>
          <RouterLink :to="{ name: 'flow-designer', params: { flowId: flow.id } }">
            {{ flow.name }} <span aria-hidden="true">→</span>
          </RouterLink>
        </h2>
        <p>{{ flow.description }}</p>
        <dl>
          <div>
            <dt>Nodes</dt>
            <dd>{{ flow.nodes.length }}</dd>
          </div>
          <div>
            <dt>Updated</dt>
            <dd>{{ formatUpdatedAt(flow.updatedAt) }}</dd>
          </div>
        </dl>
        <div
          v-if="confirmingDeleteId === flow.id"
          :ref="setDeleteDialog"
          class="delete-confirmation"
          role="alertdialog"
          :aria-label="`Delete ${flow.name}?`"
          :aria-describedby="`delete-description-${flow.id}`"
          aria-modal="true"
          tabindex="-1"
          @keydown="handleDeleteDialogKeydown"
        >
          <span :id="`delete-description-${flow.id}`">Delete this flow?</span>
          <AppButton
            text="Confirm delete"
            :icon="deleteFlowIcon"
            :disabled="deleting"
            @click="deleteFlow(flow.id)"
          />
          <AppButton
            text="Cancel"
            :icon="cancelIcon"
            data-dialog-initial-focus
            @click="closeDeleteConfirmation"
          />
        </div>
      </article>
    </div>
  </section>
</template>

<script setup lang="ts">
import { storeToRefs } from 'pinia';
import { computed, onBeforeUnmount, onMounted, ref, type ComponentPublicInstance } from 'vue';

import deleteFlowIcon from '@/assets/delete-flow-icon.svg';
import cancelIcon from '@/assets/cancel-icon.svg';
import newFlowIcon from '@/assets/new-flow-icon.svg';
import renameFlowIcon from '@/assets/rename-flow-icon.svg';
import retryIcon from '@/assets/retry-icon.svg';
import saveIcon from '@/assets/save-icon.svg';
import AppButton from '@/components/AppButton.vue';
import { flowApi } from '@/features/flows/api/flowApi';
import { useFlowsStore } from '@/features/flows/stores/flows';
import { useModalFocus } from '@/features/flows/composables/useModalFocus';

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
const deleteDialog = ref<HTMLElement>();
let listController: AbortController | undefined;

const setDeleteDialog = (element: Element | ComponentPublicInstance | null): void => {
  deleteDialog.value = element instanceof HTMLElement ? element : undefined;
};

const closeDeleteConfirmation = (): void => {
  confirmingDeleteId.value = undefined;
};
const deleteDialogOpen = computed(() => Boolean(confirmingDeleteId.value));
const { handleKeydown: handleDeleteDialogKeydown } = useModalFocus(
  deleteDialog,
  deleteDialogOpen,
  closeDeleteConfirmation
);

const loadFlows = async (): Promise<void> => {
  // A retry can overlap the previous request. Only the controller still owned by
  // this view may replace data or clear the loading indicator.
  listController?.abort();
  const controller = new AbortController();
  listController = controller;
  loading.value = true;
  error.value = undefined;
  try {
    const payloads = await flowApi.listFlows(controller.signal);
    if (listController === controller) flowStore.replaceAllFlowsFromPayloads(payloads);
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
    flowStore.replaceFlowFromPayload(await flowApi.createFlow(name));
    newFlowName.value = '';
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
    // Saving the complete flow preserves graph data while changing its name, and
    // the returned DTO becomes the new confirmed dirty-state baseline.
    flowStore.replaceFlowFromPayload(await flowApi.saveFlow({ ...payload, name }));
    editingFlowId.value = undefined;
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
    // Keep the card visible if the server rejects deletion. Local state changes
    // only after the backend confirms that the flow is gone.
    flowStore.removeConfirmedFlow(flowId);
    closeDeleteConfirmation();
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to delete the flow.';
  } finally {
    deleting.value = false;
  }
};

const formatUpdatedAt = (updatedAt: string): string =>
  // Use the browser's locale so dates follow the user's regional ordering and
  // clock conventions instead of imposing a fixed presentation format.
  new Intl.DateTimeFormat(undefined, {
    dateStyle: 'medium',
    timeStyle: 'short'
  }).format(new Date(updatedAt));

onMounted(() => void loadFlows());
onBeforeUnmount(() => listController?.abort());
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

button {
  display: inline-flex;
  gap: 7px;
  align-items: center;
  justify-content: center;
  padding: 11px 16px;
  color: var(--color-text-secondary);
  font-weight: 700;
  background: var(--color-surface-neutral);
  border: 1px solid var(--color-text-muted);
  border-radius: 9px;
}

button:not(:disabled) {
  cursor: pointer;
}

button:not(:disabled):hover {
  color: var(--color-action-primary-strong);
  background: var(--color-action-primary-surface);
  border-color: var(--color-action-primary);
}

button:disabled {
  color: var(--color-text-muted);
  cursor: not-allowed;
  background: var(--color-surface-disabled);
  border-color: var(--color-text-muted);
  border-style: dashed;
}

.create-flow button:not(:disabled),
.rename-flow button[type='submit']:not(:disabled) {
  color: var(--color-text-on-primary);
  background: var(--color-action-primary);
  border-color: var(--color-action-primary);
}

.create-flow button:not(:disabled):hover,
.rename-flow button[type='submit']:not(:disabled):hover {
  background: var(--color-action-primary-strong);
  border-color: var(--color-action-primary-strong);
}

.delete-confirmation button:first-of-type:not(:disabled) {
  color: var(--color-text-on-strong);
  background: var(--color-danger-strong);
  border-color: var(--color-danger-strong);
}

.create-flow,
.rename-flow,
.card-actions,
.delete-confirmation,
.request-error {
  display: flex;
  gap: 8px;
  align-items: center;
}

.create-flow label,
.rename-flow label {
  color: var(--color-text-primary);
  font-size: 11px;
  font-weight: 700;
}

.create-flow input,
.rename-flow input {
  min-width: 180px;
  padding: 9px;
  color: var(--color-text-secondary);
  background: var(--color-surface-raised);
  border: 1px solid var(--color-text-muted);
  border-radius: 7px;
}

.create-flow input::placeholder,
.rename-flow input::placeholder {
  color: var(--color-text-muted);
  opacity: 1;
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

.flow-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(300px, 1fr));
  gap: 18px;
}

.flow-card {
  padding: 22px;
  color: inherit;
  background: var(--color-surface-raised);
  border: 1px solid var(--color-border-faint);
  border-radius: 14px;
  box-shadow: 0 12px 30px var(--color-shadow-card);
  transition:
    border-color 160ms ease,
    transform 160ms ease,
    box-shadow 160ms ease;
}

.flow-card:hover {
  border-color: var(--color-action-primary);
  box-shadow: 0 18px 38px var(--color-shadow-card-hover);
  outline: none;
  transform: translateY(-2px);
}

.flow-card-heading {
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.status {
  padding: 5px 8px;
  color: var(--color-text-secondary);
  font-size: 10px;
  font-weight: 800;
  letter-spacing: 0.08em;
  background: var(--color-surface-neutral);
  border-radius: 999px;
  text-transform: uppercase;
}

.status.deployed {
  color: var(--color-action-primary-strong);
  background: var(--color-action-primary-surface);
}

.delete-confirmation button,
.rename-flow button {
  padding: 6px 8px;
  font-size: 10px;
}

.card-actions button {
  gap: 8px;
  min-height: 40px;
  padding: 9px 12px;
  font-size: 13px;
}

h2 {
  margin: 22px 0 8px;
  color: var(--color-text-primary);
  font-size: 19px;
}

h2 a {
  color: inherit;
  text-decoration: none;
}

h2 a:hover,
h2 a:focus-visible {
  color: var(--color-action-primary);
}

.flow-card > p {
  min-height: 44px;
  margin: 0;
  color: var(--color-text-muted);
  font-size: 14px;
  line-height: 1.55;
}

.delete-confirmation {
  margin-top: 14px;
  padding-top: 12px;
  color: var(--color-danger-text);
  border-top: 1px solid var(--color-surface-neutral);
}

dl {
  display: flex;
  gap: 34px;
  margin: 24px 0 0;
  padding-top: 18px;
  border-top: 1px solid var(--color-surface-neutral);
}

dt {
  margin-bottom: 4px;
  color: var(--color-text-muted);
  font-size: 10px;
  font-weight: 750;
  letter-spacing: 0.08em;
  text-transform: uppercase;
}

dd {
  margin: 0;
  color: var(--color-text-secondary);
  font-size: 12px;
  font-weight: 650;
}

@media (max-width: 640px) {
  .flow-library {
    width: min(100% - 28px, 1180px);
    padding: 38px 0;
  }

  .page-heading {
    align-items: start;
  }

  .page-heading {
    flex-direction: column;
  }

  .create-flow {
    flex-wrap: wrap;
  }
}
</style>
