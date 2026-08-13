<template>
  <section v-bind="automation()" class="designer-page">
    <AppErrorNotice
      id="flow-designer-error-notice"
      automation="flow-designer-error"
      :message="noticeError"
    />
    <p v-if="loading" class="request-status" role="status">Loading latest flow…</p>
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
          <AppButton
            automation="flow-deploy-cancel"
            text="Cancel"
            :icon="cancelIcon"
            data-dialog-initial-focus
            @click="closeDeployConfirmation"
          />
          <AppButton
            automation="flow-deploy-confirm"
            text="Deploy now"
            :icon="deployIcon"
            @click="deployFlow"
          />
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
          <AppButton
            automation="flow-discard-keep-editing"
            text="Keep editing"
            :icon="renameFlowIcon"
            data-dialog-initial-focus
            @click="keepEditing"
          />
          <AppButton
            automation="flow-discard-confirm"
            text="Discard changes"
            :icon="discardIcon"
            @click="discardChanges"
          />
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
            <span v-if="flow.disabled" class="disabled">disabled</span>
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
          <AppFlowDebugTargetSelector
            v-model="debugTargetId"
            automation="flow-debug-target"
            :targets="debugTargets"
            :loading="controllerTemplates.loading"
            :error="controllerTemplates.error"
          />
          <AppButton
            automation="flow-save"
            :text="saving ? 'Saving…' : 'Save flow'"
            :icon="saveIcon"
            :disabled="saving"
            @click="saveFlow"
          />
          <AppButton
            automation="flow-deploy"
            :text="deploying ? 'Deploying…' : 'Deploy flow'"
            :icon="deployIcon"
            :disabled="dirty || deploying"
            @click="showDeployConfirmation = true"
          />
          <AppButton
            v-if="flow.status === 'deployed'"
            automation="flow-toggle-disabled"
            :text="
              togglingDisabled
                ? flow.disabled
                  ? 'Enabling…'
                  : 'Disabling…'
                : flow.disabled
                  ? 'Enable'
                  : 'Disable'
            "
            :icon="flow.disabled ? enableFlowIcon : disableFlowIcon"
            :disabled="togglingDisabled"
            @click="setFlowDisabled(!flow.disabled)"
          />
          <AppButton
            automation="flow-refresh-runtime"
            text="Refresh runtime"
            :icon="refreshIcon"
            @click="refreshRuntime()"
          />
        </div>
      </div>

      <AppFlowDebugPanel
        automation="flow-debug"
        :lifecycle="debugLifecycle"
        :snapshot="debugSnapshot"
        :stale="debugSnapshotStale"
        :error="debugError"
        :target-available="true"
        :host="debugHost"
        :capabilities="debugCapabilities"
        :inspection="debugInspection"
        :affected-output-points="debugAffectedOutputPoints"
        :live-output-enabled="debugLiveOutputEnabled"
        :live-output-priority="debugLiveOutputPriority"
        :live-output-hold-milliseconds="debugLiveOutputHoldMilliseconds"
        @load="loadDebugSession"
        @step-tick="stepDebugSession"
        @step-node="stepNodeDebugSession"
        @step-instruction="stepInstructionDebugSession"
        @run="runDebugSession"
        @run-to="runToBreakpoint"
        @pause="pauseDebugSession"
        @stop="stopDebugSession"
        @restart="restartDebugSession"
        @enable-live-output="enableLiveOutput"
      />

      <AppFlowEmulatorPanel
        v-if="selectedDebugTarget?.kind === 'emulator'"
        automation="flow-emulator"
        :snapshot="emulatorSnapshot"
        @set-input="setEmulatorInput"
        @advance="advanceEmulator"
        @fault="setEmulatorFault"
        @reset="resetEmulator"
      />

      <AppFlowDesignerCanvas
        v-bind="automation('canvas')"
        :flow="flow"
        :runtime="debugNodeRuntime ?? runtime"
        :current-node-id="debugInspection?.nodeId"
        :breakpoint-node-ids="debugBreakpoints.map((breakpoint) => breakpoint.nodeId)"
        @toggle-breakpoint="toggleBreakpoint"
        @[EVENTS.MOVE_NODE]="moveNode"
        @[EVENTS.REORDER_NODE]="reorderNode"
        @[EVENTS.DELETE_NODE]="deleteNode"
        @[EVENTS.ADD_CONNECTION]="addConnection"
        @[EVENTS.DELETE_CONNECTION]="deleteConnection"
        @[EVENTS.ADD_NODE]="addNode"
        @[EVENTS.UPDATE_NODE_LABEL]="updateNodeLabel"
        @[EVENTS.UPDATE_NODE_CONFIGURATION]="updateNodeConfiguration"
      />
    </template>

    <div v-else-if="!loading" class="not-found">
      <p>Flow not found</p>
      <h1>There is no flow named “{{ flowId }}”.</h1>
      <RouterLink :to="{ name: 'flows' }">Return to flows</RouterLink>
    </div>
  </section>
