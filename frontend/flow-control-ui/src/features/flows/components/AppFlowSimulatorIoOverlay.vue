<template>
  <aside
    ref="panelElement"
    class="simulator-io configuration-panel"
    aria-label="Simulation points"
    tabindex="0"
  >
    <header>
      <h2>Simulation points</h2>
      <AppButton
        text="Apply"
        :icon="applyIcon"
        :disabled="!snapshot || !inputs.length"
        @click="apply"
      />
    </header>
    <p v-if="inputError" class="input-error" role="alert">{{ inputError }}</p>
    <p v-if="!points.length" class="empty">This flow does not use any points.</p>
    <section v-if="virtualPoints.length" aria-labelledby="virtual-points-heading">
      <h3 id="virtual-points-heading">Virtual points</h3>
      <div
        v-for="point in virtualPoints"
        :key="point.pointId"
        class="point-row"
        :class="{ selected: point.pointId === selectedPointId }"
      >
        <span class="point-name">{{ point.pointId }}</span>
        <template v-if="point.direction === DataDirectionType.Input">
          <input
            v-if="point.declaration.valueType === AutomationPointValueType.Digital"
            v-model="draft[point.pointId]"
            type="checkbox"
            :aria-label="`${point.pointId} simulated value`"
            :data-point-id="point.pointId"
            @change="markDirty(point.pointId)"
          />
          <input
            v-else
            v-model="draft[point.pointId]"
            type="text"
            inputmode="decimal"
            :aria-label="`${point.pointId} simulated value`"
            :data-point-id="point.pointId"
            @input="markDirty(point.pointId)"
          />
          <small>{{ point.declaration.units ?? '' }}</small>
        </template>
        <output v-else>{{ display(point) }}</output>
      </div>
    </section>
    <section v-if="connectedPoints.length" aria-labelledby="connected-points-heading">
      <h3 id="connected-points-heading">Connected points</h3>
      <div v-for="point in connectedPoints" :key="point.pointId" class="point-row">
        <span class="point-name">{{ point.pointId }}</span>
        <template v-if="point.direction === DataDirectionType.Input">
          <input
            v-if="point.declaration.valueType === AutomationPointValueType.Digital"
            v-model="draft[point.pointId]"
            type="checkbox"
            :aria-label="`${point.pointId} simulated value`"
            @change="markDirty(point.pointId)"
          />
          <input
            v-else
            v-model="draft[point.pointId]"
            type="text"
            inputmode="decimal"
            :aria-label="`${point.pointId} simulated value`"
            @input="markDirty(point.pointId)"
          />
          <small>{{ point.declaration.units ?? '' }}</small>
        </template>
        <output v-else>{{ display(point) }}</output>
      </div>
    </section>
  </aside>
</template>

<script setup lang="ts">
import { FlowNodeType, DataType } from '@/types/serverTypes';

import { AutomationPointValueType, DataDirectionType, DataQualityType } from '@/types/serverTypes';

import { computed, reactive, ref, watch } from 'vue';
import applyIcon from '@/assets/icons/check-icon.svg';
import AppButton from '@/components/AppButton.vue';
import type {
  EmulatorInputChange,
  EmulatorSnapshot,
  EmulatorValue
} from '@/features/flows/api/flowEmulatorApi';
import {
  virtualPointDeclarationsFromNodes,
  type FlowDefinition,
  type VirtualPointDeclaration
} from '@/features/flows/types';

