<template>
  <section v-bind="automation()" class="simulator-panel" aria-labelledby="simulator-title">
    <header class="simulator-heading">
      <div>
        <p class="eyebrow">Test workspace</p>
        <h2 id="simulator-title">Simulator</h2>
        <p class="description">Run the current draft safely with shadow outputs.</p>
      </div>
      <span class="state" :class="lifecycle" role="status" aria-live="polite"
        ><span class="state-dot" aria-hidden="true"></span>{{ stateLabel }}</span
      >
    </header>
    <div class="safety-note">
      <strong>Isolated from physical equipment</strong
      ><span>No commands from this workspace are sent to connected controllers.</span>
    </div>
    <div class="command-bar" role="group" aria-label="Simulation controls">
      <div class="command-group primary-actions">
        <AppButton
          v-bind="automation('start')"
          :text="
            lifecycle === 'compiling'
              ? 'Compiling…'
              : active
                ? 'Recompile draft'
                : 'Start simulation'
          "
          :disabled="!canStart"
          @click="emit(EVENTS.START_SIMULATION)"
        />
        <AppButton
          v-bind="automation('run')"
          text="Run continuously"
          :disabled="!canExecute"
          @click="emit(EVENTS.RUN)"
        />
        <AppButton
          v-bind="automation('pause')"
          text="Pause"
          :disabled="lifecycle !== 'running'"
          @click="emit(EVENTS.PAUSE)"
        />
      </div>
      <div class="command-group step-actions">
        <span class="group-label">Step</span>
        <AppButton
          v-bind="automation('step-tick')"
          text="One scan"
          :disabled="!canExecute"
          @click="emit(EVENTS.STEP_TICK)"
        />
        <AppButton
          v-bind="automation('step-mode')"
          text="Node"
          :disabled="!canStepNode"
          @click="emit(EVENTS.STEP_NODE)"
        />
        <AppButton
          v-bind="automation('step-instruction')"
          text="Instruction"
          :disabled="!canStepInstruction"
          @click="emit(EVENTS.STEP_INSTRUCTION)"
        />
      </div>
      <div class="command-group session-actions">
        <AppButton
          v-bind="automation('restart')"
          text="Restart"
          :disabled="!active || lifecycle === 'running'"
          @click="emit(EVENTS.RESTART)"
        />
        <AppButton
          v-bind="automation('stop')"
          text="Stop"
          :disabled="!active"
          @click="emit(EVENTS.STOP_SIMULATION)"
        />
      </div>
    </div>
    <p v-if="lifecycle === 'stale'" class="message error" role="alert">
      The draft changed. Start simulation again to compile the current graph.
    </p>
    <p v-if="error" class="message error" role="alert">{{ error }}</p>
    <dl v-if="session" class="summary">
      <div>
        <dt>Scan</dt>
        <dd>{{ session.snapshot?.tickNumber ?? session.io?.scanNumber ?? 0 }}</dd>
      </div>
      <div>
        <dt>Virtual time</dt>
        <dd>{{ session.io?.virtualTimeMilliseconds ?? 0 }} ms</dd>
      </div>
      <div>
        <dt>Session expires</dt>
        <dd>{{ Math.ceil(session.leaseRemainingMilliseconds / 60000) }} min</dd>
      </div>
      <div class="revision">
        <dt>Compiled revision</dt>
        <dd :title="session.sourceDigest">{{ session.sourceDigest.slice(0, 12) }}</dd>
      </div>
    </dl>
    <AppFlowEmulatorPanel
      v-if="session?.io"
      v-bind="automation('io')"
      :snapshot="session.io"
      :flow-interface="flowInterface"
      @[EVENTS.APPLY_INPUTS_STEP]="forwardInputs"
      @[EVENTS.ADVANCE]="forwardAdvance"
      @[EVENTS.FAULT]="forwardFault"
      @[EVENTS.RESET]="forwardReset"
      @[EVENTS.RESET_INPUTS]="emit(EVENTS.RESET_INPUTS)"
    />
    <details v-if="session" class="advanced">
      <summary>Debugger details</summary>
      <div class="debug-details">
        <span>Instruction: {{ session.inspection?.instructionPointer ?? 'scan boundary' }}</span
        ><span>Breakpoints: {{ session.breakpoints.length }}</span>
      </div>
    </details>
  </section>
