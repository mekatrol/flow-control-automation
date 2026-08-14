<template>
  <section v-bind="automation()" class="emulator-panel" aria-label="Controller emulator">
    <strong>Controller emulator</strong>
    <span>Virtual time {{ snapshot?.virtualTimeMilliseconds ?? 0 }} ms</span>
    <fieldset v-for="input in snapshot?.inputs ?? []" :key="input.pointId" class="input-control">
      <legend>{{ inputLabel(input.pointId) }}</legend>
      <label v-if="input.typedValue.type === 'boolean'">
        Value
        <input v-model="inputDraft(input.pointId).boolean" type="checkbox" />
      </label>
      <label v-else>
        Value
        <input
          v-model.number="inputDraft(input.pointId).number"
          type="number"
          :aria-invalid="error ? true : undefined"
        />
        {{ inputUnits(input.pointId) }}
      </label>
      <label>
        Quality
        <select v-model="inputDraft(input.pointId).quality">
          <option v-for="quality in qualities" :key="quality">{{ quality }}</option>
        </select>
      </label>
    </fieldset>
    <p v-if="error" class="error" role="alert">{{ error }}</p>
    <AppButton
      automation="emulator-apply-step"
      text="Apply inputs and step"
      :disabled="!snapshot"
      @click="applyAndStep"
    />
    <AppButton
      automation="emulator-advance"
      text="Advance time and scan"
      :disabled="!snapshot"
      @click="emit(EVENTS.ADVANCE, 100)"
    />
    <select :value="snapshot?.activeFault ?? ''" @change="setFault">
      <option value="">No fault</option>
      <option value="communication_loss">Communication loss</option>
      <option value="stale_input">Stale input</option>
      <option value="output_failure">Output failure</option>
    </select>
    <AppButton
      automation="emulator-reset"
      text="Reset state"
      :disabled="!snapshot"
      @click="emit(EVENTS.RESET, false)"
    />
    <AppButton
      automation="emulator-reset-inputs"
      text="Reset inputs to defaults"
      :disabled="!snapshot"
      @click="emit(EVENTS.RESET_INPUTS)"
    />
    <AppButton
      automation="emulator-power-cycle"
      text="Power cycle"
      :disabled="!snapshot"
      @click="emit(EVENTS.RESET, true)"
    />
    <ul v-if="snapshot?.outputHistory.length" aria-label="Emulator output history">
      <li v-for="output in snapshot.outputHistory" :key="`${output.scanNumber}:${output.outputId}`">
        {{ outputLabel(output.outputId) }}: proposed {{ displayValue(output.proposedValue) }},
        committed simulator {{ displayValue(output.effectiveValue) }} {{ output.units }} ({{
          output.quality
        }}), changed at scan {{ output.lastChangeScan }}
      </li>
    </ul>
  </section>
</template>

<script setup lang="ts">
import AppButton from '@/components/AppButton.vue';
import { useAutomation } from '@/composables/useAutomation';
import { reactive, ref, watch } from 'vue';
import { EVENTS } from '@/constants/events';
import type {
  EmulatorInputChange,
  EmulatorSnapshot,
  EmulatorValue
} from '@/features/flows/api/flowEmulatorApi';
import type { FlowInterface, FlowInterfaceInput } from '@/features/flows/types';

const props = defineProps<{
  automation: string;
  snapshot?: EmulatorSnapshot;
  flowInterface: FlowInterface;
}>();
const emit = defineEmits<{
  (event: typeof EVENTS.APPLY_INPUTS_STEP, inputs: EmulatorInputChange[]): void;
  (event: typeof EVENTS.ADVANCE, milliseconds: number): void;
  (event: typeof EVENTS.FAULT, fault: string | null): void;
  (event: typeof EVENTS.RESET, powerCycle: boolean): void;
  (event: typeof EVENTS.RESET_INPUTS): void;
}>();
const automation = useAutomation(props.automation);
const qualities = ['good', 'bad', 'stale', 'unavailable'] as const;
const draft = reactive<Record<string, EmulatorValue>>({});
const error = ref<string>();
const inputDraft = (id: string): EmulatorValue =>
  draft[id] ?? { type: 'boolean', boolean: false, number: 0, quality: 'unavailable' };
watch(
  () => props.snapshot?.inputs,
  (inputs) => inputs?.forEach((input) => (draft[input.pointId] = { ...input.typedValue })),
  { immediate: true, deep: true }
);
const inputEntry = (id: string): FlowInterfaceInput | undefined =>
  props.flowInterface.inputs.find((entry) => entry.id === id);
const inputLabel = (id: string): string => inputEntry(id)?.name ?? id;
const inputUnits = (id: string): string => inputEntry(id)?.units ?? '';
const outputLabel = (id: string): string =>
  props.flowInterface.outputs.find((entry) => entry.id === id)?.name ?? id;
const displayValue = (value: EmulatorValue): string =>
  value.type === 'number' ? String(value.number) : value.boolean ? 'On' : 'Off';
const applyAndStep = (): void => {
  const changes = Object.entries(draft).map(([inputId, typedValue]) => ({ inputId, typedValue }));
  if (
    changes.some(
      ({ typedValue }) => typedValue.type === 'number' && !Number.isFinite(typedValue.number)
    )
  ) {
    error.value = 'Numeric inputs must be finite.';
    return;
  }
  error.value = undefined;
  emit(EVENTS.APPLY_INPUTS_STEP, changes);
};
const setFault = (event: Event): void =>
  emit(EVENTS.FAULT, (event.target as HTMLSelectElement).value || null);
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
.input-control {
  display: flex;
  gap: var(--space-3);
  align-items: center;
}
.error {
  color: var(--color-danger-text);
}
</style>
