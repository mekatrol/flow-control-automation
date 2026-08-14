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
      <label>
        <span>Node label</span>
        <input
          :value="node.label"
          type="text"
          :aria-invalid="Boolean(errors.label)"
          @input="updateLabel"
        />
        <small v-if="errors.label" role="alert">{{ errors.label }}</small>
      </label>

      <label v-for="field in definition.editor" :key="field.key">
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
import type { FlowConfigurationValue, FlowInterface, FlowNode } from '@/features/flows/types';

const props = defineProps<{ automation: string; node: FlowNode; flowInterface?: FlowInterface }>();
const emit = defineEmits<{
  (event: typeof EVENTS.UPDATE_LABEL, label: string): void;
  (event: typeof EVENTS.UPDATE_CONFIGURATION, key: string, value: FlowConfigurationValue): void;
}>();

const automation = useAutomation(props.automation);
const definition = computed(() => getNodeKind(props.node.kind));
const interfaceEntries = computed(() =>
  props.node.kind === 'flowInput'
    ? (props.flowInterface?.inputs ?? [])
    : (props.flowInterface?.outputs ?? [])
);
const errors = ref<Record<string, string>>({});

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