</template>

<script setup lang="ts">
import { computed } from 'vue';
import AppButton from '@/components/AppButton.vue';
import AppFlowEmulatorPanel from '@/features/flows/components/AppFlowEmulatorPanel.vue';
import { useAutomation } from '@/composables/useAutomation';
import { EVENTS } from '@/constants/events';
import type { SimulatorLifecycle, SimulatorSession } from '@/features/flows/api/flowSimulatorApi';
import type { EmulatorInputChange } from '@/features/flows/api/flowEmulatorApi';
import type { FlowInterface } from '@/features/flows/types';

const props = withDefaults(
  defineProps<{
    automation: string;
    lifecycle: SimulatorLifecycle;
    session?: SimulatorSession;
    error?: string;
    flowInterface?: FlowInterface;
  }>(),
  { session: undefined, error: undefined, flowInterface: undefined }
);
const emit = defineEmits<{
  (
    event:
      | typeof EVENTS.START_SIMULATION
      | typeof EVENTS.STEP_TICK
      | typeof EVENTS.STEP_NODE
      | typeof EVENTS.STEP_INSTRUCTION
      | typeof EVENTS.STOP_SIMULATION
      | typeof EVENTS.RUN
      | typeof EVENTS.PAUSE
      | typeof EVENTS.RESTART
      | typeof EVENTS.RESET_INPUTS
  ): void;
  (event: typeof EVENTS.APPLY_INPUTS_STEP, inputs: EmulatorInputChange[]): void;
  (event: typeof EVENTS.ADVANCE, milliseconds: number): void;
  (event: typeof EVENTS.FAULT, fault: string | null): void;
  (event: typeof EVENTS.RESET, powerCycle: boolean): void;
}>();
const automation = useAutomation(props.automation);
const flowInterface = computed<FlowInterface>(
  () => props.flowInterface ?? { schemaVersion: 1, inputs: [], outputs: [] }
);
const forwardInputs = (inputs: EmulatorInputChange[]): void =>
  emit(EVENTS.APPLY_INPUTS_STEP, inputs);
const forwardAdvance = (milliseconds: number): void => emit(EVENTS.ADVANCE, milliseconds);
const forwardFault = (fault: string | null): void => emit(EVENTS.FAULT, fault);
const forwardReset = (powerCycle: boolean): void => emit(EVENTS.RESET, powerCycle);
const active = computed(() =>
  ['ready', 'running', 'paused', 'faulted', 'stale'].includes(props.lifecycle)
);
const canStart = computed(() => !['compiling', 'running'].includes(props.lifecycle));
const canExecute = computed(() => ['ready', 'paused'].includes(props.lifecycle));
const canStepNode = computed(
  () => canExecute.value && props.session?.capabilities.stepNode === true
);
const canStepInstruction = computed(
  () => canExecute.value && props.session?.capabilities.stepInstruction === true
);
const stateLabel = computed(
  () => props.lifecycle.charAt(0).toUpperCase() + props.lifecycle.slice(1)
);
</script>

