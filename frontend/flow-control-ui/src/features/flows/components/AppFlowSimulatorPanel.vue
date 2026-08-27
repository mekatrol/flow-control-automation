<template>
  <section class="simulator-controls" aria-label="Simulation controls">
    <AppButton
      :text="lifecycle === 'compiling' ? 'Starting…' : 'Start simulation'"
      :icon="playIcon"
      :disabled="!canStart"
      @click="emit(EVENTS.START_SIMULATION)"
    />
    <AppButton
      text="Stop simulation"
      :icon="stopIcon"
      :disabled="!active"
      @click="emit(EVENTS.STOP_SIMULATION)"
    />
    <span class="state" role="status">{{ stateLabel }}</span>
    <p v-if="error" class="error" role="alert">{{ error }}</p>
  </section>
</template>

<script setup lang="ts">
import { computed } from 'vue';
import playIcon from '@/assets/icons/play-icon.svg';
import stopIcon from '@/assets/icons/stop-icon.svg';
import AppButton from '@/components/AppButton.vue';
import { EVENTS } from '@/constants/events';
import type { SimulatorLifecycle } from '@/features/flows/api/flowSimulatorApi';

const props = defineProps<{ lifecycle: SimulatorLifecycle; error?: string }>();
const emit = defineEmits<{
  (event: typeof EVENTS.START_SIMULATION | typeof EVENTS.STOP_SIMULATION): void;
}>();
const active = computed(() =>
  ['ready', 'running', 'paused', 'faulted', 'stale'].includes(props.lifecycle)
);
const canStart = computed(
  () => !['compiling', 'running', 'ready', 'paused'].includes(props.lifecycle)
);
const stateLabel = computed(() =>
  props.lifecycle === 'ready' || props.lifecycle === 'paused' ? 'running' : props.lifecycle
);
</script>

<style scoped>
.simulator-controls {
  display: flex;
  flex: 0 0 auto;
  flex-wrap: wrap;
  gap: var(--space-3);
  align-items: center;
  margin-bottom: var(--space-4);
  padding: var(--space-3);
  background: var(--color-surface-subtle);
  border: var(--border-width-default) solid var(--color-border-subtle);
  border-radius: var(--radius-lg);
}
.state {
  margin-left: auto;
  font-weight: var(--font-weight-semibold);
  text-transform: uppercase;
}
.error {
  width: 100%;
  margin: 0;
  color: var(--color-danger-text);
}
</style>
