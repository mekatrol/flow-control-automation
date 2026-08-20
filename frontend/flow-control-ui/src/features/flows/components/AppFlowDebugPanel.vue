<template>
  <section v-bind="automation()" class="debug-panel" aria-label="Flow debugging">
    <div class="debug-controls">
      <strong>{{ hostLabel }} debug</strong>
      <span class="mode">{{
        host === 'controller' ? 'Shadow outputs by default' : 'Server-hosted execution'
      }}</span>
      <AppButton
        v-bind="automation('load')"
        text="Load"
        :icon="loadIcon"
        :disabled="!canLoad"
        @click="emit('load')"
      />
      <AppButton
        v-bind="automation('step')"
        text="Step tick"
        :icon="stepIcon"
        :disabled="!canStepTick"
        @click="emit('stepTick')"
      />
      <AppButton
        v-bind="automation('run-to-boundary')"
        text="Run to tick boundary"
        :icon="stepIcon"
        :disabled="!canStepTick"
        @click="emit(EVENTS.RUN_TO_BOUNDARY)"
      />
      <AppButton
        v-bind="automation('step-node')"
        text="Step node"
        :icon="stepNodeIcon"
        :disabled="!canStepNode"
        @click="emit('stepNode')"
      />
      <AppButton
        v-bind="automation('step-instruction')"
        text="Step instruction"
        :icon="stepInstructionIcon"
        :disabled="!canStepInstruction"
        @click="emit('stepInstruction')"
      />
      <AppButton
        v-bind="automation('run')"
        text="Run"
        :icon="playIcon"
        :disabled="!canRun"
        @click="emit('run')"
      />
      <AppButton
        v-bind="automation('run-to')"
        text="Run to breakpoint"
        :icon="breakpointIcon"
        :disabled="!canRunTo"
        @click="emit('runTo')"
      />
      <AppButton
        v-bind="automation('pause')"
        text="Pause"
        :icon="pauseIcon"
        :disabled="!canPause"
        @click="emit('pause')"
      />
      <AppButton
        v-bind="automation('stop')"
        text="Stop"
        :icon="stopIcon"
        :disabled="!canStop"
        @click="emit('stop')"
      />
      <AppButton
        v-bind="automation('restart')"
        text="Restart"
        :icon="refreshIcon"
        :disabled="!canRestart"
        @click="emit('restart')"
      />
      <span class="state" :class="{ stale }" role="status">{{ stateLabel }}</span>
    </div>
    <div v-if="affectedOutputPoints.length" class="live-output">
      <strong>Live physical outputs</strong>
      <p>Affected points: {{ affectedOutputPoints.join(', ') }}</p>
      <template v-if="!liveOutputEnabled">
        <label>
          <input v-model="liveOutputConfirmed" type="checkbox" />
          I confirm these named outputs may energise physical equipment.
        </label>
        <AppButton
          v-bind="automation('enable-live-output')"
          text="Enable live outputs"
          :icon="enableFlowIcon"
          :disabled="!canEnableLiveOutput"
          @click="emit('enableLiveOutput', affectedOutputPoints)"
        />
      </template>
      <strong v-else class="live-warning" role="status">
        LIVE OUTPUT ENABLED — priority {{ liveOutputPriority }}, {{ liveOutputHoldMilliseconds }} ms
        expiry
      </strong>
    </div>
    <p v-if="error" class="debug-error" role="alert">{{ error }}</p>
    <section
      v-if="executionOrder.length || breakpoints.length"
      class="execution-summary"
      aria-label="Execution and breakpoint summary"
    >
      <h3>Execution order</h3>
      <ol>
        <li v-for="nodeId in executionOrder" :key="nodeId">{{ nodeId }}</li>
      </ol>
      <h3>Breakpoints</h3>
      <ul>
        <li v-for="breakpoint in breakpoints" :key="`${breakpoint.nodeId}:${breakpoint.position}`">
          {{ breakpoint.position }} {{ breakpoint.nodeId }}
        </li>
      </ul>
    </section>
    <div v-if="inspection" class="inspection" aria-label="Paused execution frame">
      <strong>Frame {{ inspection.instructionPointer }}</strong>
      <span>Node {{ inspection.nodeId ?? 'commit' }}</span>
      <span>{{ inspection.isAtCommit ? 'At tick boundary' : 'Uncommitted' }}</span>
      <span>Slots: {{ inspection.slots.map((slot) => slot.value).join(', ') || 'none' }}</span>
      <span>
        Current state: {{ inspection.currentState.map((slot) => slot.value).join(', ') || 'none' }}
      </span>
      <span>
        Next state:
        {{ inspection.stagedNextState.map((slot) => slot?.value ?? '—').join(', ') || 'none' }}
      </span>
    </div>
    <div v-if="snapshot" class="snapshot" :class="{ stale }">
      <span>Tick {{ snapshot.tickNumber }}</span>
      <span>{{ snapshot.executionDurationUs }} µs</span>
      <span>High-water {{ snapshot.executionHighWaterUs ?? snapshot.executionDurationUs }} µs</span>
      <span>Missed deadlines {{ snapshot.missedDeadlineCount ?? 0 }}</span>
      <span>Overruns {{ snapshot.overrunCount }}</span>
      <span>Evaluation failures {{ snapshot.evaluationFailureCount }}</span>
      <span>Arbitration losses {{ snapshot.arbitrationLossCount ?? 0 }}</span>
      <span>Input {{ snapshot.inputValidity.join(', ') || 'unavailable' }}</span>
      <span v-if="stale">Stale snapshot — graph revision changed</span>
      <span v-else>Current shadow snapshot</span>
      <span v-if="snapshot.lastReason">{{ snapshot.lastReason }}</span>
      <button
        v-if="snapshot.lastReasonPath"
        type="button"
        class="diagnostic-link"
        @click="emit(EVENTS.SELECT_DIAGNOSTIC, diagnosticNodeId)"
      >
        <svg aria-hidden="true" viewBox="0 0 24 24">
          <path d="M4 12h14M14 8l4 4-4 4M20 5v14" />
        </svg>
        Go to affected node {{ diagnosticNodeId }}
      </button>
      <ul v-if="snapshot.proposedOutputs.length" aria-label="Proposed non-physical outputs">
        <li v-for="output in snapshot.proposedOutputs" :key="output.pointId">
          {{ output.pointId }}: {{ output.proposedValue }} ({{ output.quality }}) — proposed only
        </li>
      </ul>
    </div>
  </section>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue';
