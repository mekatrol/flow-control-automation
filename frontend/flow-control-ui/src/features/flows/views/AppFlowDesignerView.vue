<template>
  <section class="designer-page">
    <AppErrorNotice id="flow-designer-error-notice" :message="noticeError" />

    <AppPromptDialog
      id="deploy-confirmation-dialog"
      ref="deployDialog"
      content-label="Deploy flow confirmation"
      role="alertdialog"
      @confirm="deployFlow"
    >
      <template #prompt="{ cancel, confirm }">
        <section
          class="designer-prompt-dialog"
          aria-labelledby="deploy-title"
          aria-describedby="deploy-description"
          @keydown.esc.prevent="cancel"
        >
          <h2 id="deploy-title">Deploy this flow?</h2>
          <p id="deploy-description">
            The latest saved definition will replace the currently running version.
          </p>
          <div class="designer-prompt-actions">
            <AppButton text="Cancel" :icon="cancelIcon" data-dialog-initial-focus @click="cancel" />
            <AppButton text="Deploy now" :icon="deployIcon" @click="confirm" />
          </div>
        </section>
      </template>
    </AppPromptDialog>

    <AppPromptDialog
      id="revert-confirmation-dialog"
      ref="revertDialog"
      content-label="Revert draft confirmation"
      @confirm="revertDraftToDeployed"
    >
      <template #prompt="{ cancel, confirm }">
        <section
          class="designer-prompt-dialog"
          aria-labelledby="revert-title"
          aria-describedby="revert-description"
          @keydown.esc.prevent="cancel"
        >
          <h2 id="revert-title">Revert this draft?</h2>
          <p id="revert-description">
            All draft changes will be replaced by the currently deployed version.
          </p>
          <div class="designer-prompt-actions">
            <AppButton
              text="Keep draft"
              :icon="cancelIcon"
              data-dialog-initial-focus
              @click="cancel"
            />
            <AppButton text="Revert draft" :icon="discardIcon" @click="confirm" />
          </div>
        </section>
      </template>
    </AppPromptDialog>

    <AppPromptDialog
      id="discard-changes-dialog"
      ref="discardDialog"
      content-label="Discard unsaved flow changes confirmation"
      @cancel="keepEditing"
      @confirm="discardChanges"
    >
      <template #prompt="{ cancel, confirm }">
        <section
          class="designer-prompt-dialog"
          aria-labelledby="discard-title"
          aria-describedby="discard-description"
          @keydown.esc.prevent="cancel"
        >
          <h2 id="discard-title">Discard unsaved changes?</h2>
          <p id="discard-description">This flow has changes that have not been saved.</p>
          <div class="designer-prompt-actions">
            <AppButton
              text="Keep editing"
              :icon="renameFlowIcon"
              data-dialog-initial-focus
              @click="cancel"
            />
            <AppButton text="Discard changes" :icon="discardIcon" @click="confirm" />
          </div>
        </section>
      </template>
    </AppPromptDialog>

    <template v-if="flow">
      <section
        v-if="draftFlow?.deployedRevision"
        class="version-selector"
        aria-label="Flow version"
      >
        <div role="group" aria-label="Version to view">
          <AppButton text="Draft" :disabled="isDraftVersion" @click="showDraftVersion" />
          <AppButton
            text="Deployed"
            :disabled="isDeployedVersion || loadingDeployedVersion"
            @click="showDeployedVersion"
          />
        </div>
        <span v-if="isDeployedVersion" role="status">
          Viewing deployed revision {{ flow.revision }}. This version is read-only.
        </span>
        <AppButton
          v-if="isDraftVersion && draftFlow.status === 'draft'"
          text="Revert draft to deployed"
          :disabled="saving || revertingDraft"
          @click="openRevertConfirmation"
        />
      </section>

      <AppFlowDesignerHeader
        v-model:debug-target-id="debugTargetId"
        :flow="flow"
        :dirty="dirty"
        :compiling="compiling"
        :deploying="deploying"
        :saving="saving"
        :loading="isSpinnerVisible"
        :toggling-disabled="togglingDisabled"
        :runtime-state="runtime?.state"
        :workspace-mode="workspaceMode"
        :version-view="versionView"
        :debug-targets="debugTargets"
        :controller-templates-error="controllerTemplates.error"
        :point-references-valid="pointReferencesValid"
        @save="saveFlow"
        @compile="compileFlow"
        @deploy="openDeployConfirmation"
        @refresh-runtime="refreshRuntime"
        @toggle-disabled="setFlowDisabled"
      />

      <AppFlowWorkspaceNavigation
        :flow-id="props.flowId"
        :workspace-mode="workspaceMode"
        :version-view="versionView"
        :saving="saving"
        :loading="isSpinnerVisible"
        @save="saveFlow"
      />

      <section
        v-if="isDraftVersion"
        class="context-preview"
        aria-label="Execution context validation preview"
      >
        <label>
          <span>Validate against execution context</span>
          <select v-model="selectedContextId" :disabled="contextsLoading">
            <option value="">Flow definitions and global points</option>
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
        v-if="isDraftVersion && compileResult"
        :result="compileResult"
        :node-ids="draftFlow?.nodes.map(({ id }) => id) ?? []"
        @select-diagnostic="focusDiagnosticNode"
      />

      <AppFlowTutorialPanel
        v-if="activeTutorial"
        :tutorial="activeTutorial"
        @[EVENTS.CLOSE]="activeTutorial = undefined"
        @[EVENTS.OPEN_TUTORIAL]="openTutorialExample"
        @[EVENTS.COPY_TUTORIAL]="copyTutorialExample"
      />

      <AppFlowDebugPanel
        v-if="isSimulatorWorkspace || isDebuggerWorkspace"
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
        v-if="showEmulatorPanel"
        :snapshot="emulatorSnapshot"
        @[EVENTS.APPLY_INPUTS_STEP]="applyEmulatorInputsAndStep"
        @[EVENTS.ADVANCE]="advanceEmulator"
        @[EVENTS.FAULT]="setEmulatorFault"
        @[EVENTS.RESET]="resetEmulator"
        @[EVENTS.RESET_INPUTS]="resetEmulatorInputs"
      />

      <div :class="{ 'deployed-version-canvas': isDeployedVersion }">
        <AppFlowDesignerCanvas
          :flow="flow"
          :runtime="canvasRuntime"
          :current-node-id="debugInspection?.nodeId"
          :breakpoints="debugBreakpoints"
          :connector-values="debugConnectorValues"
          :debugging="isDebugging"
          :focus-node-id="diagnosticNodeId"
          :context-point-contracts="selectedContext?.pointContracts"
          :execution-context-id="selectedContextId || undefined"
          :simulator-io="simulatorIo"
          :simulator-mode="isSimulatorWorkspace"
          :show-default-values="showCanvasDefaultValues"
          @point-validation="setPointValidation"
          @[EVENTS.APPLY_INPUTS_STEP]="applySimulatorInputs"
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

    <div v-else-if="!isSpinnerVisible" class="not-found">
      <p>Flow not found</p>
      <h1>There is no flow named “{{ flowId }}”.</h1>
      <RouterLink :to="{ name: 'flows' }">Return to flows</RouterLink>
    </div>
  </section>
