<template>
  <section v-bind="automation()" class="emulator-panel" aria-labelledby="io-title">
    <div class="section-heading">
      <div>
        <h3 id="io-title">Inputs and outputs</h3>
        <p>Set input values, then execute a scan to inspect the result.</p>
      </div>
      <span
        >Virtual time <strong>{{ snapshot?.virtualTimeMilliseconds ?? 0 }} ms</strong></span
      >
    </div>
    <div class="workbench">
      <section class="input-area" aria-labelledby="inputs-title">
        <div class="area-heading">
          <h4 id="inputs-title">Inputs</h4>
          <AppButton
            v-bind="automation('reset-inputs')"
            text="Restore defaults"
            :disabled="!snapshot"
            @click="emit(EVENTS.RESET_INPUTS)"
          />
        </div>
        <div v-if="snapshot?.inputs.length" class="input-grid">
          <fieldset v-for="input in snapshot.inputs" :key="input.pointId" class="input-card">
            <legend>{{ inputLabel(input.pointId) }}</legend>
            <label v-if="input.typedValue.type === 'boolean'" class="value-toggle"
              ><input v-model="inputDraft(input.pointId).boolean" type="checkbox" /><span>{{
                inputDraft(input.pointId).boolean ? 'On' : 'Off'
              }}</span></label
            >
            <label v-else class="field-label"
              ><span>Value</span>
              <div class="number-field">
                <input
                  v-model.number="inputDraft(input.pointId).number"
                  type="number"
                  :aria-invalid="error ? true : undefined"
                /><span v-if="inputUnits(input.pointId)">{{ inputUnits(input.pointId) }}</span>
              </div></label
            >
            <label class="field-label"
              ><span>Quality</span
              ><select v-model="inputDraft(input.pointId).quality">
                <option v-for="quality in qualities" :key="quality">{{ quality }}</option>
              </select></label
            >
          </fieldset>
        </div>
        <p v-else class="empty-state">This flow has no interface inputs.</p>
        <p v-if="error" class="error" role="alert">{{ error }}</p>
        <div class="execute-actions">
          <AppButton
            v-bind="automation('apply-step')"
            text="Apply inputs and run one scan"
            :disabled="!snapshot"
            @click="applyAndStep"
          /><AppButton
            v-bind="automation('advance')"
            text="Advance 100 ms and scan"
            :disabled="!snapshot"
            @click="emit(EVENTS.ADVANCE, 100)"
          />
        </div>
      </section>
      <aside class="tools-area" aria-labelledby="tools-title">
        <h4 id="tools-title">Test conditions</h4>
        <label class="field-label"
          ><span>Injected fault</span
          ><select :value="snapshot?.activeFault ?? ''" @change="setFault">
            <option value="">None</option>
            <option value="communication_loss">Communication loss</option>
            <option value="stale_input">Stale input</option>
            <option value="output_failure">Output failure</option>
          </select></label
        >
        <div class="reset-actions">
          <AppButton
            v-bind="automation('reset')"
            text="Reset state"
            :disabled="!snapshot"
            @click="emit(EVENTS.RESET, false)"
          /><AppButton
            v-bind="automation('power-cycle')"
            text="Power cycle"
            :disabled="!snapshot"
            @click="emit(EVENTS.RESET, true)"
          />
        </div>
      </aside>
    </div>
    <section class="outputs" aria-labelledby="outputs-title">
      <div class="area-heading">
        <h4 id="outputs-title">Latest outputs</h4>
        <span v-if="snapshot">Scan {{ snapshot.scanNumber }}</span>
      </div>
      <div v-if="latestOutputs.length" class="output-table">
        <div class="output-row output-header" aria-hidden="true">
          <span>Output</span><span>Proposed</span><span>Committed</span><span>Quality</span
          ><span>Last change</span>
        </div>
        <div v-for="output in latestOutputs" :key="output.outputId" class="output-row">
          <strong>{{ outputLabel(output.outputId) }}</strong
          ><span>{{ displayValue(output.proposedValue) }} {{ output.units }}</span
          ><span>{{ displayValue(output.effectiveValue) }} {{ output.units }}</span
          ><span class="quality" :class="output.quality">{{ output.quality }}</span
          ><span>Scan {{ output.lastChangeScan }}</span>
        </div>
      </div>
      <p v-else class="empty-state">Run a scan to see output values.</p>
    </section>
  </section>
