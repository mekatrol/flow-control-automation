<template>
  <aside class="configuration-panel" aria-label="Node configuration">
    <div class="panel-heading">
      <AppSvg :src="getNodeIconUrl(definition.icon)" :size="22" />
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

      <AppWeeklyScheduleEditor
        v-if="scheduleField"
        :value="node.configuration[scheduleField.key]"
        @update="emit(EVENTS.UPDATE_CONFIGURATION, scheduleField.key, $event)"
      />

      <label v-for="field in standardEditorFields" :key="field.key">
        <span>{{ field.label }}</span>
        <template v-if="field.key === 'pointSourceId' && !isVirtualPointNode(node)">
          <select
            :value="node.configuration.pointSourceId"
            :aria-invalid="Boolean(errors.pointSourceId)"
            @change="updatePointSourceId($event)"
          >
            <option value="">Choose a point source</option>
            <option v-for="source in pointSources" :key="source.id" :value="source.id">
              {{ source.id }} — {{ source.name }}
            </option>
          </select>
        </template>
        <template v-else-if="field.key === 'pointId' && !isVirtualPointNode(node)">
          <select
            :value="node.configuration.pointId"
            :disabled="!validPointSourceId"
            :aria-invalid="Boolean(errors.pointId)"
            aria-describedby="point-lookup-help"
            @change="updatePointId(field, $event)"
          >
            <option value="">Choose a point</option>
            <option v-for="point in compatiblePoints" :key="point.id" :value="point.id">
              {{ point.id }} — {{ point.name }} · {{ point.valueType
              }}{{ point.units ? ` · ${point.units}` : '' }}
            </option>
          </select>
          <small v-if="validationState === 'pending'" role="status" class="field-help"
            >Validating point…</small
          >
          <small v-else-if="!errors.pointId" id="point-lookup-help" class="field-help">
            Choose a compatible point declared by the selected point source.
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
import type { NodeEditorField as EditorField } from '@/features/flows/nodeTypes';

export const validateNodeLabel = (label: string): string | undefined =>
  label.trim() ? undefined : 'Node label is required.';

// A separator at the end is a valid intermediate state while entering an ID
// such as "room-temperature". Final validation still requires an alphanumeric
// final character.
export const validatePointIdDraft = (value: string): string | undefined => {
  const key = value.trim();
  if (!key) return 'Point ID is required.';
  if (!/^[a-zA-Z0-9][a-zA-Z0-9._-]{0,127}$/.test(key))
    return 'Point ID contains unsupported characters.';
};

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
import { isVirtualPointNode } from '@/features/flows/types';
import { computed, onBeforeUnmount, ref, watch } from 'vue';

import AppSvg from '@/components/AppSvg.vue';
import AppWeeklyScheduleEditor from '@/features/flows/components/AppWeeklyScheduleEditor.vue';
import { EVENTS } from '@/constants/events';
import { getNodeIconUrl, getNodeTypeDefinition } from '@/features/flows/nodeTypes';
import type { NodeEditorField } from '@/features/flows/nodeTypes';
import type {
  FlowConfigurationValue,
  FlowNode,
  VirtualPointDefinition
} from '@/features/flows/types';
import type { PointSummary } from '@/features/points/api/pointDto';
import { pointApi } from '@/features/points/api/pointApi';
import {
  pointSourceApi,
  type PointSourceSummary
} from '@/features/pointSources/api/pointSourceApi';
import {
  pointCompatibilityError,
  validatePointReference,
  type PointValidationState
} from '@/features/flows/flowPointValidation';

const props = defineProps<{
  node: FlowNode;
  virtualPointDefinitions?: VirtualPointDefinition[];
  contextPointContracts?: VirtualPointDefinition[];
  executionContextId?: string;
}>();
const emit = defineEmits<{
  (event: typeof EVENTS.UPDATE_LABEL, label: string): void;
  (event: typeof EVENTS.UPDATE_CONFIGURATION, key: string, value: FlowConfigurationValue): void;
  (event: 'validation', nodeId: string, state: PointValidationState): void;
}>();

