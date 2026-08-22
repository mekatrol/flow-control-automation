<template>
  <section v-bind="automation()" class="designer-page">
    <AppErrorNotice
      id="flow-designer-error-notice"
      v-bind="automation('designer-error')"
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
            v-bind="automation('deploy-cancel')"
            text="Cancel"
            :icon="cancelIcon"
            data-dialog-initial-focus
            @click="closeDeployConfirmation"
          />
          <AppButton
            v-bind="automation('deploy-confirm')"
            text="Deploy now"
            :icon="deployIcon"
            @click="deployFlow"
          />
        </div>
      </section>
    </div>
    <div v-if="showRevertConfirmation" class="dialog-backdrop">
      <section
        class="discard-dialog"
        role="alertdialog"
        aria-modal="true"
        aria-labelledby="revert-title"
      >
        <h2 id="revert-title">Revert this draft?</h2>
        <p>All draft changes will be replaced by the currently deployed version.</p>
        <div>
          <AppButton
            v-bind="automation('revert-cancel')"
            text="Keep draft"
            :icon="cancelIcon"
            @click="showRevertConfirmation = false"
          />
          <AppButton
            v-bind="automation('revert-confirm')"
            text="Revert draft"
            :icon="discardIcon"
            @click="revertDraftToDeployed"
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
            v-bind="automation('discard-keep-editing')"
            text="Keep editing"
            :icon="renameFlowIcon"
            data-dialog-initial-focus
            @click="keepEditing"
          />
          <AppButton
            v-bind="automation('discard-confirm')"
            text="Discard changes"
            :icon="discardIcon"
            @click="discardChanges"
          />
        </div>
      </section>
    </div>
    <template v-if="flow">
      <section
        v-if="draftFlow?.deployedRevision"
        class="version-selector"
        aria-label="Flow version"
      >
        <div role="group" aria-label="Version to view">
          <AppButton
            v-bind="automation('view-draft')"
            text="Draft"
            :disabled="versionView === 'draft'"
            @click="showDraftVersion"
          />
          <AppButton
            v-bind="automation('view-deployed')"
            text="Deployed"
            :disabled="versionView === 'deployed' || loadingDeployedVersion"
            @click="showDeployedVersion"
          />
        </div>
        <span v-if="versionView === 'deployed'" role="status">
          Viewing deployed revision {{ flow.revision }}. This version is read-only.
        </span>
        <AppButton
          v-if="versionView === 'draft' && draftFlow.status === 'draft'"
          v-bind="automation('revert-draft')"
          text="Revert draft to deployed"
          :disabled="saving || revertingDraft"
          @click="showRevertConfirmation = true"
        />
      </section>
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
            v-if="workspaceMode === 'debugger'"
            v-model="debugTargetId"
            v-bind="automation('debug-target')"
            :targets="debugTargets"
            :loading="controllerTemplates.loading"
            :error="controllerTemplates.error"
          />
          <AppButton
            v-if="versionView === 'draft'"
            v-bind="automation('save')"
            :text="saving ? 'Saving…' : 'Save flow'"
            :icon="saveIcon"
            :disabled="saving"
            @click="saveFlow"
          />
          <AppButton
            v-if="versionView === 'draft'"
            v-bind="automation('compile')"
            :text="compiling ? 'Compiling…' : 'Compile'"
            :icon="compileIcon"
            :disabled="compiling"
            @click="compileFlow"
          />
          <AppButton
            v-if="versionView === 'draft'"
            v-bind="automation('deploy')"
            :text="deploying ? 'Deploying…' : 'Deploy flow'"
            :icon="deployIcon"
            :disabled="dirty || deploying || !pointReferencesValid"
            @click="showDeployConfirmation = true"
          />
          <AppButton
            v-if="flow.status === 'deployed'"
            v-bind="automation('toggle-disabled')"
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
            v-bind="automation('refresh-runtime')"
            text="Refresh runtime"
            :icon="refreshIcon"
            @click="refreshRuntime()"
          />
        </div>
      </div>

      <nav v-if="versionView === 'draft'" class="workspace-modes" aria-label="Flow workspace mode">
        <AppLink
          v-bind="automation('design-mode')"
          text="Design"
          :to="{ name: ROUTE_NAMES.flowDesigner, params: { flowId } }"
          :aria-current="workspaceMode === 'design' ? 'page' : undefined"
          :icon="designIcon"
          :disabled="saving"
          @click="saveFlow"
        />
        <AppLink
          v-bind="automation('simulate-mode')"
          text="Simulate"
          :to="{ name: ROUTE_NAMES.flowSimulator, params: { flowId } }"
          :aria-current="workspaceMode === 'simulator' ? 'page' : undefined"
          :icon="simulateIcon"
          :disabled="saving"
          @click="saveFlow"
        />
        <AppLink
          v-bind="automation('debug-mode')"
          text="Debug"
          :to="{ name: ROUTE_NAMES.flowDebugger, params: { flowId } }"
          :aria-current="workspaceMode === 'debugger' ? 'page' : undefined"
          :icon="debugIcon"
          :disabled="saving"
          @click="saveFlow"
        />
      </nav>

      <section
        v-if="versionView === 'draft'"
        class="context-preview"
        aria-label="Execution context validation preview"
      >
        <label>
          <span>Validate against execution context</span>
          <select v-model="selectedContextId" :disabled="contextsLoading">
            <option value="">Flow declarations and global points</option>
            <option v-for="context in executionContexts" :key="context.id" :value="context.id">
              {{ context.name }} ({{ context.id }})
            </option>
          </select>
        </label>
        <small v-if="contextsLoading" role="status">Loading execution contexts…</small>
        <small v-else-if="contextsError" role="status">{{ contextsError }}</small>
        <small v-else-if="!pointReferencesValid" role="alert">
          Drafts can be saved, but deployment is blocked until every point reference is valid.
        </small>
      </section>

      <AppFlowCompileResults
        v-if="versionView === 'draft' && compileResult"
        v-bind="automation('compile-results')"
        :result="compileResult"
        :node-ids="draftFlow?.nodes.map(({ id }) => id) ?? []"
        @select-diagnostic="focusDiagnosticNode"
      />

      <AppFlowTutorialPanel
        v-if="activeTutorial"
        v-bind="automation('tutorial')"
        :tutorial="activeTutorial"
        @[EVENTS.CLOSE]="activeTutorial = undefined"
        @[EVENTS.OPEN_TUTORIAL]="openTutorialExample"
        @[EVENTS.COPY_TUTORIAL]="copyTutorialExample"
      />

      <AppFlowSimulatorPanel
        v-if="workspaceMode === 'simulator'"
        v-bind="automation('simulator')"
        :lifecycle="simulator.lifecycle"
        :session="simulator.session"
        :error="simulator.error"
        @[EVENTS.START_SIMULATION]="startSimulation"
        @[EVENTS.STEP_TICK]="simulator.stepTick"
        @[EVENTS.STEP_NODE]="simulator.stepNode"
        @[EVENTS.STEP_INSTRUCTION]="simulator.stepInstruction"
        @[EVENTS.RUN]="simulator.run"
        @[EVENTS.PAUSE]="simulator.pause"
        @[EVENTS.RESTART]="simulator.restart"
        @[EVENTS.STOP_SIMULATION]="simulator.stop"
        @[EVENTS.APPLY_INPUTS_STEP]="simulator.applyInputsAndStep"
        @[EVENTS.ADVANCE]="simulator.advance"
        @[EVENTS.FAULT]="simulator.fault"
        @[EVENTS.RESET]="simulator.resetIo"
        @[EVENTS.RESET_INPUTS]="simulator.resetInputs"
      />

      <AppFlowDebugPanel
        v-if="workspaceMode === 'debugger'"
        v-bind="automation('debug')"
        :lifecycle="debugLifecycle"
        :snapshot="debugSnapshot"
        :stale="debugSnapshotStale"
        :error="debugError"
        :target-available="true"
        :host="debugHost"
        :capabilities="debugCapabilities"
        :inspection="debugInspection"
        :execution-order="debugExecutionOrder"
        :breakpoints="debugBreakpoints"
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
        @[EVENTS.RUN_TO_BOUNDARY]="stepDebugSession"
        @[EVENTS.SELECT_DIAGNOSTIC]="focusDiagnosticNode"
        @pause="pauseDebugSession"
        @stop="stopDebugSession"
        @restart="restartDebugSession"
        @enable-live-output="enableLiveOutput"
      />

      <AppFlowEmulatorPanel
        v-if="workspaceMode === 'debugger' && selectedDebugTarget?.kind === 'emulator'"
        v-bind="automation('emulator')"
        :snapshot="emulatorSnapshot"
        @[EVENTS.APPLY_INPUTS_STEP]="applyEmulatorInputsAndStep"
        @[EVENTS.ADVANCE]="advanceEmulator"
        @[EVENTS.FAULT]="setEmulatorFault"
        @[EVENTS.RESET]="resetEmulator"
        @[EVENTS.RESET_INPUTS]="resetEmulatorInputs"
      />

      <div :class="{ 'deployed-version-canvas': versionView === 'deployed' }">
        <AppFlowDesignerCanvas
          v-bind="automation('canvas')"
          :flow="flow"
          :runtime="debugNodeRuntime ?? runtime"
          :current-node-id="debugInspection?.nodeId"
          :breakpoints="debugBreakpoints"
          :connector-values="debugConnectorValues"
          :debugging="workspaceMode === 'debugger' && Boolean(debugSessionId)"
          :focus-node-id="diagnosticNodeId"
          :context-point-contracts="selectedContext?.pointContracts"
          :execution-context-id="selectedContextId || undefined"
          @point-validation="setPointValidation"
          @create-virtual-point="createVirtualPoint"
          @[EVENTS.SET_BREAKPOINT]="setBreakpoint"
          @[EVENTS.RUN_TO_NODE]="runToNode"
          @[EVENTS.MOVE_NODE]="moveNode"
          @[EVENTS.REORDER_NODE]="reorderNode"
          @[EVENTS.DELETE_NODE]="deleteNode"
          @[EVENTS.ADD_CONNECTION]="addConnection"
          @[EVENTS.DELETE_CONNECTION]="deleteConnection"
          @[EVENTS.ADD_NODE]="addNode"
          @[EVENTS.UPDATE_NODE_LABEL]="updateNodeLabel"
          @[EVENTS.UPDATE_NODE_CONFIGURATION]="updateNodeConfiguration"
        />
      </div>
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
import { useSaveShortcut } from '@/composables/useSaveShortcut';
import { onBeforeRouteLeave, useRouter } from 'vue-router';
import { ROUTE_NAMES } from '@/router';

