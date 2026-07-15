<template>
  <section class="designer-page">
    <p v-if="loading" class="request-status" role="status">Loading latest flow…</p>
    <p v-if="loadError" class="request-error" role="alert">{{ loadError }}</p>
    <p v-if="saveError" class="request-error" role="alert">{{ saveError }}</p>
    <p v-if="runtimeError" class="request-error" role="alert">{{ runtimeError }}</p>
    <div v-if="showDeployConfirmation" class="dialog-backdrop">
      <section
        ref="deployDialog"
        class="discard-dialog"
        role="alertdialog"
        aria-labelledby="deploy-title"
        aria-describedby="deploy-description"
        aria-modal="true"
        tabindex="-1"
        @keydown="handleDeployDialogKeydown"
      >
        <h2 id="deploy-title">Deploy this flow?</h2>
        <p id="deploy-description">
          The latest saved definition will replace the currently running version.
        </p>
        <div>
          <button type="button" data-dialog-initial-focus @click="closeDeployConfirmation">
            Cancel
          </button>
          <button type="button" class="deploy-confirm" @click="deployFlow">Deploy now</button>
        </div>
      </section>
    </div>
    <div v-if="pendingRoute" class="dialog-backdrop">
      <section
        ref="discardDialog"
        class="discard-dialog"
        role="alertdialog"
        aria-labelledby="discard-title"
        aria-describedby="discard-description"
        aria-modal="true"
        tabindex="-1"
        @keydown="handleDiscardDialogKeydown"
      >
        <h2 id="discard-title">Discard unsaved changes?</h2>
        <p id="discard-description">This flow has changes that have not been saved.</p>
        <div>
          <button type="button" data-dialog-initial-focus @click="keepEditing">Keep editing</button>
          <button type="button" @click="discardChanges">Discard changes</button>
        </div>
      </section>
    </div>
    <template v-if="flow">
      <div class="designer-heading">
        <div>
          <RouterLink :to="{ name: 'flows' }">← All flows</RouterLink>
          <div class="title-row">
            <h1>{{ flow.name }}</h1>
            <span :class="flow.status">{{ flow.status }}</span>
            <span v-if="dirty" class="dirty-state" role="status">Unsaved changes</span>
            <span
              class="runtime-state"
              role="status"
              :aria-label="`Runtime state: ${runtime?.state ?? 'disconnected'}`"
            >
              {{ runtime?.state ?? 'disconnected' }}
            </span>
          </div>
          <p>{{ flow.description }}</p>
        </div>
        <div class="heading-actions">
          <button type="button" :disabled="saving" @click="saveFlow">
            {{ saving ? 'Saving…' : 'Save flow' }}
          </button>
          <button
            type="button"
            :disabled="dirty || deploying"
            @click="showDeployConfirmation = true"
          >
            {{ deploying ? 'Deploying…' : 'Deploy flow' }}
          </button>
          <button type="button" @click="refreshRuntime()">Refresh runtime</button>
        </div>
      </div>

      <FlowDesignerCanvas
        :flow="flow"
        :runtime="runtime"
        @move-node="moveNode"
        @reorder-node="reorderNode"
        @delete-node="deleteNode"
        @add-connection="addConnection"
        @delete-connection="deleteConnection"
        @add-node="addNode"
        @update-node-label="updateNodeLabel"
        @update-node-configuration="updateNodeConfiguration"
      />
    </template>

    <div v-else-if="!loading" class="not-found">
      <p class="eyebrow">Flow not found</p>
      <h1>There is no flow named “{{ flowId }}”.</h1>
      <RouterLink :to="{ name: 'flows' }">Return to flows</RouterLink>
    </div>
  </section>
</template>

<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue';
import { onBeforeRouteLeave, useRouter } from 'vue-router';

import FlowDesignerCanvas from '@/features/flows/components/FlowDesignerCanvas.vue';
import { useFlowsStore } from '@/features/flows/stores/flows';
import type { ZOrderCommand } from '@/features/flows/graph/zOrder';
import { FlowApiError, flowApi } from '@/features/flows/api/flowApi';
import { flowRuntimeApi } from '@/features/flows/api/flowRuntimeApi';
import { createLatestRequestGuard } from '@/features/flows/api/latestRequest';
import { useFlowRuntimeStore } from '@/features/flows/stores/flowRuntime';
import { useModalFocus } from '@/features/flows/composables/useModalFocus';
import type {
  FlowConfigurationValue,
  FlowConnectionEndpoint,
  FlowNode
} from '@/features/flows/types';

const props = defineProps<{
  flowId: string;
}>();

