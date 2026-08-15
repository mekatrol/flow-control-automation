<template>
  <section v-bind="automation()" class="simulator-panel" aria-labelledby="simulator-title">
    <div class="simulator-heading">
      <div>
        <h2 id="simulator-title">Simulator</h2>
        <p>Run this draft with shadow outputs. Physical equipment cannot be commanded.</p>
      </div>
      <strong class="state" :class="lifecycle" role="status" aria-live="polite">{{
        stateLabel
      }}</strong>
    </div>
    <div class="controls" role="group" aria-label="Simulation controls">
      <AppButton
        automation="simulator-start"
        :text="lifecycle === 'compiling' ? 'Compiling…' : 'Start simulation'"
        :disabled="!canStart"
        @click="emit(EVENTS.START_SIMULATION)"
      />
      <AppButton
        automation="simulator-step-tick"
        text="Step tick"
        :disabled="!canExecute"
        @click="emit(EVENTS.STEP_TICK)"
      />
      <AppButton
        automation="simulator-step-node"
        text="Step node"
        :disabled="!canStepNode"
        @click="emit(EVENTS.STEP_NODE)"
      />
      <AppButton
        automation="simulator-step-instruction"
        text="Step instruction"
        :disabled="!canStepInstruction"
        @click="emit(EVENTS.STEP_INSTRUCTION)"
      />
      <AppButton
        automation="simulator-run"
        text="Run"
        :disabled="!canExecute"
        @click="emit(EVENTS.RUN)"
      />
      <AppButton
        automation="simulator-pause"
        text="Pause"
        :disabled="lifecycle !== 'running'"
        @click="emit(EVENTS.PAUSE)"
      />
      <AppButton
        automation="simulator-restart"
        text="Restart"
        :disabled="!active || lifecycle === 'running'"
        @click="emit(EVENTS.RESTART)"
      />
      <AppButton
        automation="simulator-stop"
        text="Stop"
        :disabled="!active"
        @click="emit(EVENTS.STOP_SIMULATION)"
      />
    </div>
    <p v-if="lifecycle === 'stale'" class="stale" role="alert">
      The draft changed. Start simulation again to compile the current graph.
    </p>
    <p v-if="error" class="error" role="alert">{{ error }}</p>
    <dl v-if="session" class="summary">
      <div>
        <dt>Source digest</dt>
        <dd>{{ session.sourceDigest }}</dd>
      </div>
      <div>
        <dt>Tick</dt>
        <dd>{{ session.snapshot?.tickNumber ?? 0 }}</dd>
      </div>
      <div>
        <dt>Lease</dt>
        <dd>{{ Math.ceil(session.leaseRemainingMilliseconds / 1000) }} seconds</dd>
      </div>
    </dl>
    <details v-if="session" class="advanced">
      <summary>Advanced debugger</summary>
      <p>Instruction {{ session.inspection?.instructionPointer ?? 'at scan boundary' }}</p>
      <p>{{ session.breakpoints.length }} breakpoints</p>
    </details>
    <section v-if="session" class="scenarios" aria-labelledby="scenario-title">
      <h3 id="scenario-title">Scenarios</h3>
      <div class="controls" role="group" aria-label="Scenario recording controls">
        <AppButton
          automation="scenario-record"
          :text="recording ? 'Recording…' : 'Record'"
          :disabled="recording"
          @click="emit(EVENTS.START_RECORDING)"
        />
        <AppButton
          automation="scenario-stop-recording"
          text="Stop recording"
          :disabled="!recording"
          @click="emit(EVENTS.STOP_RECORDING)"
        />
        <label for="scenario-name">Scenario name</label>
        <input id="scenario-name" v-model.trim="scenarioName" maxlength="200" />
        <AppButton
          automation="scenario-save"
          text="Save scenario"
          :disabled="recordedStepCount === 0 || scenarioName.length === 0"
          @click="emit(EVENTS.SAVE_SCENARIO, scenarioName)"
        />
      </div>
      <p role="status" aria-live="polite">{{ recordedStepCount }} recorded steps.</p>
      <ol v-if="recordedStepCount > 0" aria-label="Recorded timeline">
        <li v-for="(step, index) in recordedSteps" :key="index">
          {{ step.atMilliseconds }} ms — {{ step.action }}
        </li>
      </ol>
      <ul v-if="scenarios.length > 0" class="scenario-list">
        <li v-for="scenario in scenarios" :key="scenario.id">
          <span>{{ scenario.name }} ({{ scenario.steps.length }} steps)</span>
          <AppButton
            :automation="`scenario-replay-${scenario.id}`"
            text="Replay"
            @click="emit(EVENTS.REPLAY_SCENARIO, scenario)"
          />
        </li>
      </ul>
      <p v-if="scenarioResult" :class="scenarioResult.passed ? 'passed' : 'error'" role="status">
        Replay {{ scenarioResult.passed ? 'passed' : 'failed' }} at scan
        {{ scenarioResult.scanNumber }}.
      </p>
    </section>
    <AppFlowEmulatorPanel
      v-if="session?.io"
      automation="simulator-io"
      :snapshot="session.io"
      :flow-interface="flowInterface"
      @[EVENTS.APPLY_INPUTS_STEP]="forwardInputs"
      @[EVENTS.ADVANCE]="forwardAdvance"
      @[EVENTS.FAULT]="forwardFault"
      @[EVENTS.RESET]="forwardReset"
      @[EVENTS.RESET_INPUTS]="emit(EVENTS.RESET_INPUTS)"
    />
  </section>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue';