import { useAutomation } from '@/composables/useAutomation';
import { EVENTS } from '@/constants/events';
import cancelIcon from '@/assets/icons/cancel-icon.svg';
import deployIcon from '@/assets/icons/deploy-icon.svg';
import compileIcon from '@/assets/icons/compile-icon.svg';
import disableFlowIcon from '@/assets/icons/disable-flow-icon.svg';
import discardIcon from '@/assets/icons/discard-icon.svg';
import enableFlowIcon from '@/assets/icons/enable-flow-icon.svg';
import refreshIcon from '@/assets/icons/refresh-icon.svg';
import renameFlowIcon from '@/assets/icons/rename-flow-icon.svg';
import saveIcon from '@/assets/icons/save-icon.svg';
import designIcon from '@/assets/icons/flow-design-icon.svg';
import simulateIcon from '@/assets/icons/flow-simulate-icon.svg';
import debugIcon from '@/assets/icons/flow-debug-icon.svg';
import AppButton from '@/components/AppButton.vue';
import AppLink from '@/components/AppLink.vue';
import AppErrorNotice from '@/components/AppErrorNotice.vue';
import AppFlowDesignerCanvas from '@/features/flows/components/AppFlowDesignerCanvas.vue';
import AppFlowCompileResults from '@/features/flows/components/AppFlowCompileResults.vue';
import AppFlowDebugTargetSelector from '@/features/flows/components/AppFlowDebugTargetSelector.vue';
import AppFlowDebugPanel from '@/features/flows/components/AppFlowDebugPanel.vue';
import AppFlowEmulatorPanel from '@/features/flows/components/AppFlowEmulatorPanel.vue';
import AppFlowSimulatorPanel from '@/features/flows/components/AppFlowSimulatorPanel.vue';
import AppFlowTutorialPanel from '@/features/flows/components/AppFlowTutorialPanel.vue';
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
import {
  createExecutableFlowSource,
  FlowDebugSourceError,
  graphRevision
} from '@/features/flows/flowDebugSource';
import { flowCompileApi, type FlowCompileResult } from '@/features/flows/api/flowCompileApi';
import { useControllerTemplatesCatalogueStore } from '@/features/catalogues/stores/catalogues';
import { useFlowsStore } from '@/features/flows/stores/flows';
import type { ZOrderCommand } from '@/features/flows/graph/zOrder';
import { FlowApiError, flowApi } from '@/features/flows/api/flowApi';
import { flowRuntimeApi } from '@/features/flows/api/flowRuntimeApi';
import { createLatestRequestGuard } from '@/features/flows/api/latestRequest';
import { useFlowRuntimeStore } from '@/features/flows/stores/flowRuntime';
import { useFlowSimulatorStore } from '@/features/flows/stores/flowSimulator';
import { useModalFocus } from '@/features/flows/composables/useModalFocus';
import type {
  FlowConfigurationValue,
  FlowConnectionEndpoint,
  FlowDefinition,
  FlowNode
} from '@/features/flows/types';
import type { FlowTutorial } from '@/features/flows/tutorialCatalogue';
import { flowDomainToDto, flowDtoToDomain } from '@/features/flows/api/flowMapper';
import {
  executionContextApi,
  type ExecutionContextSummary
} from '@/features/flows/api/executionContextApi';
import {
  isPointNode,
  validatePointReference,
  type PointValidationState
} from '@/features/flows/flowPointValidation';
import type { VirtualPointDeclaration } from '@/features/flows/types';

