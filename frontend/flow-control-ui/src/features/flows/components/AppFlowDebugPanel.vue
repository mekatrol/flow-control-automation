<template>
  <section v-bind="automation()" class="debug-panel" aria-label="Shadow debugging">
    <div class="debug-controls">
      <strong>Shadow debug</strong>
      <span class="mode">Proposed outputs are non-physical</span>
      <AppButton automation="debug-load" text="Load" :disabled="!canLoad" @click="emit('load')" />
      <AppButton automation="debug-step" text="Step" :disabled="!canStep" @click="emit('step')" />
      <AppButton automation="debug-run" text="Run" :disabled="!canRun" @click="emit('run')" />
      <AppButton
        automation="debug-pause"
        text="Pause"
        :disabled="!canPause"
        @click="emit('pause')"
      />
      <AppButton automation="debug-stop" text="Stop" :disabled="!canStop" @click="emit('stop')" />
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
          automation="debug-enable-live-output"
          text="Enable live outputs"
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
import AppButton from '@/components/AppButton.vue';
import { useAutomation } from '@/composables/useAutomation';
import type { DebugRuntimeSnapshot } from '@/features/flows/api/flowDebugApi';

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
}>();
const emit = defineEmits<{
  load: [];
  step: [];
  run: [];
  pause: [];
  stop: [];
  enableLiveOutput: [pointIds: string[]];
}>();
const automation = useAutomation(props.automation);
const busy = computed(() => props.lifecycle === 'loading' || props.lifecycle === 'stepping');
const active = computed(() => ['ready', 'running', 'paused', 'fault'].includes(props.lifecycle));
const canLoad = computed(() => props.targetAvailable && !busy.value && !active.value);
const canStep = computed(() => !props.stale && ['ready', 'paused'].includes(props.lifecycle));
const canRun = computed(() => !props.stale && ['ready', 'paused'].includes(props.lifecycle));
const canPause = computed(() => props.lifecycle === 'running');
const canStop = computed(() => active.value || busy.value);
const affectedOutputPoints = computed(() => props.affectedOutputPoints ?? []);
const liveOutputConfirmed = ref(false);
const canEnableLiveOutput = computed(
  () => liveOutputConfirmed.value && !props.stale && ['ready', 'paused'].includes(props.lifecycle)
);
const stateLabel = computed(() => (props.stale ? 'stale' : props.lifecycle));
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
.snapshot.stale {
  opacity: 0.65;
}
.snapshot ul {
  width: 100%;
  margin: 0;
}
</style>
