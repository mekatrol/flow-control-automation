<template>
  <section v-bind="automation()" class="emulator-panel" aria-label="Controller emulator">
    <strong>Controller emulator</strong>
    <span>Virtual time {{ snapshot?.virtualTimeMilliseconds ?? 0 }} ms</span>
    <label v-for="input in snapshot?.inputs ?? []" :key="input.pointId">
      {{ input.pointId }}
      <input type="checkbox" :checked="input.value" @change="setInput(input.pointId, $event)" />
    </label>
    <AppButton
      automation="emulator-advance"
      text="Advance and scan"
      :disabled="!snapshot"
      @click="emit('advance', 100)"
    />
    <select :value="snapshot?.activeFault ?? ''" @change="setFault">
      <option value="">No fault</option>
      <option value="communication_loss">Communication loss</option>
      <option value="stale_input">Stale input</option>
      <option value="output_failure">Output failure</option>
    </select>
    <AppButton
      automation="emulator-reset"
      text="Reset"
      :disabled="!snapshot"
      @click="emit('reset', false)"
    />
    <AppButton
      automation="emulator-power-cycle"
      text="Power cycle"
      :disabled="!snapshot"
      @click="emit('reset', true)"
    />
    <ul v-if="snapshot?.outputHistory.length" aria-label="Emulator output history">
      <li v-for="output in snapshot.outputHistory" :key="`${output.scanNumber}:${output.pointId}`">
        {{ output.pointId }}: proposed {{ output.proposedValue }}, effective
        {{ output.effectiveValue }} ({{ output.quality }})
      </li>
    </ul>
  </section>
</template>

<script setup lang="ts">
import AppButton from '@/components/AppButton.vue';
import { useAutomation } from '@/composables/useAutomation';
import type { EmulatorSnapshot } from '@/features/flows/api/flowEmulatorApi';

const props = defineProps<{ automation: string; snapshot?: EmulatorSnapshot }>();
const emit = defineEmits<{
  setInput: [pointId: string, value: boolean];
  advance: [milliseconds: number];
  fault: [fault: string | null];
  reset: [powerCycle: boolean];
}>();
const automation = useAutomation(props.automation);
const setInput = (pointId: string, event: Event): void =>
  emit('setInput', pointId, (event.target as HTMLInputElement).checked);
const setFault = (event: Event): void =>
  emit('fault', (event.target as HTMLSelectElement).value || null);
</script>

<style scoped>
.emulator-panel {
  display: flex;
  flex-wrap: wrap;
  gap: var(--space-3);
  align-items: center;
  padding: var(--space-3);
  margin-bottom: var(--space-4);
  border: var(--border-width-default) solid var(--color-border-subtle);
  border-radius: var(--radius-lg);
}
.emulator-panel ul {
  width: 100%;
}
</style>
