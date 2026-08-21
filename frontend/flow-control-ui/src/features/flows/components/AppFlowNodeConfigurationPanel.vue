<template>
  <aside v-bind="automation()" class="configuration-panel" aria-label="Node configuration">
    <div class="panel-heading">
      <AppSvg :src="getNodeIconUrl(definition.icon)" v-bind="automation('icon')" :size="22" />
      <div class="heading-copy">
        <strong>Configure {{ definition.label }}</strong>
        <small>{{ node.id }}</small>
      </div>
    </div>

    <div class="fields">
      <label v-if="!interfaceEntry">
        <span>Node label</span>
        <input
          :value="node.label"
          type="text"
          :aria-invalid="Boolean(errors.label)"
          @input="updateLabel"
        />
        <small v-if="errors.label" role="alert">{{ errors.label }}</small>
      </label>

      <template v-if="interfaceEntry">
        <label>
          <span>{{ node.kind === 'flowInput' ? 'Input name' : 'Output name' }}</span>
          <input :value="interfaceEntry.name" type="text" @change="updateInterfaceName" />
        </label>
        <label>
          <span>Data type</span>
          <select :value="interfaceEntry.dataType" @change="updateInterfaceType">
            <option v-for="type in interfaceDataTypes" :key="type" :value="type">{{ type }}</option>
          </select>
        </label>
        <label v-if="interfaceEntry.dataType === 'number'">
          <span>Units</span>
          <input :value="interfaceEntry.units ?? ''" type="text" @change="updateInterfaceUnits" />
        </label>
        <label v-if="node.kind === 'flowInput' && interfaceInput">
          <span>Default value</span>
          <select
            v-if="interfaceInput.dataType === 'boolean'"
            :value="String(interfaceInput.defaultValue ?? false)"
            @change="updateBooleanDefault"
          >
            <option value="false">False</option>
            <option value="true">True</option>
          </select>
          <input
            v-else-if="interfaceInput.dataType === 'number'"
            :value="interfaceInput.defaultValue ?? 0"
            type="number"
            @change="updateNumberDefault"
          />
          <input
            v-else-if="interfaceInput.dataType === 'string'"
            :value="interfaceInput.defaultValue ?? ''"
            type="text"
            @change="updateStringDefault"
          />
          <span v-else>Events do not have a default value.</span>
        </label>
        <label v-if="node.kind === 'flowInput' && interfaceInput" class="checkbox-field">
          <input
            type="checkbox"
            :checked="interfaceInput.required"
            @change="updateInterfaceRequired"
          />
          <span>Required input</span>
        </label>
      </template>

      <label v-for="field in nodeEditorFields" :key="field.key">
        <span>{{ field.label }}</span>
        <select
          v-if="field.key === 'interfaceId'"
          :value="node.configuration.interfaceId"
          :aria-invalid="Boolean(errors.interfaceId)"
          @change="updateField(field, $event)"
        >
          <option value="">Choose an interface entry</option>
          <option v-for="entry in interfaceEntries" :key="entry.id" :value="entry.id">
            {{ entry.name }} · {{ entry.dataType }}{{ entry.units ? ` · ${entry.units}` : '' }}
          </option>
        </select>
        <input
          v-else-if="field.input === 'checkbox'"
          type="checkbox"
          :checked="Boolean(node.configuration[field.key])"
          @change="updateField(field, $event)"
        />
        <input
          v-else-if="field.input === 'number'"
          type="number"
          :value="node.configuration[field.key]"
          :aria-invalid="Boolean(errors[field.key])"
          @input="updateField(field, $event)"
        />
        <input
          v-else-if="field.input === 'text'"
          type="text"
          :value="node.configuration[field.key]"
          :aria-invalid="Boolean(errors[field.key])"
          @input="updateField(field, $event)"
        />
        <select
          v-else
          :value="node.configuration[field.key]"
          :aria-invalid="Boolean(errors[field.key])"
          @change="updateField(field, $event)"
        >
          <option v-for="option in field.options" :key="option" :value="option">
            {{ option }}
          </option>
        </select>
        <small v-if="errors[field.key]" role="alert">{{ errors[field.key] }}</small>
      </label>
    </div>
  </aside>