</template>

<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue';
import { useSaveShortcut } from '@/composables/useSaveShortcut';
import { useSpinner } from '@/composables/useSpinner';
import { onBeforeRouteLeave, useRouter } from 'vue-router';
import { ROUTE_NAMES } from '@/router';

import { EVENTS } from '@/constants/events';
import cancelIcon from '@/assets/icons/cancel-icon.svg';
import deployIcon from '@/assets/icons/deploy-icon.svg';
import discardIcon from '@/assets/icons/discard-icon.svg';
import renameFlowIcon from '@/assets/icons/rename-flow-icon.svg';
import AppButton from '@/components/AppButton.vue';
import AppErrorNotice from '@/components/AppErrorNotice.vue';
import AppPromptDialog from '@/components/AppPromptDialog.vue';
import AppFlowDesignerCanvas from '@/features/flows/components/AppFlowDesignerCanvas.vue';
import AppFlowCompileResults from '@/features/flows/components/AppFlowCompileResults.vue';
import AppFlowDebugPanel from '@/features/flows/components/AppFlowDebugPanel.vue';
import AppFlowEmulatorPanel from '@/features/flows/components/AppFlowEmulatorPanel.vue';
import AppFlowTutorialPanel from '@/features/flows/components/AppFlowTutorialPanel.vue';
import AppFlowDesignerHeader from '@/features/flows/components/designer/AppFlowDesignerHeader.vue';
import { getFlowDebugTargets } from '@/features/flows/debugTargets';
import {
  createExecutableFlowSource,
  FlowDebugSourceError,
  graphRevision
} from '@/features/flows/flowDebugSource';
import { flowCompileApi, type FlowCompileResult } from '@/features/flows/api/flowCompileApi';
import { useControllerTemplatesStore } from '@/features/controllerTemplates/stores/controllerTemplates';
import { useFlowsStore } from '@/features/flows/stores/flows';
import type { ZOrderCommand } from '@/features/flows/graph/zOrder';
import { FlowApiError, flowApi } from '@/features/flows/api/flowApi';
import { flowRuntimeApi } from '@/features/flows/api/flowRuntimeApi';
import { createLatestRequestGuard } from '@/features/flows/api/latestRequest';
import { useFlowRuntimeStore } from '@/features/flows/stores/flowRuntime';
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
import type { VirtualPointDefinition } from '@/features/flows/types';
import { unconnectedVirtualPoint, virtualPointDefinitionsFromNodes } from '@/features/flows/types';
import { VersionView, WorkspaceMode } from '@/features/flows/types/flowDesigner';
import AppFlowWorkspaceNavigation from '@/features/flows/components/designer/AppFlowWorkspaceNavigation.vue';
import { useRuntimeContext } from '@/features/flows/composables/useRuntimeContext';

