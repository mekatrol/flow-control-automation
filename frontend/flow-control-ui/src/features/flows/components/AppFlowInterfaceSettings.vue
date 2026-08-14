<template>
  <section v-bind="automation()" class="interface-settings" aria-labelledby="flow-interface-title">
    <div class="heading">
      <div>
        <h2 id="flow-interface-title">Flow inputs and outputs</h2>
        <p>Portable terminals for simulation and reusable flows.</p>
      </div>
      <AppButton automation="flow-interface-add-input" text="Add input" @click="addInput" />
      <AppButton automation="flow-interface-add-output" text="Add output" @click="addOutput" />
    </div>
    <p v-if="error" role="alert" class="error">{{ error }}</p>
    <div class="entries">
      <fieldset v-for="entry in draft.inputs" :key="entry.id">
        <legend>Input · {{ entry.id }}</legend>
        <label>Name <input v-model="entry.name" @change="publish" /></label>
        <label
        >Type
          <select v-model="entry.dataType" @change="changeType(entry)">
            <option v-for="type in dataTypes" :key="type">{{ type }}</option>
          </select></label
        >
        <label v-if="entry.dataType === 'number'"
        >Units <input v-model="entry.units" @change="publish"
        /></label>
        <label v-if="entry.dataType === 'boolean'"
        >Default
          <select v-model="entry.defaultValue" @change="publish">
            <option :value="false">False</option>
            <option :value="true">True</option>
          </select></label
        >
        <label v-else-if="entry.dataType === 'number'"
        >Default
          <input v-model.number="entry.defaultValue" type="number" @change="publish" />
        </label>
        <label v-else-if="entry.dataType === 'string'"
        >Default <input v-model="entry.defaultValue" @change="publish"
        /></label>
        <label><input v-model="entry.required" type="checkbox" @change="publish" /> Required</label>
        <AppButton
          :automation="`flow-interface-move-up-${entry.id}`"
          text="Move input up"
          :disabled="draft.inputs[0]?.id === entry.id"
          @click="moveInput(entry.id, -1)"
        />
        <AppButton
          :automation="`flow-interface-move-down-${entry.id}`"
          text="Move input down"
          :disabled="draft.inputs.at(-1)?.id === entry.id"
          @click="moveInput(entry.id, 1)"
        />
        <AppButton
          :automation="`flow-interface-remove-${entry.id}`"
          text="Remove input"
          @click="removeInput(entry.id)"
        />
      </fieldset>
      <fieldset v-for="entry in draft.outputs" :key="entry.id">
        <legend>Output · {{ entry.id }}</legend>
        <label>Name <input v-model="entry.name" @change="publish" /></label>
        <label
        >Type
          <select v-model="entry.dataType" @change="changeType(entry)">
            <option v-for="type in dataTypes" :key="type">{{ type }}</option>
          </select></label
        >
        <label v-if="entry.dataType === 'number'"
        >Units <input v-model="entry.units" @change="publish"
        /></label>
        <AppButton
          :automation="`flow-interface-move-up-${entry.id}`"
          text="Move output up"
          :disabled="draft.outputs[0]?.id === entry.id"
          @click="moveOutput(entry.id, -1)"
        />
        <AppButton
          :automation="`flow-interface-move-down-${entry.id}`"
          text="Move output down"
          :disabled="draft.outputs.at(-1)?.id === entry.id"
          @click="moveOutput(entry.id, 1)"
        />
        <AppButton
          :automation="`flow-interface-remove-${entry.id}`"
          text="Remove output"
          @click="removeOutput(entry.id)"
        />
      </fieldset>
    </div>
  </section>
</template>

<script setup lang="ts">
import { ref, watch } from 'vue';
import AppButton from '@/components/AppButton.vue';
import { useAutomation } from '@/composables/useAutomation';
import { EVENTS } from '@/constants/events';
import type {
  FlowInterface,
  FlowInterfaceDataType,
  FlowInterfaceInput,
  FlowInterfaceOutput
} from '@/features/flows/types';