<style scoped>
.simulator-panel {
  max-height: min(38dvh, 26rem);
  flex: 0 1 auto;
  margin-bottom: var(--space-4);
  overflow: hidden auto;
  scrollbar-gutter: stable;
  border: var(--border-width-default) solid var(--color-border-subtle);
  border-radius: var(--radius-lg);
  background: var(--color-surface-subtle);
}
.simulator-heading {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: var(--space-4);
  padding: var(--space-6-5);
  background: var(--color-surface-raised);
}
.eyebrow {
  margin: 0 0 var(--space-2);
  color: var(--color-text-muted);
  font-size: var(--font-size-sm);
  font-weight: var(--font-weight-semibold);
  letter-spacing: 0.08em;
  text-transform: uppercase;
}
.simulator-heading h2 {
  margin: 0;
  font-size: var(--font-size-xl);
}
.description {
  margin: var(--space-2) 0 0;
  color: var(--color-text-muted);
}
.state {
  display: inline-flex;
  gap: var(--space-2);
  align-items: center;
  padding: var(--space-2) var(--space-4);
  font-weight: var(--font-weight-semibold);
  background: var(--color-surface-subtle);
  border: var(--border-width-default) solid var(--color-border-subtle);
  border-radius: 999px;
}
.state-dot {
  width: 0.55rem;
  height: 0.55rem;
  background: currentcolor;
  border-radius: 50%;
}
.state.ready,
.state.running,
.state.paused {
  color: var(--color-success-text);
}
.state.stale,
.state.faulted,
.error {
  color: var(--color-danger-text);
}
.safety-note {
  display: flex;
  flex-wrap: wrap;
  gap: var(--space-2) var(--space-4);
  padding: var(--space-3) var(--space-6-5);
  color: var(--color-text-muted);
  border-top: var(--border-width-default) solid var(--color-border-subtle);
  border-bottom: var(--border-width-default) solid var(--color-border-subtle);
}
.safety-note strong {
  color: var(--color-text-primary);
}
.command-bar {
  display: flex;
  flex-wrap: wrap;
  gap: var(--space-4);
  align-items: stretch;
  padding: var(--space-4) var(--space-6-5);
}
.command-group {
  display: flex;
  flex-wrap: wrap;
  gap: var(--space-2);
  align-items: center;
}
.command-group + .command-group {
  padding-left: var(--space-4);
  border-left: var(--border-width-default) solid var(--color-border-subtle);
}
.group-label {
  width: 100%;
  color: var(--color-text-muted);
  font-size: var(--font-size-sm);
  font-weight: var(--font-weight-semibold);
}
.session-actions {
  margin-left: auto;
}
.summary {
  display: grid;
  grid-template-columns: repeat(4, minmax(8rem, 1fr));
  gap: var(--space-3);
  padding: 0 var(--space-6-5) var(--space-4);
  margin: 0;
}
.summary div {
  padding: var(--space-3) var(--space-4);
  background: var(--color-surface-raised);
  border: var(--border-width-default) solid var(--color-border-subtle);
  border-radius: var(--radius-lg);
}
.summary dt {
  color: var(--color-text-muted);
  font-size: var(--font-size-sm);
}
.summary dd {
  margin: var(--space-2) 0 0;
  font-size: var(--font-size-lg);
  font-weight: var(--font-weight-semibold);
}
.revision dd {
  overflow: hidden;
  font-family: monospace;
  text-overflow: ellipsis;
}
.message {
  margin: 0 var(--space-6-5) var(--space-4);
  padding: var(--space-3) var(--space-4);
  background: var(--color-surface-raised);
  border-left: 3px solid currentcolor;
}
.advanced {
  margin: 0 var(--space-6-5) var(--space-4);
}
.advanced summary {
  padding: var(--space-3) 0;
  cursor: pointer;
  font-weight: var(--font-weight-semibold);
}
.debug-details {
  display: flex;
  gap: var(--space-6-5);
  color: var(--color-text-muted);
}
@media (max-width: 60rem) {
  .summary {
    grid-template-columns: repeat(2, 1fr);
  }
  .session-actions {
    margin-left: 0;
  }
}
@media (max-width: 40rem) {
  .simulator-panel {
    max-height: min(44dvh, 24rem);
  }
  .simulator-heading {
    padding: var(--space-4);
  }
  .command-bar,
  .summary {
    padding-right: var(--space-4);
    padding-left: var(--space-4);
  }
  .command-group + .command-group {
    width: 100%;
    padding-top: var(--space-3);
    padding-left: 0;
    border-top: var(--border-width-default) solid var(--color-border-subtle);
    border-left: 0;
  }
  .summary {
    grid-template-columns: 1fr;
  }
}
</style>
