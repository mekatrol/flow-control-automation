<template>
  <div v-bind="automation()" class="debug-target">
    <label :for="selectId">Debug target</label>
    <select
      :id="selectId"
      :value="modelValue"
      :disabled="loading || targets.length === 0"
      @change="selectTarget"
    >
      <option v-for="target in targets" :key="target.id" :value="target.id">
        {{ target.kind === 'controller' ? `Controller — ${target.label}` : target.label }}
      </option>
    </select>
    <small v-if="loading" role="status">Loading controller targets…</small>
    <small v-else-if="error" class="error" role="alert">{{ error }}</small>
    <small v-else-if="selected?.kind === 'controller'">
      Template revision {{ selected.controllerTemplateRevision }} · Shadow mode
    </small>
    <small v-else-if="selected?.kind === 'emulator'">Controller limits with simulated I/O</small>
    <small v-else>Built-in server VM</small>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue';

import { useAutomation } from '@/composables/useAutomation';
import type { FlowDebugTarget } from '@/features/flows/debugTargets';

const props = defineProps<{
  automation: string;
  modelValue: string;
  targets: FlowDebugTarget[];
  loading?: boolean;
  error?: string;
}>();
const emit = defineEmits<{
  (event: 'update:modelValue', value: string): void;
}>();

const automation = useAutomation(props.automation);
const selectId = computed(() => `${props.automation}-select`);
const selected = computed(() => props.targets.find((target) => target.id === props.modelValue));

const selectTarget = (event: Event): void => {
  const value = (event.target as HTMLSelectElement).value;
  if (props.targets.some((target) => target.id === value)) emit('update:modelValue', value);
};
</script>

<style scoped>
.debug-target {
  display: grid;
  gap: var(--space-1-5);
  min-width: 220px;
}

label {
  color: var(--color-text-secondary);
  font-size: var(--font-size-xs);
  font-weight: var(--font-weight-bold);
}

select {
  min-height: 34px;
  padding: var(--space-2) var(--space-3);
  color: var(--color-text-primary);
  background: var(--color-surface-raised);
  border: var(--border-width-default) solid var(--color-border-default);
  border-radius: var(--radius-sm);
}

small {
  color: var(--color-text-subtle);
  font-size: var(--font-size-xs);
}

.error {
  color: var(--color-danger-strong);
}
</style>