import AppButton from '@/components/AppButton.vue';
import AppFlowEmulatorPanel from '@/features/flows/components/AppFlowEmulatorPanel.vue';
import { useAutomation } from '@/composables/useAutomation';
import { EVENTS } from '@/constants/events';
import type { SimulatorLifecycle, SimulatorSession } from '@/features/flows/api/flowSimulatorApi';
import type { EmulatorInputChange } from '@/features/flows/api/flowEmulatorApi';
import type { FlowInterface } from '@/features/flows/types';
import type { FlowScenario, FlowScenarioRunResult } from '@/features/flows/api/flowScenarioApi';

const props = withDefaults(
  defineProps<{
    automation: string;
    lifecycle: SimulatorLifecycle;
    session?: SimulatorSession;
    error?: string;
    flowInterface?: FlowInterface;
    recording?: boolean;
    recordedStepCount?: number;
    scenarios?: FlowScenario[];
    recordedSteps?: FlowScenario['steps'];
    scenarioResult?: FlowScenarioRunResult;
  }>(),
  {
    session: undefined,
    error: undefined,
    flowInterface: undefined,
    recording: false,
    recordedStepCount: 0,
    scenarios: () => [],
    recordedSteps: () => [],
    scenarioResult: undefined
  }
);
const emit = defineEmits<{
  (event: typeof EVENTS.START_SIMULATION): void;
  (event: typeof EVENTS.STEP_TICK): void;
  (event: typeof EVENTS.STEP_NODE): void;
  (event: typeof EVENTS.STEP_INSTRUCTION): void;
  (event: typeof EVENTS.STOP_SIMULATION): void;
  (event: typeof EVENTS.APPLY_INPUTS_STEP, inputs: EmulatorInputChange[]): void;
  (event: typeof EVENTS.ADVANCE, milliseconds: number): void;
  (event: typeof EVENTS.FAULT, fault: string | null): void;
  (event: typeof EVENTS.RESET, powerCycle: boolean): void;
  (event: typeof EVENTS.RESET_INPUTS): void;
  (event: typeof EVENTS.RUN | typeof EVENTS.PAUSE | typeof EVENTS.RESTART): void;
  (event: typeof EVENTS.START_RECORDING | typeof EVENTS.STOP_RECORDING): void;
  (event: typeof EVENTS.SAVE_SCENARIO, name: string): void;
  (event: typeof EVENTS.REPLAY_SCENARIO, scenario: FlowScenario): void;
}>();
const automation = useAutomation(props.automation);
const scenarioName = ref('Recorded scenario');
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
  margin-bottom: var(--space-4);
  padding: var(--space-4);
  border: var(--border-width-default) solid var(--color-border-subtle);
  border-radius: var(--radius-lg);
  background: var(--color-surface-subtle);
}
.simulator-heading,
.controls,
.summary {
  display: flex;
  flex-wrap: wrap;
  gap: var(--space-3);
  align-items: center;
}
.simulator-heading {
  justify-content: space-between;
}
.simulator-heading h2,
.simulator-heading p,
.summary dd {
  margin: 0;
}
.state {
  text-transform: uppercase;
}
.state.stale,
.stale,
.error {
  color: var(--color-danger-text);
}
.summary {
  margin-top: var(--space-3);
}
.summary div {
  min-width: 0;
}
.summary dt {
  font-weight: var(--font-weight-semibold);
}
.summary dd {
  overflow-wrap: anywhere;
}
.advanced {
  margin-top: var(--space-3);
}
.scenarios {
  margin-top: var(--space-4);
  border-top: var(--border-width-default) solid var(--color-border-subtle);
}
.scenario-list {
  padding: 0;
  list-style: none;
}
.scenario-list li {
  display: flex;
  gap: var(--space-3);
  align-items: center;
  justify-content: space-between;
}
.passed {
  color: var(--color-success-text);
}
</style>