</template>

<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue';
import { onBeforeRouteLeave, useRouter } from 'vue-router';

import { useAutomation } from '@/composables/useAutomation';
import { EVENTS } from '@/constants/events';
import cancelIcon from '@/assets/icons/cancel-icon.svg';
import deployIcon from '@/assets/icons/deploy-icon.svg';
import disableFlowIcon from '@/assets/icons/disable-flow-icon.svg';
import discardIcon from '@/assets/icons/discard-icon.svg';
import enableFlowIcon from '@/assets/icons/enable-flow-icon.svg';
import refreshIcon from '@/assets/icons/refresh-icon.svg';
import renameFlowIcon from '@/assets/icons/rename-flow-icon.svg';
import saveIcon from '@/assets/icons/save-icon.svg';
import AppButton from '@/components/AppButton.vue';
import AppErrorNotice from '@/components/AppErrorNotice.vue';
import AppFlowDesignerCanvas from '@/features/flows/components/AppFlowDesignerCanvas.vue';
import AppFlowDebugTargetSelector from '@/features/flows/components/AppFlowDebugTargetSelector.vue';
import AppFlowDebugPanel from '@/features/flows/components/AppFlowDebugPanel.vue';
import AppFlowEmulatorPanel from '@/features/flows/components/AppFlowEmulatorPanel.vue';
import { getFlowDebugTargets } from '@/features/flows/debugTargets';
import {
  flowDebugApi,
  type DebugRuntimeSnapshot,
  type ExecutableFlowSource,
  type FlowDebugCapabilities,
  type FlowDebugInspection,
  type FlowDebugBreakpoint
} from '@/features/flows/api/flowDebugApi';
import { flowEmulatorApi, type EmulatorSnapshot } from '@/features/flows/api/flowEmulatorApi';
import { createExecutableFlowSource, graphRevision } from '@/features/flows/flowDebugSource';
import { useControllerTemplatesCatalogueStore } from '@/features/catalogues/stores/catalogues';
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
const automation = useAutomation('flow-designer');

const flowStore = useFlowsStore();
const runtimeStore = useFlowRuntimeStore();
const controllerTemplates = useControllerTemplatesCatalogueStore();
const router = useRouter();
const flow = computed(() => flowStore.findFlow(props.flowId));
const dirty = computed(() => flowStore.isFlowDirty(props.flowId));
const loading = ref(false);
const saving = ref(false);
const togglingDisabled = ref(false);
const loadError = ref<string>();
const saveError = ref<string>();
const runtimeError = ref<string>();
const noticeError = computed(() => loadError.value ?? saveError.value ?? runtimeError.value ?? '');
const runtimeFailureMessage = (error: unknown, fallback: string): string =>
  error instanceof FlowApiError && error.status
    ? `${error.message} (status ${error.status})`
    : error instanceof Error
      ? error.message
      : fallback;
const showDeployConfirmation = ref(false);
const deployDialog = ref<HTMLElement>();
const discardDialog = ref<HTMLElement>();
const runtime = computed(() => runtimeStore.snapshotFor(props.flowId));
const debugTargets = computed(() => getFlowDebugTargets(controllerTemplates.allItems));
const debugTargetId = ref('server');
type DesignerDebugLifecycle =
  | 'idle'
  | 'loading'
  | 'ready'
  | 'stepping'
  | 'running'
  | 'paused'
  | 'fault'
  | 'stopped';