const props = defineProps<{
  flowId: string;
  workspaceMode: WorkspaceMode;
}>();

const flowStore = useFlowsStore();
const { isSpinnerVisible, withSpinner } = useSpinner();
const runtimeStore = useFlowRuntimeStore();
const workspaceMode = computed(() => props.workspaceMode);
const flowId = computed(() => props.flowId);
const activeTutorial = ref<FlowTutorial>();
const controllerTemplates = useControllerTemplatesStore();
const router = useRouter();
const draftFlow = computed(() => flowStore.findFlow(props.flowId));
const deployedFlow = ref<FlowDefinition>();
const versionView = ref(VersionView.Draft);
const loadingDeployedVersion = ref(false);
const revertingDraft = ref(false);
const compiling = ref(false);
const compileResult = ref<FlowCompileResult>();
const compiledGraphRevision = ref<number>();
const flow = computed(() =>
  versionView.value === VersionView.Deployed ? deployedFlow.value : draftFlow.value
);
const isDraftVersion = computed(() => versionView.value === VersionView.Draft);
const isDeployedVersion = computed(() => versionView.value === VersionView.Deployed);
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
const mergedPointDefinitions = computed(() => {
  const result = new Map<string, VirtualPointDefinition>();
  for (const definition of [
    ...(selectedContext.value?.pointContracts ?? []),
    ...virtualPointDefinitionsFromNodes(flow.value?.nodes ?? [])
  ])
    result.set(definition.key, definition);
  return [...result.values()];
});
const pointReferencesValid = computed(() => {
  const nodes = flow.value?.nodes.filter(isPointNode) ?? [];
  return nodes.every((node) => pointValidation.value[node.id] === 'valid');
});
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
const deployDialog = ref<InstanceType<typeof AppPromptDialog>>();
const revertDialog = ref<InstanceType<typeof AppPromptDialog>>();
const discardDialog = ref<InstanceType<typeof AppPromptDialog>>();
const runtime = computed(() => runtimeStore.snapshotFor(props.flowId));
const debugTargets = computed(() => getFlowDebugTargets(controllerTemplates.allItems));
const debugTargetId = ref('server');
const diagnosticNodeId = ref<string>();

const deploying = computed(() => runtimeStore.isDeploying(props.flowId));
let loadController: AbortController | undefined;
const loadGuard = createLatestRequestGuard();
const pendingRoute = ref<string>();
let allowNavigation = false;

