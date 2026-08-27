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

import chevronDownIcon from '@/assets/icons/chevron-down-icon.svg';
import AppButton from '@/components/AppButton.vue';
import { EVENTS } from '@/constants/events';

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
  (event: typeof EVENTS.UPDATE_MODEL_VALUE, values: string[]): void;
}>();

const root = ref<HTMLElement>();
const open = ref(false);
const allSelected = computed(
  () =>
    props.options.length > 0 && props.options.every(({ value }) => props.modelValue.includes(value))
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
    EVENTS.UPDATE_MODEL_VALUE,
    props.options.map(({ value }) => value)
  );
};

const toggleOption = (value: string): void => {
  const selected = props.modelValue.includes(value)
    ? props.modelValue.filter((candidate) => candidate !== value)
    : [...props.modelValue, value];
  if (selected.length > 0) emit(EVENTS.UPDATE_MODEL_VALUE, selected);
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
  gap: var(--space-3-5);
  align-items: center;
}

.multi-select-label {
  color: var(--color-text-primary);
  font-size: var(--font-size-md);
  font-weight: var(--font-weight-bold);
}

.multi-select-menu {
  position: absolute;
  z-index: 4;
  top: calc(100% + 6px);
  right: 0;
  display: grid;
  min-width: 220px;
  max-height: 320px;
  padding: var(--space-3-5);
  overflow-y: auto;
  color: var(--color-text-primary);
  background: var(--color-surface-raised);
  border: var(--border-width-default) solid var(--color-border-default);
  border-radius: var(--radius-lg);
  box-shadow: var(--shadow-menu);
}

.multi-select-menu label {
  display: flex;
  gap: var(--space-3-5);
  align-items: center;
  min-height: 44px;
  padding: var(--space-2-5) var(--space-3-5);
  border-radius: var(--radius-sm);
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
  font-weight: var(--font-weight-strong);
}

.option-separator {
  margin: var(--space-0-5) var(--space-3-5);
  border-top: var(--border-width-default) solid var(--color-border-subtle);
}

/* Mobile breakpoint (40rem): stacks page and navigation content for phone layouts. */
@media (max-width: 40rem) {
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