const debugLifecycle = ref<DesignerDebugLifecycle>('idle');
const debugSessionId = ref<string>();
const debugSnapshot = ref<DebugRuntimeSnapshot>();
const debugRevision = ref<number>();
const debugError = ref<string>();
const debugAffectedOutputPoints = ref<string[]>([]);
const debugLiveOutputEnabled = ref(false);
const debugLiveOutputPriority = ref<number>();
const debugLiveOutputHoldMilliseconds = ref<number>();
const debugCapabilities = ref<FlowDebugCapabilities>();
const debugInspection = ref<FlowDebugInspection>();
const debugBreakpoints = ref<FlowDebugBreakpoint[]>([]);
const emulatorSnapshot = ref<EmulatorSnapshot>();
let debugController: AbortController | undefined;
let debugPollTimer: ReturnType<typeof window.setInterval> | undefined;
const stopDebugPolling = (): void => {
  if (debugPollTimer !== undefined) window.clearInterval(debugPollTimer);
  debugPollTimer = undefined;
};
const deploying = computed(() => runtimeStore.isDeploying(props.flowId));
let loadController: AbortController | undefined;
const loadGuard = createLatestRequestGuard();
const pendingRoute = ref<string>();
let allowNavigation = false;

watch(debugTargets, (targets) => {
  if (!targets.some((target) => target.id === debugTargetId.value)) debugTargetId.value = 'server';
});
watch(debugTargetId, () => {
  if (debugSessionId.value) void stopDebugSession();
});

const flowRevision = computed(() => (flow.value ? graphRevision(flow.value) : 1));
const debugSnapshotStale = computed(() =>
  Boolean(debugSnapshot.value && debugRevision.value !== flowRevision.value)
);
const debugNodeRuntime = computed(() => {
  const snapshot = debugSnapshot.value;
  if (!snapshot || debugSnapshotStale.value) return undefined;
  return {
    flowId: snapshot.flowId,
    state: snapshot.lifecycleState === 'fault' ? ('error' as const) : ('stopped' as const),
    updatedAt: new Date(snapshot.completedAtMs).toISOString(),
    nodes: Object.fromEntries(
      snapshot.nodes.map((node) => [
        node.nodeId,
        {
          state: (node.state === 'fault' || snapshot.lastReasonPath.includes(node.nodeId)
            ? 'error'
            : 'stopped') as 'error' | 'stopped',
          value: node.typedValue ? `${node.typedValue.value} · ${node.quality}` : node.quality,
          updatedAt: new Date(snapshot.completedAtMs).toISOString()
        }
      ])
    )
  };
});
const selectedDebugTarget = computed(() =>
  debugTargets.value.find((target) => target.id === debugTargetId.value)
);
const debugHost = computed<'server' | 'emulator' | 'controller'>(() => {
  const kind = selectedDebugTarget.value?.kind;
  return kind === 'emulator' || kind === 'controller' ? kind : 'server';
});
const executableSource = (): ExecutableFlowSource | undefined => {
  const current = flow.value;
  const target = selectedDebugTarget.value;
  if (!current || !target) return;
  return createExecutableFlowSource(current, target);
};
const debugFailure = (error: unknown): string =>
  error instanceof Error ? error.message : 'Debug operation failed.';
