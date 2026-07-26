<template>
  <aside class="configuration-panel" aria-label="Node configuration">
    <div class="panel-heading">
      <img :src="getNodeIconUrl(definition.icon)" alt="" />
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
        <input
          v-if="field.input === 'checkbox'"
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

import { getNodeIconUrl, getNodeKind } from '@/features/flows/nodeKinds';
import type { NodeEditorField } from '@/features/flows/nodeKinds';
import type { FlowConfigurationValue, FlowNode } from '@/features/flows/types';

const props = defineProps<{ node: FlowNode }>();
const emit = defineEmits<{
  updateLabel: [label: string];
  updateConfiguration: [key: string, value: FlowConfigurationValue];
}>();

const definition = computed(() => getNodeKind(props.node.kind));
const errors = ref<Record<string, string>>({});

const updateLabel = (event: Event): void => {
  const label = (event.target as HTMLInputElement).value;
  const error = validateNodeLabel(label);
  // Invalid drafts stay in the form control for correction but are not emitted,
  // so the last valid graph value remains safe to save or discard.
  if (error) errors.value.label = error;
  else {
    delete errors.value.label;
    emit('updateLabel', label.trim());
  }
};

const updateField = (field: NodeEditorField, event: Event): void => {
  const result = editorValueFromInput(field, event.target as HTMLInputElement | HTMLSelectElement);
  if (result.error) errors.value[field.key] = result.error;
  else {
    delete errors.value[field.key];
    emit('updateConfiguration', field.key, result.value!);
  }
};
</script>

<style scoped>
.configuration-panel {
  min-height: 0;
  padding: 16px;
  overflow-y: auto;
  overscroll-behavior-y: contain;
  background: var(--color-surface-subtle);
  border-left: 1px solid var(--color-border-subtle);
  scrollbar-gutter: stable;
}

.panel-heading {
  display: flex;
  gap: 10px;
  align-items: flex-start;
  color: var(--color-palette-heading);
}

.heading-copy {
  display: grid;
  min-width: 0;
}

.panel-heading small {
  color: var(--color-text-subtle);
  font-size: 10px;
  overflow-wrap: anywhere;
}

.panel-heading img {
  flex: 0 0 22px;
  width: 22px;
  height: 22px;
  filter: var(--filter-node-icon-foreground);
}

.fields {
  display: grid;
  gap: 14px;
  margin-top: 18px;
}

label {
  display: grid;
  gap: 4px;
  min-width: 0;
  color: var(--color-text-secondary);
  font-size: 10px;
  font-weight: 700;
}

input:not([type='checkbox']),
select {
  width: 100%;
  min-height: 32px;
  padding: 6px 8px;
  background: var(--color-surface-raised);
  border: 1px solid var(--color-action-primary-border-strong);
  border-radius: 6px;
}

[aria-invalid='true'] {
  border-color: var(--color-danger-border) !important;
}

label small {
  color: var(--color-danger-strong);
}
</style>