const definition = computed(() => getNodeTypeDefinition(props.node.nodeType));
const nodeEditorFields = computed(() => definition.value.editor);
const scheduleField = computed(() =>
  nodeEditorFields.value.find((field) => field.input === 'schedule')
);
const standardEditorFields = computed(() =>
  nodeEditorFields.value.filter((field) => field.input !== 'schedule')
);
const errors = ref<Record<string, string>>({});
const definitions = computed(() => {
  const merged = new Map<string, VirtualPointDefinition>();
  for (const point of [
    ...(props.contextPointContracts ?? []),
    ...(props.virtualPointDefinitions ?? [])
  ])
    merged.set(point.key, point);
  return [...merged.values()];
});
const remotePoints = ref<PointSummary[]>([]);
const pointSources = ref<PointSourceSummary[]>([]);
const pointDraft = ref(String(props.node.configuration.pointId ?? ''));
const validationState = ref<PointValidationState>('idle');
let lookupController: AbortController | undefined;
const validPointSourceId = computed(() =>
  pointSources.value.some((source) => source.id === props.node.configuration.pointSourceId)
);
const compatiblePoints = computed(() =>
  remotePoints.value.filter(
    (point) =>
      point.sourceId === props.node.configuration.pointSourceId &&
      !pointCompatibilityError(props.node, point)
  )
);

const pointIdError = (value: string): string | undefined => {
  const key = value.trim();
  if (!key) return 'Point ID is required.';
  if (!/^[a-zA-Z0-9](?:[a-zA-Z0-9._-]{0,126}[a-zA-Z0-9])?$/.test(key))
    return 'Point ID contains unsupported characters.';
  const declared = definitions.value.find((point) => point.key === key);
  if (!declared) return undefined;
  return pointCompatibilityError(props.node, declared);
};

const validatePointId = async (): Promise<void> => {
  const localError = pointIdError(pointDraft.value);
  if (localError) {
    errors.value.pointId = localError;
    validationState.value = 'invalid';
  } else {
    validationState.value = 'pending';
    emit('validation', props.node.id, 'pending');
    lookupController?.abort();
    lookupController = new AbortController();
    try {
      const result = await validatePointReference(
        props.node,
        definitions.value,
        lookupController.signal,
        props.executionContextId
      );
      validationState.value = result.state;
      if (result.message) errors.value.pointId = result.message;
      else delete errors.value.pointId;
    } catch {
      return;
    }
  }
  emit('validation', props.node.id, validationState.value);
};

const updatePointId = (field: NodeEditorField, event: Event): void => {
  const target = event.target as HTMLSelectElement;
  pointDraft.value = target.value;
  emit(EVENTS.UPDATE_CONFIGURATION, field.key, target.value);
  void validatePointId();
};

const updatePointSourceId = (event: Event): void => {
  const sourceId = (event.target as HTMLSelectElement).value;
  emit(EVENTS.UPDATE_CONFIGURATION, 'pointSourceId', sourceId);
  emit(EVENTS.UPDATE_CONFIGURATION, 'pointId', '');
  pointDraft.value = '';
};

const loadPoints = async (sourceId: FlowConfigurationValue | undefined): Promise<void> => {
  remotePoints.value = [];
  if (!pointSources.value.some((source) => source.id === sourceId)) return;
  try {
    remotePoints.value = (await pointApi.list({ filter: '', page: 1, pageSize: 50 })).items;
  } catch {
    remotePoints.value = [];
  }
};

watch(
  () => props.node.id,
  () => {
    pointDraft.value = String(props.node.configuration.pointId ?? '');
    void validatePointId();
  },
  { immediate: true }
);
watch(definitions, () => void validatePointId());
watch(
  () => props.node.configuration.pointSourceId,
  (sourceId) => loadPoints(sourceId),
  { immediate: true }
);

void pointSourceApi
  .list()
  .then((page) => {
    pointSources.value = page.items.filter((source) => source.enabled);
    void loadPoints(props.node.configuration.pointSourceId);
  })
  .catch(() => {
    pointSources.value = [];
  });
onBeforeUnmount(() => {
  lookupController?.abort();
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

.field-help {
  color: var(--color-text-subtle);
  font-weight: var(--font-weight-regular);
}
.create-point-button {
  justify-self: start;
}
.create-point-form {
  display: grid;
  gap: var(--space-3);
  padding: var(--space-4);
  border: var(--border-width-default) solid var(--color-border-subtle);
}
.create-point-actions {
  display: flex;
  gap: var(--space-2);
  justify-content: flex-end;
}
</style>