const loadDebugSession = async (): Promise<void> => {
  debugController?.abort();
  debugController = new AbortController();
  debugLifecycle.value = 'loading';
  debugError.value = undefined;
  debugSnapshot.value = undefined;
  try {
    const source = executableSource();
    if (!source) throw new Error('The flow is not available.');
    if (selectedDebugTarget.value?.kind === 'emulator' && !emulatorSnapshot.value)
      emulatorSnapshot.value = await flowEmulatorApi.create(source);
    const session = await flowDebugApi.load(
      source,
      debugHost.value,
      emulatorSnapshot.value?.emulatorId,
      debugController.signal
    );
    if (session.flowId !== props.flowId || session.revision !== source.revision)
      throw new Error('Loaded debug session does not match this flow revision.');
    debugSessionId.value = session.debugSessionId;
    debugRevision.value = session.revision;
    debugSnapshot.value = session.snapshot;
    debugAffectedOutputPoints.value = session.affectedOutputPoints;
    debugLiveOutputEnabled.value = session.liveOutputEnabled;
    debugLiveOutputPriority.value = session.liveOutputPriority;
    debugLiveOutputHoldMilliseconds.value = session.liveOutputHoldMilliseconds;
    debugCapabilities.value = session.capabilities;
    debugInspection.value = session.inspection;
    debugLifecycle.value = 'ready';
  } catch (error) {
    debugLifecycle.value = 'fault';
    debugError.value = debugFailure(error);
  }
};
const applyDebugSession = (session: Awaited<ReturnType<typeof flowDebugApi.stepNode>>): void => {
  debugLifecycle.value = session.lifecycleState === 'empty' ? 'stopped' : session.lifecycleState;
  debugSnapshot.value = session.snapshot;
  debugCapabilities.value = session.capabilities;
  debugInspection.value = session.inspection;
};
const stepNodeDebugSession = async (): Promise<void> => {
  if (!debugSessionId.value) return;
  try {
    applyDebugSession(await flowDebugApi.stepNode(props.flowId, debugSessionId.value));
  } catch (error) {
    debugError.value = debugFailure(error);
  }
};
const stepInstructionDebugSession = async (): Promise<void> => {
  if (!debugSessionId.value) return;
  try {
    applyDebugSession(await flowDebugApi.stepInstruction(props.flowId, debugSessionId.value));
  } catch (error) {
    debugError.value = debugFailure(error);
  }
};
const restartDebugSession = async (): Promise<void> => {
  if (!debugSessionId.value) return;
  try {
    applyDebugSession(await flowDebugApi.restart(props.flowId, debugSessionId.value));
  } catch (error) {
    debugError.value = debugFailure(error);
  }
};
const setEmulatorInput = async (pointId: string, value: boolean): Promise<void> => {
  if (!emulatorSnapshot.value) return;
  emulatorSnapshot.value = await flowEmulatorApi.setInputs(emulatorSnapshot.value.emulatorId, [
    { pointId, value, isGood: true }
  ]);
};
const advanceEmulator = async (milliseconds: number): Promise<void> => {
  if (!emulatorSnapshot.value) return;
  emulatorSnapshot.value = await flowEmulatorApi.advance(
    emulatorSnapshot.value.emulatorId,
    milliseconds
  );
};
const setEmulatorFault = async (fault: string | null): Promise<void> => {
  if (!emulatorSnapshot.value) return;
  emulatorSnapshot.value = await flowEmulatorApi.fault(emulatorSnapshot.value.emulatorId, fault);
};
const resetEmulator = async (powerCycle: boolean): Promise<void> => {
  if (!emulatorSnapshot.value) return;
  emulatorSnapshot.value = await flowEmulatorApi.reset(
    emulatorSnapshot.value.emulatorId,
    powerCycle
  );
};
const toggleBreakpoint = async (nodeId: string): Promise<void> => {
  if (!debugSessionId.value || !debugCapabilities.value?.maximumBreakpoints) return;
  const exists = debugBreakpoints.value.some((breakpoint) => breakpoint.nodeId === nodeId);
  const next = exists
    ? debugBreakpoints.value.filter((breakpoint) => breakpoint.nodeId !== nodeId)
    : [...debugBreakpoints.value, { nodeId, position: 'before' as const }];
  try {
    const session = await flowDebugApi.replaceBreakpoints(props.flowId, debugSessionId.value, next);
    debugBreakpoints.value = session.breakpoints;
  } catch (error) {
    debugError.value = debugFailure(error);
  }
};
const runToBreakpoint = async (): Promise<void> => {
  const breakpoint = debugBreakpoints.value[0];
  if (!debugSessionId.value || !breakpoint) {
    debugError.value = 'Add a breakpoint by double-clicking a node first.';
    return;
  }
  try {
    applyDebugSession(await flowDebugApi.runTo(props.flowId, debugSessionId.value, breakpoint));
  } catch (error) {
    debugError.value = debugFailure(error);
  }
};
const enableLiveOutput = async (confirmedPointIds: string[]): Promise<void> => {
  const sessionId = debugSessionId.value;
  if (!sessionId || debugSnapshotStale.value) return;
  debugError.value = undefined;
  try {
    const session = await flowDebugApi.enableLiveOutput(props.flowId, sessionId, confirmedPointIds);
    debugLiveOutputEnabled.value = session.liveOutputEnabled;
    debugLiveOutputPriority.value = session.liveOutputPriority;
    debugLiveOutputHoldMilliseconds.value = session.liveOutputHoldMilliseconds;
  } catch (error) {
    debugError.value = debugFailure(error);
  }
};
const stepDebugSession = async (): Promise<void> => {
  const sessionId = debugSessionId.value;
  if (!sessionId || debugSnapshotStale.value) return;
  debugLifecycle.value = 'stepping';
  debugError.value = undefined;
  try {
    const snapshot = await flowDebugApi.step(props.flowId, sessionId);
    if (
      snapshot.flowId !== props.flowId ||
      snapshot.revision !== debugRevision.value ||
      snapshot.debugSessionId !== sessionId
    )
      throw new Error('The debug service returned a stale or mismatched snapshot.');
    debugSnapshot.value = snapshot;
    debugLifecycle.value = 'ready';
  } catch (error) {
    debugLifecycle.value = 'fault';
    debugError.value = debugFailure(error);
  }
};
const runDebugSession = async (): Promise<void> => {
  const sessionId = debugSessionId.value;
  if (!sessionId) return;
  try {
    const session = await flowDebugApi.run(props.flowId, sessionId);
    debugLifecycle.value = session.lifecycleState === 'running' ? 'running' : 'fault';
    stopDebugPolling();
    debugPollTimer = window.setInterval(async () => {
      if (debugLifecycle.value !== 'running') return;
      try {
        const current = await flowDebugApi.inspect(props.flowId, sessionId);
        if (current.snapshot) debugSnapshot.value = current.snapshot;
        if (current.lifecycleState !== 'running') {
          debugLifecycle.value =
            current.lifecycleState === 'empty' ? 'stopped' : current.lifecycleState;
          stopDebugPolling();
        }
      } catch (error) {
        debugLifecycle.value = 'fault';
        debugError.value = debugFailure(error);
        stopDebugPolling();
      }
    }, 250);
  } catch (error) {
    debugLifecycle.value = 'fault';
    debugError.value = debugFailure(error);
  }
};
const pauseDebugSession = async (): Promise<void> => {
  const sessionId = debugSessionId.value;
  if (!sessionId) return;
  stopDebugPolling();
  try {
    const session = await flowDebugApi.pause(props.flowId, sessionId);
    debugSnapshot.value = session.snapshot;
    debugLifecycle.value = 'paused';
  } catch (error) {
    debugLifecycle.value = 'fault';
    debugError.value = debugFailure(error);
  }
};
const stopDebugSession = async (keepalive = false): Promise<void> => {
  stopDebugPolling();
  debugController?.abort();
  const sessionId = debugSessionId.value;
  debugSessionId.value = undefined;
  debugAffectedOutputPoints.value = [];
  debugLiveOutputEnabled.value = false;
  debugLiveOutputPriority.value = undefined;
  debugLiveOutputHoldMilliseconds.value = undefined;
  debugCapabilities.value = undefined;
  debugInspection.value = undefined;
  debugBreakpoints.value = [];
  debugLifecycle.value = 'stopped';
  if (!sessionId) return;
  try {
    await flowDebugApi.stop(props.flowId, sessionId, keepalive);
  } catch (error) {
    if (!keepalive) debugError.value = debugFailure(error);
  }
};

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
    // A missing flow already has a dedicated, actionable empty state below. Do not
    // cover its navigation link with a second modal error presentation.
    loadError.value =
      error instanceof FlowApiError && error.status === 404
        ? undefined
        : error instanceof Error
          ? error.message
          : 'Unable to load this flow.';
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
    runtimeError.value = runtimeFailureMessage(error, 'Unable to load runtime state.');
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
    const message = runtimeFailureMessage(error, 'Unable to deploy this flow.');
    runtimeStore.failDeployment(props.flowId, message);
    runtimeError.value = message;
  }
};