import breakpointIcon from '@/assets/icons/breakpoint-icon.svg';
import enableFlowIcon from '@/assets/icons/enable-flow-icon.svg';
import loadIcon from '@/assets/icons/flow-debug-icon.svg';
import pauseIcon from '@/assets/icons/pause-icon.svg';
import playIcon from '@/assets/icons/play-icon.svg';
import refreshIcon from '@/assets/icons/refresh-icon.svg';
import stepIcon from '@/assets/icons/step-icon.svg';
import stepInstructionIcon from '@/assets/icons/step-instruction-icon.svg';
import stepNodeIcon from '@/assets/icons/step-node-icon.svg';
import stopIcon from '@/assets/icons/stop-icon.svg';
import AppButton from '@/components/AppButton.vue';
import { useAutomation } from '@/composables/useAutomation';
import { EVENTS } from '@/constants/events';
import type {
  DebugRuntimeSnapshot,
  FlowDebugCapabilities,
  FlowDebugInspection
} from '@/features/flows/api/flowDebugApi';
import type { FlowDebugBreakpoint } from '@/features/flows/api/flowDebugApi';

const props = defineProps<{
  automation: string;
  lifecycle: 'idle' | 'loading' | 'ready' | 'stepping' | 'running' | 'paused' | 'fault' | 'stopped';
  snapshot?: DebugRuntimeSnapshot;
  stale?: boolean;
  error?: string;
  targetAvailable: boolean;
  affectedOutputPoints?: string[];
  liveOutputEnabled?: boolean;
  liveOutputPriority?: number;
  liveOutputHoldMilliseconds?: number;
  host?: 'server' | 'emulator' | 'controller';
  capabilities?: FlowDebugCapabilities;
  inspection?: FlowDebugInspection;
  executionOrder?: string[];
  breakpoints?: FlowDebugBreakpoint[];
}>();
const emit = defineEmits<{
  (
    event:
      | 'load'
      | 'stepTick'
      | 'stepNode'
      | 'stepInstruction'
      | 'run'
      | 'runTo'
      | 'pause'
      | 'stop'
      | 'restart'
  ): void;
  (event: 'enableLiveOutput', pointIds: string[]): void;
  (event: typeof EVENTS.RUN_TO_BOUNDARY): void;
  (event: typeof EVENTS.SELECT_DIAGNOSTIC, nodeId: string): void;
}>();
const automation = useAutomation(props.automation);
const busy = computed(() => props.lifecycle === 'loading' || props.lifecycle === 'stepping');
const active = computed(() => ['ready', 'running', 'paused', 'fault'].includes(props.lifecycle));
const canLoad = computed(() => props.targetAvailable && !busy.value && !active.value);
const host = computed(() => props.host ?? 'controller');
const hostLabel = computed(() => `${host.value.charAt(0).toUpperCase()}${host.value.slice(1)}`);
const canStepTick = computed(
  () =>
    props.capabilities?.stepTick !== false &&
    !props.stale &&
    ['ready', 'paused'].includes(props.lifecycle)
);
const canStepNode = computed(() => props.capabilities?.stepNode === true && canStepTick.value);
const canStepInstruction = computed(
  () => props.capabilities?.stepInstruction === true && canStepTick.value
);
const canRun = computed(
  () =>
    props.capabilities?.continue !== false &&
    !props.stale &&
    ['ready', 'paused'].includes(props.lifecycle)
);
const canRunTo = computed(() => props.capabilities?.runTo === true && canRun.value);
const canPause = computed(
  () => props.capabilities?.pause !== false && props.lifecycle === 'running'
);
const canStop = computed(() => active.value || busy.value);
const canRestart = computed(() => active.value && !busy.value);
const affectedOutputPoints = computed(() => props.affectedOutputPoints ?? []);
const liveOutputConfirmed = ref(false);
const canEnableLiveOutput = computed(
  () => liveOutputConfirmed.value && !props.stale && ['ready', 'paused'].includes(props.lifecycle)
);
const stateLabel = computed(() => (props.stale ? 'stale' : props.lifecycle));
const executionOrder = computed(() => props.executionOrder ?? []);
const breakpoints = computed(() => props.breakpoints ?? []);
const diagnosticNodeId = computed(() => {
  const path = props.snapshot?.lastReasonPath ?? '';
  const match = path.match(/\/nodes\/([^/]+)/);
  return match?.[1] ?? path;
});
</script>

