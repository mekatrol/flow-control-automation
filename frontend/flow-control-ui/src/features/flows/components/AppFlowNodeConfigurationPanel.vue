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

      <label v-for="field in nodeEditorFields" :key="field.key">
        <span>{{ field.label }}</span>
        <template v-if="field.key === 'pointId'">
          <input
            type="text"
            :value="node.configuration.pointId"
            :list="`${node.id}-compatible-points`"
            autocomplete="off"
            :aria-invalid="Boolean(errors.pointId)"
            aria-describedby="point-lookup-help"
            @input="updatePointId(field, $event)"
            @blur="validatePointId"
          />
          <datalist :id="`${node.id}-compatible-points`">
            <option v-for="point in compatibleVirtualPoints" :key="point.key" :value="point.key">
              {{ point.key }} · virtual · {{ point.valueType
              }}{{ point.units ? ` · ${point.units}` : '' }}
            </option>
          </datalist>
          <small v-if="!errors.pointId" id="point-lookup-help" class="field-help">
            Search declared compatible points or enter a point ID manually.
          </small>
        </template>
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
  FlowNode,
  VirtualPointDeclaration
} from '@/features/flows/types';

const props = defineProps<{
  automation: string;
  node: FlowNode;
  virtualPointDeclarations?: VirtualPointDeclaration[];
}>();
const emit = defineEmits<{
  (event: typeof EVENTS.UPDATE_LABEL, label: string): void;
  (event: typeof EVENTS.UPDATE_CONFIGURATION, key: string, value: FlowConfigurationValue): void;
}>();

const automation = useAutomation(props.automation);
const definition = computed(() => getNodeKind(props.node.kind));
const nodeEditorFields = computed(() => definition.value.editor);
const errors = ref<Record<string, string>>({});
const compatibleVirtualPoints = computed(() => {
  const analog = props.node.kind === 'analogInput' || props.node.kind === 'analogOutput';
  const input = props.node.kind === 'analogInput' || props.node.kind === 'digitalInput';
  return (props.virtualPointDeclarations ?? []).filter(
    (point) =>
      point.valueType === (analog ? 'analog' : 'digital') &&
      (input ? point.readable : point.commandable)
  );
});

const pointIdError = (value: string): string | undefined => {
  const key = value.trim();
  if (!key) return 'Point ID is required.';
  if (!/^[a-zA-Z0-9](?:[a-zA-Z0-9._-]{0,126}[a-zA-Z0-9])?$/.test(key))
    return 'Point ID contains unsupported characters.';
  const declared = (props.virtualPointDeclarations ?? []).find((point) => point.key === key);
  if (!declared) return undefined;
  return compatibleVirtualPoints.value.includes(declared)
    ? undefined
    : `Virtual point “${key}” is incompatible with this ${definition.value.label.toLocaleLowerCase()}.`;
};

const validatePointId = (): void => {
  const error = pointIdError(String(props.node.configuration.pointId ?? ''));
  if (error) errors.value.pointId = error;
  else delete errors.value.pointId;
};

const updatePointId = (field: NodeEditorField, event: Event): void => {
  const target = event.target as HTMLInputElement;
  const error = pointIdError(target.value);
  if (error) errors.value.pointId = error;
  else {
    delete errors.value.pointId;
    emit(EVENTS.UPDATE_CONFIGURATION, field.key, target.value.trim());
  }
};

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

.field-help {
  color: var(--color-text-subtle);
  font-weight: var(--font-weight-regular);
}
</style>