const props = defineProps<{
  flowId: string;
  workspaceMode: 'design' | 'simulator' | 'debugger';
}>();
const automation = useAutomation('flow-designer');

const flowStore = useFlowsStore();
const runtimeStore = useFlowRuntimeStore();
const simulator = useFlowSimulatorStore();
const workspaceMode = computed(() => props.workspaceMode);
const activeTutorial = ref<FlowTutorial>();
const controllerTemplates = useControllerTemplatesCatalogueStore();
const router = useRouter();
const draftFlow = computed(() => flowStore.findFlow(props.flowId));
const deployedFlow = ref<FlowDefinition>();
const versionView = ref<'draft' | 'deployed'>('draft');
const loadingDeployedVersion = ref(false);
const revertingDraft = ref(false);
const compiling = ref(false);
const compileResult = ref<FlowCompileResult>();
const compiledGraphRevision = ref<number>();
const flow = computed(() =>
  versionView.value === 'deployed' ? deployedFlow.value : draftFlow.value
);
const dirty = computed(() => flowStore.isFlowDirty(props.flowId));
const executionContexts = ref<ExecutionContextSummary[]>([]);
const selectedContextId = ref('');
const contextsLoading = ref(false);
const contextsError = ref('');
const pointValidation = ref<Record<string, PointValidationState>>({});
let pointValidationController: AbortController | undefined;
const selectedContext = computed(() =>
  executionContexts.value.find(({ id }) => id === selectedContextId.value)
);
const mergedPointDeclarations = computed(() => {
  const result = new Map<string, VirtualPointDeclaration>();
  for (const declaration of [
    ...(selectedContext.value?.pointContracts ?? []),
    ...(flow.value?.virtualPointDeclarations ?? [])
  ])
    result.set(declaration.key, declaration);
  return [...result.values()];
});
const pointReferencesValid = computed(() => {
  const nodes = flow.value?.nodes.filter(isPointNode) ?? [];
  return nodes.every((node) => pointValidation.value[node.id] === 'valid');
});
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
const showRevertConfirmation = ref(false);
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
const debugExecutionOrder = ref<string[]>([]);
const diagnosticNodeId = ref<string>();
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
watch(flowRevision, (revision, previous) => {
  if (previous !== undefined && revision !== previous) simulator.markStale();
  if (compiledGraphRevision.value !== undefined && revision !== compiledGraphRevision.value) {
    compileResult.value = undefined;
    compiledGraphRevision.value = undefined;
  }
});
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
const debugConnectorValues = computed(() => {
  const snapshot = debugSnapshot.value;
  const currentFlow = flow.value;
  if (!snapshot || !currentFlow || debugSnapshotStale.value) return undefined;
  const values: Record<
    string,
    Record<string, import('@/features/flows/api/flowRuntimeApi').ConnectorRuntimeValue>
  > = {};
  for (const nodeSnapshot of snapshot.nodes) {
    const node = currentFlow.nodes.find((candidate) => candidate.id === nodeSnapshot.nodeId);
    if (!node || !nodeSnapshot.typedValue) continue;
    const typed = nodeSnapshot.typedValue;
    const text = typed.type === 'number' ? String(typed.number) : String(typed.value);
    const units = undefined;
    values[node.id] = {};
    for (const connector of node.connectors.filter((candidate) => candidate.direction === 'output'))
      values[node.id]![connector.id] = {
        value: text,
        quality: nodeSnapshot.quality,
        units,
        state: 'committed'
      };
  }
  for (const [nodeId, typed] of Object.entries(debugInspection.value?.nodeValues ?? {})) {
    const node = currentFlow.nodes.find((candidate) => candidate.id === nodeId);
    if (!node) continue;
    const text = typed.type === 'number' ? String(typed.number) : String(typed.value);
    values[nodeId] ??= {};
    for (const connector of node.connectors.filter((candidate) => candidate.direction === 'output'))
      values[nodeId]![connector.id] = {
        value: text,
        quality: typed.quality ?? 'good',
        state: 'paused-frame'
      };
  }
  for (const connection of currentFlow.connections) {
    const source = values[connection.start.nodeId]?.[connection.start.connectorId];
    if (!source) continue;
    (values[connection.end.nodeId] ??= {})[connection.end.connectorId] = source;
  }
  return values;
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
const compileFlow = async (): Promise<void> => {
  const current = draftFlow.value;
  const target = debugTargets.value.find((item) => item.id === 'server');
  if (!current || !target) return;
  compiling.value = true;
  saveError.value = undefined;
  try {
    const source = createExecutableFlowSource(current, target);
    compileResult.value = await flowCompileApi.compile(source);
    compiledGraphRevision.value = graphRevision(current);
    const firstPath = compileResult.value.diagnostics[0]?.path ?? '';
    const match = /^\/nodes\/(\d+)(?:\/|$)/.exec(firstPath);
    if (match) diagnosticNodeId.value = current.nodes[Number(match[1])]?.id;
  } catch (error) {
    const nodeIndex =
      error instanceof FlowDebugSourceError && error.nodeId
        ? current.nodes.findIndex(({ id }) => id === error.nodeId)
        : -1;
    compileResult.value = {
      success: false,
      diagnostics: [
        {
          code: 'InvalidDraft',
          displayCode: 'FLOW-DRAFT',
          path: nodeIndex >= 0 ? `/nodes/${nodeIndex}` : '',
          title: 'Draft cannot be compiled',
          message: error instanceof Error ? error.message : 'The draft could not be compiled.'
        }
      ]
    };
    compiledGraphRevision.value = graphRevision(current);
  } finally {
    compiling.value = false;
  }
};
const startSimulation = async (): Promise<void> => {
  const current = flow.value;
  const target = debugTargets.value.find((item) => item.id === 'server');
  if (!current || !target) return;
  await simulator.start(createExecutableFlowSource(current, target));
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
  debugExecutionOrder.value = session.executionOrder ?? [];
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
const applyEmulatorInputsAndStep = async (
  inputs: import('@/features/flows/api/flowEmulatorApi').EmulatorInputChange[]
): Promise<void> => {
  if (!emulatorSnapshot.value) return;
  emulatorSnapshot.value = await flowEmulatorApi.applyInputsAndStep(
    emulatorSnapshot.value.emulatorId,
    inputs
  );
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
const resetEmulatorInputs = async (): Promise<void> => {
  if (!emulatorSnapshot.value) return;
  emulatorSnapshot.value = await flowEmulatorApi.resetInputs(emulatorSnapshot.value.emulatorId);
};
const setBreakpoint = async (
  nodeId: string,
  position: 'before' | 'after' | null
): Promise<void> => {
  if (!debugSessionId.value || !debugCapabilities.value?.maximumBreakpoints) return;
  const retained = debugBreakpoints.value.filter((breakpoint) => breakpoint.nodeId !== nodeId);
  const next = position ? [...retained, { nodeId, position }] : retained;
  try {
    const session = await flowDebugApi.replaceBreakpoints(props.flowId, debugSessionId.value, next);
    debugBreakpoints.value = session.breakpoints;
  } catch (error) {
    debugError.value = debugFailure(error);
  }
};
const runToNode = async (nodeId: string): Promise<void> => {
  if (!debugSessionId.value) return;
  try {
    applyDebugSession(
      await flowDebugApi.runTo(props.flowId, debugSessionId.value, { nodeId, position: 'before' })
    );
  } catch (error) {
    debugError.value = debugFailure(error);
  }
};
const focusDiagnosticNode = (nodeId: string): void => {
  diagnosticNodeId.value = nodeId;
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
const openTutorialExample = (tutorial: FlowTutorial): void => {
  activeTutorial.value = tutorial;
  void router.push({ name: ROUTE_NAMES.flowSimulator, params: { flowId: props.flowId } });
};
const copyTutorialExample = async (tutorial: FlowTutorial): Promise<void> => {
  try {
    const created = await flowApi.createFlow(`${tutorial.title} copy`);
    await flowApi.saveFlow(
      flowDomainToDto({
        ...tutorial.flow,
        id: created.id,
        name: created.name,
        updatedAt: created.updatedAt
      })
    );
    await router.push({ name: 'flow-designer', params: { flowId: created.id } });
  } catch (error) {
    loadError.value = runtimeFailureMessage(error, 'Unable to copy the tutorial flow.');
  }
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

const setPointValidation = (nodeId: string, state: PointValidationState): void => {
  pointValidation.value[nodeId] = state;
};
const validateAllPointReferences = async (): Promise<boolean> => {
  pointValidationController?.abort();
  const controller = new AbortController();
  pointValidationController = controller;
  const nodes = flow.value?.nodes.filter(isPointNode) ?? [];
  for (const node of nodes) pointValidation.value[node.id] = 'pending';
  const results = await Promise.all(
    nodes.map((node) =>
      validatePointReference(
        node,
        mergedPointDeclarations.value,
        controller.signal,
        selectedContextId.value || undefined
      )
    )
  ).catch(() => undefined);
  if (!results || controller.signal.aborted) return false;
  nodes.forEach((node, index) => (pointValidation.value[node.id] = results[index]!.state));
  return results.every(({ state }) => state === 'valid');
};
const createVirtualPoint = (declaration: VirtualPointDeclaration): void => {
  flowStore.addVirtualPointDeclaration(props.flowId, declaration);
  void validateAllPointReferences();
};
const loadExecutionContexts = async (): Promise<void> => {
  contextsLoading.value = true;
  contextsError.value = '';
  try {
    executionContexts.value = await executionContextApi.list();
    const containing = executionContexts.value.find((context) =>
      context.programs.some(({ flowId }) => flowId === props.flowId)
    );
    if (!selectedContextId.value && containing) selectedContextId.value = containing.id;
  } catch (error) {
    contextsError.value =
      error instanceof Error ? error.message : 'Unable to load execution contexts.';
  } finally {
    contextsLoading.value = false;
  }
};

const loadFlow = async (flowId: string): Promise<void> => {
  // Route parameters can change before a request finishes. Abort the old fetch
  // and also use a generation guard so a late response cannot replace the new flow.
  loadController?.abort();
  const controller = new AbortController();
  const requestGeneration = loadGuard.begin();
  loadController = controller;
  versionView.value = 'draft';
  deployedFlow.value = undefined;
  loading.value = true;
  loadError.value = undefined;
  flowStore.selectFlow(flowId);
  try {
    const payload = await flowApi.getFlow(flowId, controller.signal);
    if (!loadGuard.isCurrent(requestGeneration)) return;
    flowStore.replaceFlowFromPayload(payload);
    flowStore.selectFlow(flowId);
    await validateAllPointReferences();
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
    flowStore.replaceFlowFromPayload(await flowApi.getFlow(props.flowId));
  } catch (error) {
    const message = runtimeFailureMessage(error, 'Unable to deploy this flow.');
    runtimeStore.failDeployment(props.flowId, message);
    runtimeError.value = message;
  }
};

const showDraftVersion = (): void => {
  versionView.value = 'draft';
};

const showDeployedVersion = async (): Promise<void> => {
  loadingDeployedVersion.value = true;
  saveError.value = undefined;
  try {
    deployedFlow.value = flowDtoToDomain(await flowApi.getDeployedFlow(props.flowId));
    versionView.value = 'deployed';
  } catch (error) {
    saveError.value = runtimeFailureMessage(error, 'Unable to load the deployed version.');
  } finally {
    loadingDeployedVersion.value = false;
  }
};

const revertDraftToDeployed = async (): Promise<void> => {
  showRevertConfirmation.value = false;
  revertingDraft.value = true;
  saveError.value = undefined;
  try {
    flowStore.replaceFlowFromPayload(await flowApi.revertToDeployed(props.flowId));
    versionView.value = 'draft';
  } catch (error) {
    saveError.value = runtimeFailureMessage(error, 'Unable to revert the draft.');
  } finally {
    revertingDraft.value = false;
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

useSaveShortcut(saveFlow, () => !saving.value);

watch(
  () => props.flowId,
  (flowId, previous) => {
    if (previous !== undefined && flowId !== previous) void simulator.stop(true);
    void loadFlow(flowId);
  },
  { immediate: true }
);
watch(selectedContextId, () => void validateAllPointReferences());
onMounted(() => void loadExecutionContexts());
onBeforeUnmount(() => {
  loadGuard.invalidate();
  loadController?.abort();
  controllerTemplates.cancel();
  pointValidationController?.abort();
  void simulator.stop(true);
  void stopDebugSession(true);
});

const handleBeforeUnload = (event: BeforeUnloadEvent): void => {
  if (simulator.session) void simulator.stop(true);
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
  const workspaceRoutes = [
    ROUTE_NAMES.flowDesigner,
    ROUTE_NAMES.flowSimulator,
    ROUTE_NAMES.flowDebugger
  ];
  if (to.params.flowId === props.flowId && workspaceRoutes.includes(String(to.name))) return true;
  // Client-side routing does not trigger beforeunload, so it needs a separate
  // guard and an application-owned dialog that can keep or discard the draft.
  if (allowNavigation || !dirty.value) {
    if (simulator.session) void simulator.stop(true);
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
  width: calc(100% - 40px);
  height: calc(100dvh - 72px);
  min-height: 0;
  margin: var(--space-0) auto;
  padding: var(--space-17) var(--space-0) var(--space-12);
  flex-direction: column;
}

.designer-page :deep(.canvas-frame) {
  min-height: 0;
  flex: 1;
}

.workspace-modes {
  display: flex;
  gap: var(--space-2);
  margin-bottom: var(--space-3);
}

.workspace-modes a {
  min-height: var(--control-min-height);
  padding: var(--space-2) var(--space-4);
  color: var(--color-text-primary);
  background: var(--color-surface-raised);
  border: var(--border-width-default) solid var(--color-border-default);
  border-radius: var(--radius-lg);
  text-decoration: none;
}

.workspace-modes a[aria-current='page'] {
  color: var(--color-action-primary-strong);
  background: var(--color-action-primary-surface);
  border-color: var(--color-action-primary);
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
  color: var(--color-text-primary);
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

.version-selector {
  display: flex;
  align-items: center;
  gap: var(--space-4);
  justify-content: space-between;
  margin-bottom: var(--space-5);
}

.version-selector > div {
  display: flex;
  gap: var(--space-2);
}

.deployed-version-canvas {
  pointer-events: none;
  opacity: 0.9;
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
    overflow-y: auto;
  }

  .designer-page :deep(.canvas-frame) {
    min-height: 24rem;
    flex: 0 0 24rem;
  }

  .designer-heading {
    align-items: start;
    flex-direction: column;
  }
}
</style>