const setFlowDisabled = async (disabled: boolean): Promise<void> => {
  togglingDisabled.value = true;
  runtimeError.value = undefined;
  try {
    const saved = await flowApi.setFlowDisabled(props.flowId, disabled);
    flowStore.replaceFlowFromPayload(saved);
    await refreshRuntime();
  } catch (error) {
    runtimeError.value =
      error instanceof Error ? error.message : 'Unable to change the flow execution state.';
  } finally {
    togglingDisabled.value = false;
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
  controllerTemplates.cancel();
  void stopDebugSession(true);
});

const handleBeforeUnload = (event: BeforeUnloadEvent): void => {
  if (debugSessionId.value) void stopDebugSession(true);
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
  if (allowNavigation || !dirty.value) {
    if (debugSessionId.value) void stopDebugSession(true);
    return true;
  }
  pendingRoute.value = to.fullPath;
  return false;
});
onMounted(() => {
  window.addEventListener('beforeunload', handleBeforeUnload);
  void controllerTemplates.load();
});
onBeforeUnmount(() => window.removeEventListener('beforeunload', handleBeforeUnload));
</script>

<style scoped>
.designer-page {
  display: flex;
  height: calc(100dvh - 72px);
  width: calc(100% - 40px);
  min-height: 0;
  margin: var(--space-0) auto;
  padding: var(--space-17) var(--space-0) var(--space-12);
  overflow: hidden;
  flex-direction: column;
}

