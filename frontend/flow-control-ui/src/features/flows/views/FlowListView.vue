<script setup lang="ts">
import { storeToRefs } from 'pinia';
import { onBeforeUnmount, onMounted, ref } from 'vue';

import { flowApi } from '../api/flowApi';
import { useFlowsStore } from '../stores/flows';

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
    confirmingDeleteId.value = undefined;
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
        <input id="new-flow-name" v-model="newFlowName" type="text" />
        <button type="submit" :disabled="creating">
          {{ creating ? 'Creating…' : 'New flow' }}
        </button>
      </form>
    </div>

    <p v-if="loading" class="request-status" role="status">Loading flows…</p>
    <div v-if="error" class="request-error" role="alert">
      <span>{{ error }}</span>
      <button type="button" @click="loadFlows">Retry</button>
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
            <button type="button" @click="beginRename(flow.id, flow.name)">Rename</button>
            <button type="button" @click="confirmingDeleteId = flow.id">Delete</button>
          </div>
        </div>
        <form
          v-if="editingFlowId === flow.id"
          class="rename-flow"
          @submit.prevent="renameFlow(flow.id)"
        >
          <label :for="`rename-${flow.id}`">Rename {{ flow.name }}</label>
          <input :id="`rename-${flow.id}`" v-model="renameValue" type="text" />
          <button type="submit" :disabled="renaming">Save name</button>
          <button type="button" @click="editingFlowId = undefined">Cancel</button>
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
          class="delete-confirmation"
          role="alertdialog"
          :aria-label="`Delete ${flow.name}?`"
        >
          <span>Delete this flow?</span>
          <button type="button" :disabled="deleting" @click="deleteFlow(flow.id)">
            Confirm delete
          </button>
          <button type="button" @click="confirmingDeleteId = undefined">Cancel</button>
        </div>
      </article>
    </div>
  </section>
</template>

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
  color: #0b7568;
  font-size: 11px;
  font-weight: 800;
  letter-spacing: 0.13em;
  text-transform: uppercase;
}

h1 {
  margin: 0;
  color: #102133;
  font-size: clamp(34px, 5vw, 52px);
  letter-spacing: -0.04em;
}

.page-heading p:last-child {
  max-width: 560px;
  margin: 10px 0 0;
  color: #627587;
}

button {
  padding: 11px 16px;
  color: #76909d;
  font-weight: 700;
  background: #e5ecef;
  border: 0;
  border-radius: 9px;
}

button:not(:disabled) {
  cursor: pointer;
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
  font-size: 11px;
  font-weight: 700;
}

.create-flow input,
.rename-flow input {
  min-width: 180px;
  padding: 9px;
  border: 1px solid #cbd8e2;
  border-radius: 7px;
}

.request-status,
.request-error,
.empty-state {
  margin-bottom: 22px;
  padding: 16px;
  border-radius: 10px;
}

.request-status {
  color: #31566e;
  background: #e9f4fa;
}

.request-error {
  justify-content: space-between;
  color: #8e3021;
  background: #fbe9e5;
}

.empty-state {
  color: #627587;
  background: #fff;
  border: 1px dashed #b9c8d3;
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
  background: #fff;
  border: 1px solid #dce5ec;
  border-radius: 14px;
  box-shadow: 0 12px 30px rgb(31 55 75 / 5%);
  transition:
    border-color 160ms ease,
    transform 160ms ease,
    box-shadow 160ms ease;
}

.flow-card:hover {
  border-color: #91b9b1;
  box-shadow: 0 18px 38px rgb(31 55 75 / 10%);
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
  color: #6b7680;
  font-size: 10px;
  font-weight: 800;
  letter-spacing: 0.08em;
  background: #edf1f4;
  border-radius: 999px;
  text-transform: uppercase;
}

.status.deployed {
  color: #0b655b;
  background: #dff6ee;
}

.card-actions button,
.delete-confirmation button,
.rename-flow button {
  padding: 6px 8px;
  font-size: 10px;
}

h2 {
  margin: 22px 0 8px;
  color: #102133;
  font-size: 19px;
}

h2 a {
  color: inherit;
  text-decoration: none;
}

h2 a:hover,
h2 a:focus-visible {
  color: #0b7568;
}

.flow-card > p {
  min-height: 44px;
  margin: 0;
  color: #627587;
  font-size: 14px;
  line-height: 1.55;
}

.delete-confirmation {
  margin-top: 14px;
  padding-top: 12px;
  color: #8e3021;
  border-top: 1px solid #edf1f4;
}

dl {
  display: flex;
  gap: 34px;
  margin: 24px 0 0;
  padding-top: 18px;
  border-top: 1px solid #edf1f4;
}

dt {
  margin-bottom: 4px;
  color: #8594a1;
  font-size: 10px;
  font-weight: 750;
  letter-spacing: 0.08em;
  text-transform: uppercase;
}

dd {
  margin: 0;
  color: #34495b;
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
