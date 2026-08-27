<template>
  <section
    v-if="inputs.length || outputs.length"
    class="simulator-io"
    aria-label="Simulated virtual points"
  >
    <strong>Virtual points</strong>
    <label v-for="input in inputs" :key="input.pointId">
      <span>{{ input.pointId }}</span>
      <input
        v-if="isBoolean(input.typedValue)"
        v-model="draft[input.pointId]!.boolean"
        type="checkbox"
        @change="markDirty(input.pointId)"
      />
      <input
        v-else
        v-model.number="draft[input.pointId]!.number"
        type="number"
        :aria-label="`${input.pointId} simulated value`"
        @input="markDirty(input.pointId)"
      />
      <small>{{ units(input.pointId) }}</small>
    </label>
    <AppButton v-if="inputs.length" text="Apply inputs" :icon="applyIcon" @click="apply" />
    <output v-for="output in outputs" :key="output.outputId">
      <span>{{ output.outputId }}</span>
      <strong>{{ display(output.effectiveValue) }} {{ output.units ?? '' }}</strong>
    </output>
  </section>
</template>

<script setup lang="ts">
import { computed, reactive, watch } from 'vue';
import applyIcon from '@/assets/icons/check-icon.svg';
import AppButton from '@/components/AppButton.vue';
import type {
  EmulatorInputChange,
  EmulatorSnapshot,
  EmulatorValue
} from '@/features/flows/api/flowEmulatorApi';
import type { FlowDefinition } from '@/features/flows/types';

const props = defineProps<{ flow: FlowDefinition; snapshot: EmulatorSnapshot }>();
const emit = defineEmits<{ (event: 'apply', inputs: EmulatorInputChange[]): void }>();
const draft = reactive<Record<string, EmulatorValue>>({});
const dirty = reactive(new Set<string>());
const declarations = computed(
  () => new Map((props.flow.virtualPointDeclarations ?? []).map((point) => [point.key, point]))
);
const inputPointIds = computed(
  () =>
    new Set(
      props.flow.nodes
        .filter((node) => node.kind === 'analogInput' || node.kind === 'digitalInput')
        .map((node) => String(node.configuration.pointId ?? ''))
    )
);
const inputs = computed(() => {
  const values = new Map(props.snapshot.inputs.map((input) => [input.pointId, input]));
  for (const pointId of inputPointIds.value) {
    const declaration = declarations.value.get(pointId);
    if (!declaration || values.has(pointId)) continue;
    const numeric = declaration.valueType === 'analog';
    values.set(pointId, {
      pointId,
      typedValue: {
        type: numeric ? 'number' : 'boolean',
        boolean: numeric ? false : Boolean(declaration.relinquishDefault),
        number:
          numeric && typeof declaration.relinquishDefault === 'number'
            ? declaration.relinquishDefault
            : 0,
        quality: 'good'
      }
    });
  }
  return [...values.values()].filter(
    (input) => declarations.value.has(input.pointId) && inputPointIds.value.has(input.pointId)
  );
});
const outputs = computed(() => {
  const latest = new Map<string, EmulatorSnapshot['outputHistory'][number]>();
  for (const output of props.snapshot.outputHistory)
    if (declarations.value.has(output.outputId)) latest.set(output.outputId, output);
  return [...latest.values()];
});
watch(
  inputs,
  (values) =>
    values.forEach((input) => {
      if (!dirty.has(input.pointId)) draft[input.pointId] = { ...input.typedValue };
    }),
  { immediate: true, deep: true }
);
const isBoolean = (value: EmulatorValue): boolean =>
  value.type === 'boolean' ||
  (value as EmulatorValue & { dataType?: string }).dataType === 'boolean';
const markDirty = (pointId: string): void => {
  dirty.add(pointId);
};
const units = (pointId: string): string => declarations.value.get(pointId)?.units ?? '';
const display = (value: EmulatorValue): string =>
  value.type === 'number' ? String(value.number) : value.boolean ? 'On' : 'Off';
const apply = (): void =>
  emit(
    'apply',
    inputs.value.map((input) => ({
      inputId: input.pointId,
      typedValue: { ...draft[input.pointId]! }
    }))
  );
</script>

<style scoped>
.simulator-io {
  display: flex;
  flex-wrap: wrap;
  gap: var(--space-3);
  align-items: center;
  padding: var(--space-3) var(--space-4);
  background: var(--color-surface-subtle);
  border-bottom: var(--border-width-default) solid var(--color-border-subtle);
}
.simulator-io label,
.simulator-io output {
  display: flex;
  gap: var(--space-2);
  align-items: center;
}
.simulator-io input[type='number'] {
  width: 8rem;
}
.simulator-io small {
  color: var(--color-text-muted);
}
</style>