const flowStore = useFlowsStore();
const runtimeStore = useFlowRuntimeStore();
const router = useRouter();
const flow = computed(() => flowStore.findFlow(props.flowId));
const dirty = computed(() => flowStore.isFlowDirty(props.flowId));
const loading = ref(false);
const saving = ref(false);
const loadError = ref<string>();
const saveError = ref<string>();
const runtimeError = ref<string>();
const showDeployConfirmation = ref(false);
const deployDialog = ref<HTMLElement>();
const discardDialog = ref<HTMLElement>();
const runtime = computed(() => runtimeStore.snapshotFor(props.flowId));
const deploying = computed(() => runtimeStore.isDeploying(props.flowId));
let loadController: AbortController | undefined;
const loadGuard = createLatestRequestGuard();
const pendingRoute = ref<string>();
let allowNavigation = false;

const closeDeployConfirmation = (): void => {
  showDeployConfirmation.value = false;
};
const { handleKeydown: handleDeployDialogKeydown } = useModalFocus(
  deployDialog,
  showDeployConfirmation,
  closeDeployConfirmation
);

const moveNode = (nodeId: string, x: number, y: number): void => {
  flowStore.moveNode(props.flowId, nodeId, x, y);
};

const reorderNode = (nodeId: string, command: ZOrderCommand): void => {
  flowStore.reorderNode(props.flowId, nodeId, command);
};

const deleteNode = (nodeId: string): void => {
  flowStore.deleteNode(props.flowId, nodeId);
};

const addConnection = (start: FlowConnectionEndpoint, end: FlowConnectionEndpoint): void => {
  flowStore.connectNodes(props.flowId, start, end);
};

const deleteConnection = (connectionId: string): void => {
  flowStore.deleteConnection(props.flowId, connectionId);
};

const addNode = (node: FlowNode): void => {
  flowStore.addNode(props.flowId, node);
};

const updateNodeLabel = (nodeId: string, label: string): void => {
  flowStore.updateNodeLabel(props.flowId, nodeId, label);
};

const updateNodeConfiguration = (
  nodeId: string,
  key: string,
  value: FlowConfigurationValue
): void => {
  flowStore.updateNodeConfiguration(props.flowId, nodeId, key, value);
};

const loadFlow = async (flowId: string): Promise<void> => {
  // Route parameters can change before a request finishes. Abort the old fetch
  // and also use a generation guard so a late response cannot replace the new flow.
  loadController?.abort();
  const controller = new AbortController();
  const requestGeneration = loadGuard.begin();
  loadController = controller;
  loading.value = true;
  loadError.value = undefined;
  flowStore.selectFlow(flowId);
  try {
    const payload = await flowApi.getFlow(flowId, controller.signal);
    if (!loadGuard.isCurrent(requestGeneration)) return;
    flowStore.replaceFlowFromPayload(payload);
    flowStore.selectFlow(flowId);
    void refreshRuntime(flowId);
  } catch (error) {
    if (
      !loadGuard.isCurrent(requestGeneration) ||
      (error instanceof FlowApiError && error.kind === 'cancelled')
    )
      return;
    loadError.value = error instanceof Error ? error.message : 'Unable to load this flow.';
  } finally {
    if (loadController === controller) loading.value = false;
  }
};

const refreshRuntime = async (flowId = props.flowId): Promise<void> => {
  runtimeError.value = undefined;
  try {
    const snapshot = await flowRuntimeApi.getRuntime(flowId);
    // Runtime responses are scoped to a route ID. Reject a mismatched snapshot
    // instead of displaying another flow's state after a proxy or cache error.
    if (snapshot.flowId !== flowId) throw new Error('Runtime state belongs to another flow.');
    runtimeStore.applySnapshot(snapshot);
  } catch (error) {
    runtimeStore.disconnect(flowId);
    runtimeError.value = error instanceof Error ? error.message : 'Unable to load runtime state.';
  }
};

const deployFlow = async (): Promise<void> => {
  showDeployConfirmation.value = false;
  runtimeStore.beginDeployment(props.flowId);
  runtimeError.value = undefined;
  try {
    const snapshot = await flowRuntimeApi.deployFlow(props.flowId);
    if (snapshot.flowId !== props.flowId) throw new Error('Runtime state belongs to another flow.');
    runtimeStore.completeDeployment(snapshot);
  } catch (error) {
    const message = error instanceof Error ? error.message : 'Unable to deploy this flow.';
    runtimeStore.failDeployment(props.flowId, message);
    runtimeError.value = message;
  }
};

const saveFlow = async (): Promise<void> => {
  const payload = flowStore.flowPayload(props.flowId);
  if (!payload) return;
  saving.value = true;
  saveError.value = undefined;
  try {
    const saved = await flowApi.saveFlow(payload);
    // Replace from the server response, rather than assuming the submitted DTO is
    // final; the backend may normalize fields or update its timestamp.
    flowStore.replaceFlowFromPayload(saved);
  } catch (error) {
    saveError.value = error instanceof Error ? error.message : 'Unable to save this flow.';
  } finally {
    saving.value = false;
  }
};

watch(
  () => props.flowId,
  (flowId) => void loadFlow(flowId),
  { immediate: true }
);
onBeforeUnmount(() => {
  loadGuard.invalidate();
  loadController?.abort();
});