</template>

<script lang="ts">
import type { FlowConfigurationValue as EditorValue } from '@/features/flows/types';
import type { NodeEditorField as EditorField } from '@/features/flows/nodeKinds';

export const validateNodeLabel = (label: string): string | undefined =>
  label.trim() ? undefined : 'Node label is required.';

export const editorValueFromInput = (
  field: EditorField,
  target: HTMLInputElement | HTMLSelectElement
): { value?: EditorValue; error?: string } => {
  // Browser form controls expose text even when an input visually represents a
  // number. Convert and validate here before values enter the persisted graph.
  if (field.input === 'checkbox' && target instanceof HTMLInputElement) {
    return { value: target.checked };
  }
  if (field.input === 'number') {
    if (target.value.trim() === '') return { error: `${field.label} is required.` };
    const value = Number(target.value);
    return Number.isFinite(value) ? { value } : { error: `${field.label} must be a number.` };
  }
  if (field.options && !field.options.includes(target.value)) {
    return { error: `Choose a valid ${field.label.toLocaleLowerCase()}.` };
  }
  return { value: target.value };
};
</script>

<script setup lang="ts">
import { computed, ref } from 'vue';

import AppSvg from '@/components/AppSvg.vue';
import { useAutomation } from '@/composables/useAutomation';
import { EVENTS } from '@/constants/events';
import { getNodeIconUrl, getNodeKind } from '@/features/flows/nodeKinds';
import type { NodeEditorField } from '@/features/flows/nodeKinds';
import type {
  FlowConfigurationValue,
  FlowInterface,
  FlowInterfaceDataType,
  FlowInterfaceInput,
  FlowInterfaceOutput,
  FlowNode
} from '@/features/flows/types';

const props = defineProps<{ automation: string; node: FlowNode; flowInterface?: FlowInterface }>();
const emit = defineEmits<{
  (event: typeof EVENTS.UPDATE_LABEL, label: string): void;
  (event: typeof EVENTS.UPDATE_CONFIGURATION, key: string, value: FlowConfigurationValue): void;
  (event: typeof EVENTS.UPDATE_INTERFACE, value: FlowInterface): void;
}>();

const automation = useAutomation(props.automation);
const definition = computed(() => getNodeKind(props.node.kind));
const interfaceEntries = computed(() =>
  props.node.kind === 'flowInput'
    ? (props.flowInterface?.inputs ?? [])
    : (props.flowInterface?.outputs ?? [])
);
const interfaceEntry = computed(() =>
  interfaceEntries.value.find((entry) => entry.id === props.node.configuration.interfaceId)
);
const interfaceInput = computed((): FlowInterfaceInput | undefined =>
  props.node.kind === 'flowInput'
    ? (interfaceEntry.value as FlowInterfaceInput | undefined)
    : undefined
);
const nodeEditorFields = computed(() =>
  definition.value.editor.filter((field) => field.key !== 'interfaceId')
);
const interfaceDataTypes: FlowInterfaceDataType[] = ['boolean', 'number', 'string', 'event'];
const errors = ref<Record<string, string>>({});

const updateInterfaceEntry = (
  update: (entry: FlowInterfaceInput | FlowInterfaceOutput) => void
): void => {
  if (!props.flowInterface || !interfaceEntry.value) return;
  const next: FlowInterface = {
    schemaVersion: props.flowInterface.schemaVersion,
    inputs: props.flowInterface.inputs.map((entry) => ({ ...entry })),
    outputs: props.flowInterface.outputs.map((entry) => ({ ...entry }))
  };
  const entries = props.node.kind === 'flowInput' ? next.inputs : next.outputs;
  const entry = entries.find((candidate) => candidate.id === interfaceEntry.value?.id);
  if (!entry) return;
  update(entry);
  emit(EVENTS.UPDATE_INTERFACE, next);
};