.designer-page :deep(.canvas-frame) {
  min-height: 0;
  flex: 1;
}

.designer-heading {
  display: flex;
  gap: var(--space-14);
  align-items: end;
  justify-content: space-between;
  margin-bottom: var(--space-12);
}

.designer-heading a,
.not-found a {
  color: var(--color-action-primary);
  font-size: var(--font-size-lg);
  font-weight: var(--font-weight-bold);
  text-decoration: none;
}

.title-row {
  display: flex;
  gap: var(--space-5-5);
  align-items: center;
  margin-top: var(--space-4);
}

h1 {
  margin: var(--space-0);
  color: var(--color-text-primary);
  font-size: var(--font-size-heading-fluid);
  letter-spacing: -0.035em;
}

.title-row span {
  padding: var(--space-2) var(--space-3-5);
  color: var(--color-node-status-fill);
  font-size: var(--font-size-xs);
  font-weight: var(--font-weight-black);
  letter-spacing: 0.08em;
  background: var(--color-surface-disabled);
  border-radius: var(--radius-pill);
  text-transform: uppercase;
}

.title-row span.deployed {
  color: var(--color-action-primary-strong);
  background: var(--color-action-primary-surface);
}

.title-row span.disabled {
  color: var(--color-text-secondary);
  border-color: var(--color-text-secondary);
}

.title-row .dirty-state {
  color: var(--color-warning-text);
  background: var(--color-warning-surface);
}

.title-row .runtime-state {
  color: var(--color-info-text);
  background: var(--color-info-surface);
}

.designer-heading p {
  margin: var(--space-3) var(--space-0) var(--space-0);
  color: var(--color-text-muted);
  font-size: var(--font-size-xl);
}

.heading-actions {
  display: flex;
  gap: var(--space-3-5);
}

.request-status {
  margin: var(--space-0) var(--space-0) var(--space-5-5);
  padding: var(--space-4-5) var(--space-5-5);
  border-radius: var(--radius-lg);
}

.dialog-backdrop {
  position: fixed;
  z-index: 20;
  display: grid;
  inset: 0;
  place-items: center;
  padding: var(--space-10);
  background: var(--color-modal-backdrop);
}

.discard-dialog {
  width: min(430px, 100%);
  padding: var(--space-12);
  background: var(--color-surface-raised);
  border-radius: var(--radius-2xl);
  box-shadow: var(--shadow-dialog);
}

.discard-dialog h2 {
  margin: var(--space-0);
  color: var(--color-text-primary);
}

.discard-dialog p {
  color: var(--color-text-muted);
}

.discard-dialog > div {
  display: flex;
  gap: var(--space-3-5);
  justify-content: end;
}

.request-status {
  color: var(--color-info-text);
  background: var(--color-info-surface);
}

.not-found {
  padding: var(--space-40) var(--space-0);
}

.eyebrow {
  margin: var(--space-0) var(--space-0) var(--space-4-5);
  color: var(--color-danger-text-muted);
  font-size: var(--font-size-sm);
  font-weight: var(--font-weight-black);
  letter-spacing: 0.13em;
  text-transform: uppercase;
}

.not-found h1 {
  margin-bottom: var(--space-12);
}

/* Tablet breakpoint (48rem): reflows multi-column controls and workspace panels. */
@media (max-width: 48rem) {
  .designer-page {
    width: calc(100% - 28px);
    padding-top: var(--space-10);
  }

  .designer-heading {
    align-items: start;
    flex-direction: column;
  }
}
</style>