<style scoped>
.debug-panel {
  margin-bottom: var(--space-4);
  border: var(--border-width-default) solid var(--color-border-subtle);
  border-radius: var(--radius-lg);
  background: var(--color-surface-subtle);
}
.debug-controls {
  display: flex;
  flex-wrap: wrap;
  gap: var(--space-3);
  align-items: center;
  padding: var(--space-3);
}
.mode {
  color: var(--color-warning-text);
  font-weight: var(--font-weight-semibold);
}
.state {
  margin-left: auto;
  text-transform: uppercase;
}
.state.stale,
.debug-error {
  color: var(--color-danger-text);
}
.live-output {
  padding: var(--space-3);
  border-top: var(--border-width-default) solid var(--color-warning-text);
}
.live-warning {
  color: var(--color-danger-text);
}
.debug-error {
  margin: 0;
  padding: var(--space-3);
  background: var(--color-danger-surface);
}
.snapshot {
  display: flex;
  flex-wrap: wrap;
  gap: var(--space-5);
  padding: var(--space-3);
  border-top: var(--border-width-default) solid var(--color-border-subtle);
}
.inspection {
  display: flex;
  flex-wrap: wrap;
  gap: var(--space-4);
  padding: var(--space-3);
  border-top: var(--border-width-default) solid var(--color-border-subtle);
}
.snapshot.stale {
  opacity: 0.65;
}
.snapshot ul {
  width: 100%;
  margin: 0;
}
.diagnostic-link {
  display: inline-flex;
  gap: var(--space-2);
  align-items: center;
}
.diagnostic-link svg {
  width: 1.125rem;
  height: 1.125rem;
  fill: none;
  stroke: currentcolor;
  stroke-width: var(--stroke-width-standard);
  stroke-linecap: round;
  stroke-linejoin: round;
}
</style>