interface SimulationPoint {
  pointId: string;
  declaration: VirtualPointDeclaration;
  direction: typeof DataDirectionType.Input | typeof DataDirectionType.Output;
  value: EmulatorValue;
  connected: boolean;
}
const props = defineProps<{
  flow: FlowDefinition;
  snapshot?: EmulatorSnapshot;
  contextPointContracts?: VirtualPointDeclaration[];
  selectedPointId?: string;
}>();
const emit = defineEmits<{ (event: 'apply', inputs: EmulatorInputChange[]): void }>();
const draft = reactive<Record<string, string | boolean>>({});
const dirty = reactive(new Set<string>());
const inputError = ref('');
const panelElement = ref<HTMLElement>();
const flowDeclarations = computed(
  () =>
    new Map(virtualPointDeclarationsFromNodes(props.flow.nodes).map((point) => [point.key, point]))
);
const contextDeclarations = computed(
  () => new Map((props.contextPointContracts ?? []).map((point) => [point.key, point]))
);
const points = computed<SimulationPoint[]>(() => {
  const inputValues = new Map(
    (props.snapshot?.inputs ?? []).map((input) => [input.pointId, input.typedValue])
  );
  const outputValues = new Map<string, EmulatorValue>();
  for (const output of props.snapshot?.outputHistory ?? [])
    outputValues.set(output.outputId, output.effectiveValue);
  const result = new Map<string, SimulationPoint>();
  for (const node of props.flow.nodes) {
    const virtual =
      node.nodeType === FlowNodeType.AnalogVirtual || node.nodeType === FlowNodeType.DigitalVirtual;
    const virtualRead =
      virtual && props.flow.connections.some((connection) => connection.start.nodeId === node.id);
    const virtualWrite =
      virtual && props.flow.connections.some((connection) => connection.end.nodeId === node.id);
    const input =
      node.nodeType === FlowNodeType.AnalogInput ||
      node.nodeType === FlowNodeType.DigitalInput ||
      (virtualRead && !virtualWrite);
    const output =
      node.nodeType === FlowNodeType.AnalogOutput ||
      node.nodeType === FlowNodeType.DigitalOutput ||
      virtualWrite;
    if (!input && !output) continue;
    const pointId = String(node.configuration.pointId ?? '');
    if (!pointId || result.has(pointId)) continue;
    const flowDeclaration = flowDeclarations.value.get(pointId);
    const declaration = flowDeclaration ?? contextDeclarations.value.get(pointId);
    if (!declaration) continue;
    const numeric = declaration.valueType === AutomationPointValueType.Analog;
    const fallback: EmulatorValue = {
      dataType: numeric ? DataType.Number : DataType.Boolean,
      boolean: !numeric && Boolean(declaration.relinquishDefault),
      number:
        numeric && typeof declaration.relinquishDefault === 'number'
          ? declaration.relinquishDefault
          : 0,
      quality: DataQualityType.Good
    };
    result.set(pointId, {
      pointId,
      declaration,
      direction: input ? DataDirectionType.Input : DataDirectionType.Output,
      value: (input ? inputValues : outputValues).get(pointId) ?? fallback,
      connected: !flowDeclaration
    });
  }
  return [...result.values()];
});
const inputs = computed(() =>
  points.value.filter((point) => point.direction === DataDirectionType.Input)
);
const virtualPoints = computed(() => points.value.filter((point) => !point.connected));
const connectedPoints = computed(() => points.value.filter((point) => point.connected));
watch(
  points,
  (values) =>
    values.forEach((point) => {
      if (point.direction !== DataDirectionType.Input || dirty.has(point.pointId)) return;
      draft[point.pointId] =
        point.declaration.valueType === AutomationPointValueType.Digital
          ? point.value.boolean
          : String(point.value.number);
    }),
  { immediate: true, deep: true }
);
watch(
  () => props.selectedPointId,
  (pointId) => {
    if (!pointId) return;
    const input = [
      ...(panelElement.value?.querySelectorAll<HTMLInputElement>('[data-point-id]') ?? [])
    ].find((element) => element.dataset.pointId === pointId);
    input?.focus();
  },
  { immediate: true, flush: 'post' }
);
const markDirty = (pointId: string): void => {
  dirty.add(pointId);
};
const display = (point: SimulationPoint): string =>
  point.declaration.valueType === AutomationPointValueType.Analog
    ? `${point.value.number}${point.declaration.units ? ` ${point.declaration.units}` : ''}`
    : point.value.boolean
      ? 'On'
      : 'Off';
const apply = (): void => {
  const invalid = inputs.value.find(
    (point) =>
      point.declaration.valueType === AutomationPointValueType.Analog &&
      !Number.isFinite(Number(draft[point.pointId]))
  );
  if (invalid) {
    inputError.value = `Enter a valid number for ${invalid.pointId}.`;
    return;
  }
  inputError.value = '';
  emit(
    'apply',
    inputs.value.map((point) => ({
      inputId: point.pointId,
      typedValue: {
        ...point.value,
        dataType:
          point.declaration.valueType === AutomationPointValueType.Digital
            ? DataType.Boolean
            : DataType.Number,
        boolean:
          point.declaration.valueType === AutomationPointValueType.Digital
            ? Boolean(draft[point.pointId])
            : point.value.boolean,
        number:
          point.declaration.valueType === AutomationPointValueType.Analog
            ? Number(draft[point.pointId])
            : point.value.number
      }
    }))
  );
  dirty.clear();
};
</script>

<style scoped>
.simulator-io {
  width: 280px;
  min-width: 280px;
  padding: var(--space-4);
  overflow-y: auto;
  background: var(--color-surface-subtle);
  border-left: var(--border-width-default) solid var(--color-border-subtle);
}
.simulator-io header {
  display: flex;
  gap: var(--space-3);
  align-items: center;
  justify-content: space-between;
  margin-bottom: var(--space-5);
}
.simulator-io h2,
.simulator-io h3,
.simulator-io .empty {
  margin: 0;
}
.simulator-io h2 {
  font-size: var(--font-size-lg);
}
.input-error {
  color: var(--color-danger-text);
}
.simulator-io h3 {
  margin-block: var(--space-5) var(--space-2);
  color: var(--color-text-muted);
  font-size: var(--font-size-sm);
  text-transform: uppercase;
}
.point-row {
  display: grid;
  grid-template-columns: minmax(0, 1fr) minmax(5rem, 7rem) auto;
  gap: var(--space-2);
  align-items: center;
  min-height: 2.25rem;
}
.point-name {
  overflow: hidden;
  text-overflow: ellipsis;
}
.point-row input[type='text'] {
  min-width: 0;
  width: 100%;
}
.point-row input[type='checkbox'] {
  justify-self: start;
}
.point-row.selected {
  margin-inline: calc(-1 * var(--space-2));
  padding-inline: var(--space-2);
  background: var(--color-surface-raised);
  outline: var(--border-width-default) solid var(--color-action-primary-text);
}
.point-row output {
  grid-column: 2 / 4;
  text-align: right;
}
.point-row small {
  color: var(--color-text-muted);
}
</style>