const props = defineProps<{
  automation: string;
  modelValue: FlowInterface;
  referencedInputIds?: string[];
  referencedOutputIds?: string[];
}>();
const emit = defineEmits<{ (event: typeof EVENTS.UPDATE_INTERFACE, value: FlowInterface): void }>();
const automation = useAutomation(props.automation);
const draft = ref<FlowInterface>(structuredClone(props.modelValue));
const error = ref<string>();
const dataTypes: FlowInterfaceDataType[] = ['boolean', 'number', 'string', 'event'];
watch(
  () => props.modelValue,
  (value) => {
    draft.value = structuredClone(value);
  },
  { deep: true }
);
const nextId = (prefix: string): string => {
  const used = new Set([...draft.value.inputs, ...draft.value.outputs].map((entry) => entry.id));
  let suffix = 1;
  while (used.has(`${prefix}-${suffix}`)) suffix += 1;
  return `${prefix}-${suffix}`;
};
const publish = (): void => {
  const names = [...draft.value.inputs, ...draft.value.outputs].map((entry) =>
    entry.name.trim().toLocaleLowerCase()
  );
  error.value = names.some((name) => name.length === 0)
    ? 'Interface names cannot be empty.'
    : new Set(names).size !== names.length
      ? 'Interface names must be unique.'
      : undefined;
  if (error.value) return;
  emit(EVENTS.UPDATE_INTERFACE, structuredClone(draft.value));
};
const move = <Entry extends FlowInterfaceInput | FlowInterfaceOutput>(
  entries: Entry[],
  id: string,
  offset: number
): void => {
  const from = entries.findIndex((entry) => entry.id === id);
  const to = from + offset;
  if (from < 0 || to < 0 || to >= entries.length) return;
  const [entry] = entries.splice(from, 1);
  if (entry) entries.splice(to, 0, entry);
  publish();
};
const moveInput = (id: string, offset: number): void => move(draft.value.inputs, id, offset);
const moveOutput = (id: string, offset: number): void => move(draft.value.outputs, id, offset);
const addInput = (): void => {
  draft.value.inputs.push({
    id: nextId('input'),
    name: 'New input',
    dataType: 'boolean',
    defaultValue: false,
    required: false
  });
  publish();
};
const addOutput = (): void => {
  draft.value.outputs.push({ id: nextId('output'), name: 'New output', dataType: 'boolean' });
  publish();
};
const removeInput = (id: string): void => {
  if (props.referencedInputIds?.includes(id)) {
    error.value = `Input ${id} is referenced by a node.`;
    return;
  }
  draft.value.inputs = draft.value.inputs.filter((entry) => entry.id !== id);
  publish();
};
const removeOutput = (id: string): void => {
  if (props.referencedOutputIds?.includes(id)) {
    error.value = `Output ${id} is referenced by a node.`;
    return;
  }
  draft.value.outputs = draft.value.outputs.filter((entry) => entry.id !== id);
  publish();
};
const changeType = (entry: FlowInterfaceInput | FlowInterfaceOutput): void => {
  if (entry.dataType !== 'number') delete entry.units;
  if ('required' in entry)
    entry.defaultValue =
      entry.dataType === 'boolean'
        ? false
        : entry.dataType === 'number'
          ? 0
          : entry.dataType === 'string'
            ? ''
            : null;
  publish();
};
</script>

<style scoped>
.interface-settings {
  margin-bottom: var(--space-4);
  padding: var(--space-4);
  border: var(--border-width-default) solid var(--color-border-subtle);
  border-radius: var(--radius-lg);
}
.heading,
.entries,
fieldset,
label {
  display: flex;
  flex-wrap: wrap;
  gap: var(--space-3);
  align-items: center;
}
.heading h2,
.heading p {
  margin: 0;
}
.heading div {
  margin-right: auto;
}
fieldset {
  min-width: min(100%, 24rem);
}
input,
select {
  min-height: var(--control-min-height);
}
.error {
  color: var(--color-danger-text);
}
</style>