const handleBeforeUnload = (event: BeforeUnloadEvent): void => {
  if (!dirty.value) return;
  // Browsers show their own confirmation wording for tab close and page refresh.
  // Setting returnValue is still required by browsers that support this prompt.
  event.preventDefault();
  event.returnValue = '';
};

const keepEditing = (): void => {
  pendingRoute.value = undefined;
};
const discardDialogOpen = computed(() => Boolean(pendingRoute.value));
const { handleKeydown: handleDiscardDialogKeydown } = useModalFocus(
  discardDialog,
  discardDialogOpen,
  keepEditing
);

const discardChanges = async (): Promise<void> => {
  const target = pendingRoute.value;
  if (!target) return;
  // Restore the last server-confirmed graph before allowing the blocked route to
  // continue, so the discarded draft cannot reappear from shared store state.
  flowStore.resetFlow(props.flowId);
  pendingRoute.value = undefined;
  allowNavigation = true;
  await router.push(target);
};

onBeforeRouteLeave((to) => {
  // Client-side routing does not trigger beforeunload, so it needs a separate
  // guard and an application-owned dialog that can keep or discard the draft.
  if (allowNavigation || !dirty.value) return true;
  pendingRoute.value = to.fullPath;
  return false;
});
onMounted(() => window.addEventListener('beforeunload', handleBeforeUnload));
onBeforeUnmount(() => window.removeEventListener('beforeunload', handleBeforeUnload));
</script>

<style scoped>
.designer-page {
  width: min(1280px, calc(100% - 40px));
  margin: 0 auto;
  padding: 34px 0 60px;
}

.designer-heading {
  display: flex;
  gap: 28px;
  align-items: end;
  justify-content: space-between;
  margin-bottom: 24px;
}

.designer-heading a,
.not-found a {
  color: #0b7568;
  font-size: 13px;
  font-weight: 700;
  text-decoration: none;
}

.title-row {
  display: flex;
  gap: 12px;
  align-items: center;
  margin-top: 9px;
}

h1 {
  margin: 0;
  color: #102133;
  font-size: clamp(28px, 4vw, 38px);
  letter-spacing: -0.035em;
}

.title-row span {
  padding: 5px 8px;
  color: #687784;
  font-size: 10px;
  font-weight: 800;
  letter-spacing: 0.08em;
  background: #e5eaee;
  border-radius: 999px;
  text-transform: uppercase;
}

.title-row span.deployed {
  color: #0b655b;
  background: #dff6ee;
}

.title-row .dirty-state {
  color: #8a4b16;
  background: #fff0d8;
}

.title-row .runtime-state {
  color: #31566e;
  background: #e9f4fa;
}

.designer-heading p {
  margin: 7px 0 0;
  color: #627587;
  font-size: 14px;
}

button {
  padding: 11px 16px;
  color: #76909d;
  font-weight: 700;
  background: #e5ecef;
  border: 0;
  border-radius: 9px;
}

.heading-actions {
  display: flex;
  gap: 8px;
}

.heading-actions button:first-child:not(:disabled) {
  color: #fff;
  background: #0b7568;
  cursor: pointer;
}

.heading-actions button:not(:disabled) {
  cursor: pointer;
}

.heading-actions button:nth-child(2):not(:disabled),
.deploy-confirm {
  color: #fff;
  background: #0b7568;
}

.request-status,
.request-error {
  margin: 0 0 12px;
  padding: 10px 12px;
  border-radius: 8px;
}

.dialog-backdrop {
  position: fixed;
  z-index: 20;
  display: grid;
  inset: 0;
  place-items: center;
  padding: 20px;
  background: rgb(16 33 51 / 45%);
}

.discard-dialog {
  width: min(430px, 100%);
  padding: 24px;
  background: #fff;
  border-radius: 12px;
  box-shadow: 0 20px 60px rgb(16 33 51 / 25%);
}

.discard-dialog h2 {
  margin: 0;
  color: #102133;
}

.discard-dialog p {
  color: #627587;
}

.discard-dialog > div {
  display: flex;
  gap: 8px;
  justify-content: end;
}

.discard-dialog button:last-child {
  color: #fff;
  background: #a13928;
}

.request-status {
  color: #31566e;
  background: #e9f4fa;
}

.request-error {
  color: #8e3021;
  background: #fbe9e5;
}

.not-found {
  padding: 80px 0;
}

.eyebrow {
  margin: 0 0 10px;
  color: #a64f38;
  font-size: 11px;
  font-weight: 800;
  letter-spacing: 0.13em;
  text-transform: uppercase;
}

.not-found h1 {
  margin-bottom: 24px;
}

@media (max-width: 760px) {
  .designer-page {
    width: calc(100% - 28px);
    overflow-x: hidden;
  }

  .designer-heading {
    align-items: start;
    flex-direction: column;
  }
}
</style>