</template>

<script setup lang="ts">
import AppButton from '@/components/AppButton.vue';
import { useAutomation } from '@/composables/useAutomation';
import { computed, reactive, ref, watch } from 'vue';
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
const latestOutputs = computed(() => {
  const history = props.snapshot?.outputHistory ?? [];
  const scan = Math.max(0, ...history.map((item) => item.scanNumber));
  return history.filter((item) => item.scanNumber === scan);
});
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
  margin: 0 var(--space-6-5) var(--space-4);
  overflow: hidden;
  background: var(--color-surface-raised);
  border: var(--border-width-default) solid var(--color-border-subtle);
  border-radius: var(--radius-lg);
}
.section-heading,
.area-heading {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--space-4);
}
.section-heading {
  padding: var(--space-4);
  border-bottom: var(--border-width-default) solid var(--color-border-subtle);
}
h3,
h4,
.section-heading p {
  margin: 0;
}
.section-heading p {
  margin-top: var(--space-2);
  color: var(--color-text-muted);
}
.section-heading > span,
.area-heading > span {
  color: var(--color-text-muted);
}
.workbench {
  display: grid;
  grid-template-columns: minmax(0, 1fr) minmax(13rem, 20rem);
}
.input-area,
.tools-area,
.outputs {
  padding: var(--space-4);
}
.tools-area {
  border-left: var(--border-width-default) solid var(--color-border-subtle);
}
.input-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(14rem, 1fr));
  gap: var(--space-3);
  margin-top: var(--space-3);
}
.input-card {
  display: grid;
  gap: var(--space-3);
  min-width: 0;
  padding: var(--space-4);
  border: var(--border-width-default) solid var(--color-border-subtle);
  border-radius: var(--radius-lg);
}
.input-card legend {
  padding: 0 var(--space-2);
  font-weight: var(--font-weight-semibold);
}
.field-label {
  display: grid;
  gap: var(--space-2);
  color: var(--color-text-muted);
  font-size: var(--font-size-sm);
}
.field-label select,
.field-label input {
  min-height: var(--control-min-height);
  color: var(--color-text-primary);
  background: var(--color-surface-subtle);
  border: var(--border-width-default) solid var(--color-border-default);
  border-radius: var(--radius-lg);
}
.number-field {
  display: flex;
  align-items: center;
  gap: var(--space-2);
}
.number-field input {
  min-width: 0;
  width: 100%;
  padding: 0 var(--space-3);
}
.value-toggle {
  display: flex;
  gap: var(--space-2);
  align-items: center;
  min-height: var(--control-min-height);
  font-weight: var(--font-weight-semibold);
}
.execute-actions,
.reset-actions {
  display: flex;
  flex-wrap: wrap;
  gap: var(--space-2);
  margin-top: var(--space-4);
}
.reset-actions {
  flex-direction: column;
  align-items: stretch;
}
.tools-area .field-label {
  margin-top: var(--space-4);
}
.outputs {
  border-top: var(--border-width-default) solid var(--color-border-subtle);
}
.output-table {
  margin-top: var(--space-3);
  overflow-x: auto;
}
.output-row {
  display: grid;
  grid-template-columns: minmax(10rem, 1.5fr) repeat(4, minmax(7rem, 1fr));
  gap: var(--space-3);
  align-items: center;
  padding: var(--space-3);
  border-top: var(--border-width-default) solid var(--color-border-subtle);
}
.output-header {
  color: var(--color-text-muted);
  font-size: var(--font-size-sm);
  border-top: 0;
}
.quality {
  width: fit-content;
  padding: var(--space-1) var(--space-2);
  text-transform: capitalize;
  border: var(--border-width-default) solid currentcolor;
  border-radius: 999px;
}
.quality.good {
  color: var(--color-success-text);
}
.quality.bad,
.error {
  color: var(--color-danger-text);
}
.empty-state {
  color: var(--color-text-muted);
}
@media (max-width: 52rem) {
  .workbench {
    grid-template-columns: 1fr;
  }
  .tools-area {
    border-top: var(--border-width-default) solid var(--color-border-subtle);
    border-left: 0;
  }
  .output-header {
    display: none;
  }
  .output-row {
    grid-template-columns: 1fr 1fr;
  }
}
</style>