const updateInterfaceName = (event: Event): void => {
  const name = (event.target as HTMLInputElement).value.trim();
  if (name) updateInterfaceEntry((entry) => (entry.name = name));
};
const updateInterfaceType = (event: Event): void => {
  const dataType = (event.target as HTMLSelectElement).value as FlowInterfaceDataType;
  updateInterfaceEntry((entry) => {
    entry.dataType = dataType;
    if (dataType !== 'number') delete entry.units;
    if ('required' in entry)
      entry.defaultValue =
        dataType === 'boolean'
          ? false
          : dataType === 'number'
            ? 0
            : dataType === 'string'
              ? ''
              : null;
  });
};
const updateInterfaceUnits = (event: Event): void => {
  const units = (event.target as HTMLInputElement).value.trim();
  updateInterfaceEntry((entry) => {
    if (units) entry.units = units;
    else delete entry.units;
  });
};
const updateBooleanDefault = (event: Event): void =>
  updateInterfaceEntry((entry) => {
    if ('required' in entry)
      entry.defaultValue = (event.target as HTMLSelectElement).value === 'true';
  });
const updateNumberDefault = (event: Event): void =>
  updateInterfaceEntry((entry) => {
    if ('required' in entry) entry.defaultValue = Number((event.target as HTMLInputElement).value);
  });
const updateStringDefault = (event: Event): void =>
  updateInterfaceEntry((entry) => {
    if ('required' in entry) entry.defaultValue = (event.target as HTMLInputElement).value;
  });
const updateInterfaceRequired = (event: Event): void =>
  updateInterfaceEntry((entry) => {
    if ('required' in entry) entry.required = (event.target as HTMLInputElement).checked;
  });

const updateLabel = (event: Event): void => {
  const label = (event.target as HTMLInputElement).value;
  const error = validateNodeLabel(label);
  // Invalid drafts stay in the form control for correction but are not emitted,
  // so the last valid graph value remains safe to save or discard.
  if (error) errors.value.label = error;
  else {
    delete errors.value.label;
    emit(EVENTS.UPDATE_LABEL, label.trim());
  }
};

const updateField = (field: NodeEditorField, event: Event): void => {
  const result = editorValueFromInput(field, event.target as HTMLInputElement | HTMLSelectElement);
  if (result.error) errors.value[field.key] = result.error;
  else {
    delete errors.value[field.key];
    emit(EVENTS.UPDATE_CONFIGURATION, field.key, result.value!);
  }
};
</script>

<style scoped>
.configuration-panel {
  min-height: 0;
  padding: var(--space-8);
  overflow-y: auto;
  overscroll-behavior-y: contain;
  background: var(--color-surface-subtle);
  border-left: var(--border-width-default) solid var(--color-border-subtle);
  scrollbar-gutter: stable;
}

.panel-heading {
  display: flex;
  gap: var(--space-4-5);
  align-items: flex-start;
  color: var(--color-palette-heading);
}

.heading-copy {
  display: grid;
  min-width: 0;
}

.panel-heading small {
  color: var(--color-text-subtle);
  font-size: var(--font-size-xs);
  overflow-wrap: anywhere;
}

.fields {
  display: grid;
  gap: var(--space-6-5);
  margin-top: var(--space-9);
}

label {
  display: grid;
  gap: var(--space-1-5);
  min-width: 0;
  color: var(--color-text-secondary);
  font-size: var(--font-size-xs);
  font-weight: var(--font-weight-bold);
}

.checkbox-field {
  display: flex;
  align-items: center;
}

input:not([type='checkbox']),
select {
  width: 100%;
  min-height: 32px;
  padding: var(--space-2-5) var(--space-3-5);
  background: var(--color-surface-raised);
  border: var(--border-width-default) solid var(--color-action-primary-border-strong);
  border-radius: var(--radius-sm);
}

[aria-invalid='true'] {
  border-color: var(--color-danger-border) !important;
}

label small {
  color: var(--color-danger-strong);
}
</style>
