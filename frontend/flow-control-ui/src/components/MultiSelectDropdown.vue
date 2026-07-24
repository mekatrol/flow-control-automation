<template>
  <div ref="root" class="multi-select">
    <span class="multi-select-label">{{ label }}</span>
    <AppButton
      :text="summary"
      :icon="chevronDownIcon"
      aria-haspopup="true"
      :aria-expanded="open"
      :aria-label="`${label}: ${summary}`"
      @click="open = !open"
    />
    <div v-if="open" class="multi-select-menu">
      <label class="select-all">
        <input type="checkbox" :checked="allSelected" @change="selectAll" />
        {{ allLabel }}
      </label>
      <div class="option-separator" aria-hidden="true"></div>
      <label v-for="option in options" :key="option.value">
        <input
          type="checkbox"
          :value="option.value"
          :checked="modelValue.includes(option.value)"
          :disabled="modelValue.length === 1 && modelValue.includes(option.value)"
          @change="toggleOption(option.value)"
        />
        {{ option.label }}
      </label>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref } from 'vue';

import chevronDownIcon from '@/assets/chevron-down-icon.svg';
import AppButton from '@/components/AppButton.vue';

export interface MultiSelectOption {
  label: string;
  value: string;
}

const props = withDefaults(
  defineProps<{
    label: string;
    allLabel?: string;
    modelValue: string[];
    options: MultiSelectOption[];
  }>(),
  {
    allLabel: 'All'
  }
);

const emit = defineEmits<{
  'update:modelValue': [values: string[]];
}>();

const root = ref<HTMLElement>();
const open = ref(false);
const allSelected = computed(
  () => props.options.length > 0 && props.options.every(({ value }) => props.modelValue.includes(value))
);
const summary = computed(() => {
  if (allSelected.value) return props.allLabel;
  return props.options
    .filter(({ value }) => props.modelValue.includes(value))
    .map(({ label }) => label)
    .join(', ');
});

const selectAll = (): void => {
  emit(
    'update:modelValue',
    props.options.map(({ value }) => value)
  );
};

const toggleOption = (value: string): void => {
  const selected = props.modelValue.includes(value)
    ? props.modelValue.filter((candidate) => candidate !== value)
    : [...props.modelValue, value];
  if (selected.length > 0) emit('update:modelValue', selected);
};

const closeFromOutside = (event: MouseEvent): void => {
  if (!root.value?.contains(event.target as Node)) open.value = false;
};
const closeFromEscape = (event: KeyboardEvent): void => {
  if (event.key === 'Escape') open.value = false;
};

onMounted(() => {
  document.addEventListener('click', closeFromOutside);
  document.addEventListener('keydown', closeFromEscape);
});
onBeforeUnmount(() => {
  document.removeEventListener('click', closeFromOutside);
  document.removeEventListener('keydown', closeFromEscape);
});
</script>

<style scoped>
.multi-select {
  position: relative;
  display: flex;
  gap: 8px;
  align-items: center;
}

.multi-select-label {
  color: var(--color-text-primary);
  font-size: 12px;
  font-weight: 700;
}

.multi-select :deep(button) {
  min-width: 170px;
  min-height: 44px;
  justify-content: space-between;
  flex-direction: row-reverse;
  padding: 9px 10px;
  color: var(--color-text-primary);
  background: var(--color-surface-raised);
  border: 1px solid var(--color-border-default);
  border-radius: 7px;
  cursor: pointer;
}

.multi-select :deep(.button-text) {
  flex: 1;
  text-align: left;
}

.multi-select-menu {
  position: absolute;
  z-index: 4;
  top: calc(100% + 6px);
  right: 0;
  display: grid;
  min-width: 220px;
  max-height: 320px;
  padding: 8px;
  overflow-y: auto;
  color: var(--color-text-primary);
  background: var(--color-surface-raised);
  border: 1px solid var(--color-border-default);
  border-radius: 8px;
  box-shadow: 0 10px 30px var(--color-shadow-dialog);
}

.multi-select-menu label {
  display: flex;
  gap: 8px;
  align-items: center;
  min-height: 44px;
  padding: 6px 8px;
  border-radius: 6px;
  cursor: pointer;
}

.multi-select-menu label:hover {
  background: var(--color-action-primary-surface);
}

.multi-select-menu input {
  width: 18px;
  height: 18px;
  accent-color: var(--color-action-primary);
}

.select-all {
  font-weight: 750;
}

.option-separator {
  margin: 2px 8px;
  border-top: 1px solid var(--color-border-subtle);
}

@media (max-width: 640px) {
  .multi-select {
    align-items: stretch;
    flex-direction: column;
  }

  .multi-select-menu {
    right: auto;
    left: 0;
  }
}
</style>