watch(debugTargets, (targets) => {
  if (!targets.some((target) => target.id === debugTargetId.value)) debugTargetId.value = 'server';
});

const flowRevision = computed(() => (flow.value ? graphRevision(flow.value) : 1));
watch(flowRevision, (revision) => {
  if (compiledGraphRevision.value !== undefined && revision !== compiledGraphRevision.value) {
    compileResult.value = undefined;
    compiledGraphRevision.value = undefined;
  }
});
const selectedDebugTarget = computed(() =>
  debugTargets.value.find((target) => target.id === debugTargetId.value)
);
const execution = useRuntimeContext({
  flowId,
  flow,
  revision: computed(() => flow.value?.revision ?? 1),
  deployedRuntime: runtime
});
const isSimulatorWorkspace = computed(() => workspaceMode.value === WorkspaceMode.Simulator);
const isDebuggerWorkspace = computed(() => workspaceMode.value === WorkspaceMode.Debugger);
const canvasRuntime = execution.canvasRuntime;
const simulatorIo = execution.io;
const showCanvasDefaultValues = computed(
  () => isSimulatorWorkspace.value || isDebuggerWorkspace.value
);
const isDebugging = computed(() =>
  ['ready', 'running', 'paused', 'stepping'].includes(execution.lifecycle.value)
);
const debugConnectorValues = computed(() => undefined);
const debugLifecycle = computed(() =>
  execution.lifecycle.value === 'faulted'
    ? 'fault'
    : execution.lifecycle.value === 'preparing'
      ? 'loading'
      : execution.lifecycle.value === 'stale'
        ? 'stopped'
        : execution.lifecycle.value
);
const debugSnapshot = execution.runtime;
const debugSnapshotStale = computed(() => execution.lifecycle.value === 'stale');
const debugError = execution.error;
const debugHost = computed<'server' | 'emulator' | 'controller'>(() => {
  const kind = selectedDebugTarget.value?.kind;
  return kind === 'emulator' || kind === 'controller' ? kind : 'server';
});
const debugCapabilities = computed(() =>
  execution.capabilities.value
    ? {
        stepTick: execution.capabilities.value.canStepTick,
        stepNode: execution.capabilities.value.canStepNode,
        stepInstruction: execution.capabilities.value.canStepInstruction,
        continue: execution.capabilities.value.canRun,
        pause: execution.capabilities.value.canPause,
        runTo: execution.capabilities.value.canRunTo,
        maximumBreakpoints: 32,
        maximumInspectableSlots: 256
      }
    : undefined
);
const debugInspection = execution.inspection;
const debugExecutionOrder = computed<string[]>(() => []);
const debugBreakpoints = execution.breakpoints;
const debugAffectedOutputPoints = computed(
  () => execution.context.value?.io?.liveOutputPointIds ?? []
);
const debugLiveOutputEnabled = computed(
  () => execution.context.value?.io?.liveOutputEnabled ?? false
);
const debugLiveOutputPriority = computed(() => undefined);
const debugLiveOutputHoldMilliseconds = computed(() => undefined);
const emulatorSnapshot = computed(() => undefined);
const showEmulatorPanel = computed(() => false);
const createExecution = async (): Promise<void> => {
  await saveFlow();
  if (saveError.value) return;
  await execution.create({
    mode: isSimulatorWorkspace.value ? 'simulator' : 'debugger',
    expectedRevision: flow.value?.revision ?? 1,
    targetId: isSimulatorWorkspace.value ? 'server' : debugTargetId.value,
    replaceExisting: true,
    breakpoints: debugBreakpoints.value
  });
};
const loadDebugSession = createExecution;
const applySimulatorInputs = execution.applyInputsAndStep;
const stepDebugSession = execution.stepTick;
const stepNodeDebugSession = execution.stepNode;
const stepInstructionDebugSession = execution.stepInstruction;
const runDebugSession = execution.run;
const runToBreakpoint = async (): Promise<void> => {
  const value = debugBreakpoints.value[0];
  if (value) await execution.runTo(value);
};
const pauseDebugSession = execution.pause;
const stopDebugSession = execution.stop;
const restartDebugSession = execution.restart;
const enableLiveOutput = execution.enableLiveOutput;
const applyEmulatorInputsAndStep = execution.applyInputsAndStep;
const advanceEmulator = execution.advance;
const setEmulatorFault = execution.injectFault;
const resetEmulator = execution.resetIo;
const resetEmulatorInputs = execution.resetInputs;
const setBreakpoint = execution.setBreakpoint;
const runToNode = execution.runToNode;

watch(debugTargetId, () => {
  if (execution.contextId.value) void execution.stop();
});

const compileFlow = async (): Promise<void> => {
  const current = draftFlow.value;
  const target = debugTargets.value.find((item) => item.id === 'server');
  if (!current || !target) return;
  try {
    await withSpinner(
      () => {
        compiling.value = true;
        saveError.value = undefined;
      },
      () => flowCompileApi.compile(createExecutableFlowSource(current, target)),
      (result) => {
        compileResult.value = result;
        compiledGraphRevision.value = graphRevision(current);
        const firstPath = result.diagnostics[0]?.path ?? '';
        const match = /^\/nodes\/(\d+)(?:\/|$)/.exec(firstPath);
        if (match) diagnosticNodeId.value = current.nodes[Number(match[1])]?.id;
      }
    );
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

const focusDiagnosticNode = (nodeId: string): void => {
  diagnosticNodeId.value = nodeId;
};
const openRevertConfirmation = (): void => {
  revertDialog.value?.showModal();
};

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
    await withSpinner(
      null,
      async () => {
        const created = await flowApi.createFlow(`${tutorial.title} copy`);
        await flowApi.saveFlow(
          flowDomainToDto({
            ...tutorial.flow,
            id: created.id,
            name: created.name,
            updatedAt: created.updatedAt
          })
        );
        return created;
      },
      async (created) => {
        await router.push({ name: 'flow-designer', params: { flowId: created.id } });
      }
    );
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
        mergedPointDefinitions.value,
        controller.signal,
        selectedContextId.value || undefined
      )
    )
  ).catch(() => undefined);
  if (!results || controller.signal.aborted) return false;
  nodes.forEach((node, index) => (pointValidation.value[node.id] = results[index]!.state));
  return results.every(({ state }) => state === 'valid');
};

const loadExecutionContexts = async (): Promise<void> => {
  try {
    await withSpinner(
      () => {
        contextsLoading.value = true;
        contextsError.value = '';
      },
      () => executionContextApi.list(),
      (contexts) => {
        executionContexts.value = contexts;
        const containing = contexts.find((context) =>
          context.programs.some(({ flowId }) => flowId === props.flowId)
        );
        if (!selectedContextId.value && containing) selectedContextId.value = containing.id;
      }
    );
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
  const controller = new AbortController();
  const requestGeneration = loadGuard.begin();
  try {
    await withSpinner(
      () => {
        loadController?.abort();
        loadController = controller;
        versionView.value = VersionView.Draft;
        deployedFlow.value = undefined;
        loadError.value = undefined;
        flowStore.selectFlow(flowId);
      },
      () => flowApi.getFlow(flowId, controller.signal),
      async (payload) => {
        if (!loadGuard.isCurrent(requestGeneration)) return;
        flowStore.replaceFlowFromPayload(payload);
        flowStore.selectFlow(flowId);
        await validateAllPointReferences();
        void refreshRuntime(flowId);
      }
    );
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
    if (loadController === controller) {
      loadController = undefined;
    }
  }
};

const refreshRuntime = async (flowId = props.flowId): Promise<void> => {
  try {
    await withSpinner(
      () => {
        runtimeError.value = undefined;
      },
      () => flowRuntimeApi.getRuntime(flowId),
      (snapshot) => {
        if (snapshot.flowId !== flowId) throw new Error('Runtime state belongs to another flow.');
        runtimeStore.applySnapshot(snapshot);
      }
    );
  } catch (error) {
    runtimeStore.disconnect(flowId);
    runtimeError.value = runtimeFailureMessage(error, 'Unable to load runtime state.');
  }
};

const openDeployConfirmation = (): void => {
  deployDialog.value?.showModal();
};

const deployFlow = async (): Promise<void> => {
  try {
    await withSpinner(
      () => {
        runtimeStore.beginDeployment(props.flowId);
        runtimeError.value = undefined;
      },
      () => flowRuntimeApi.deployFlow(props.flowId),
      async (snapshot) => {
        if (snapshot.flowId !== props.flowId)
          throw new Error('Runtime state belongs to another flow.');
        runtimeStore.completeDeployment(snapshot);
        flowStore.replaceFlowFromPayload(await flowApi.getFlow(props.flowId));
      }
    );
  } catch (error) {
    const message = runtimeFailureMessage(error, 'Unable to deploy this flow.');
    runtimeStore.failDeployment(props.flowId, message);
    runtimeError.value = message;
  }
};

const showDraftVersion = (): void => {
  versionView.value = VersionView.Draft;
};

const showDeployedVersion = async (): Promise<void> => {
  try {
    await withSpinner(
      () => {
        loadingDeployedVersion.value = true;
        saveError.value = undefined;
      },
      () => flowApi.getDeployedFlow(props.flowId),
      (result) => {
        deployedFlow.value = flowDtoToDomain(result);
        versionView.value = VersionView.Deployed;
      }
    );
  } catch (error) {
    saveError.value = runtimeFailureMessage(error, 'Unable to load the deployed version.');
  } finally {
    loadingDeployedVersion.value = false;
  }
};

const revertDraftToDeployed = async (): Promise<void> => {
  try {
    await withSpinner(
      () => {
        revertingDraft.value = true;
        saveError.value = undefined;
      },
      () => flowApi.revertToDeployed(props.flowId),
      (result) => {
        flowStore.replaceFlowFromPayload(result);
        versionView.value = VersionView.Draft;
      }
    );
  } catch (error) {
    saveError.value = runtimeFailureMessage(error, 'Unable to revert the draft.');
  } finally {
    revertingDraft.value = false;
  }
};

const setFlowDisabled = async (disabled: boolean): Promise<void> => {
  try {
    await withSpinner(
      () => {
        togglingDisabled.value = true;
        runtimeError.value = undefined;
      },
      () => flowApi.setFlowDisabled(props.flowId, disabled),
      async (saved) => {
        flowStore.replaceFlowFromPayload(saved);
        await refreshRuntime();
      }
    );
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
  try {
    const disconnectedVirtual = unconnectedVirtualPoint(payload);
    if (disconnectedVirtual) {
      diagnosticNodeId.value = disconnectedVirtual.id;
      throw new Error(
        `${disconnectedVirtual.label} must have its Set input, Value output, or both connected.`
      );
    }
    await withSpinner(
      () => {
        saving.value = true;
        saveError.value = undefined;
      },
      () => flowApi.saveFlow(payload),
      (saved) => {
        flowStore.replaceFlowFromPayload(saved);
      }
    );
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
    if (previous !== undefined && flowId !== previous) void execution.stop(true);
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
  void execution.stop(true);
});

const handleBeforeUnload = (event: BeforeUnloadEvent): void => {
  void execution.stop(true);
  if (!dirty.value) return;
  // Browsers show their own confirmation wording for tab close and page refresh.
  // Setting returnValue is still required by browsers that support this prompt.
  event.preventDefault();
  event.returnValue = '';
};

const keepEditing = (): void => {
  pendingRoute.value = undefined;
};

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
    void execution.stop(true);
    return true;
  }
  pendingRoute.value = to.fullPath;
  discardDialog.value?.showModal();
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
  overflow-y: auto;
  flex-direction: column;
  scrollbar-gutter: stable;
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
  gap: var(--space-5-5);
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

.designer-prompt-dialog {
  width: min(430px, 100%);
}

.designer-prompt-dialog h2 {
  margin: var(--space-0);
  color: var(--color-text-primary);
}

.designer-prompt-dialog p {
  color: var(--color-text-muted);
}

.designer-prompt-actions {
  display: flex;
  gap: var(--space-3-5);
  justify-content: end;
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
