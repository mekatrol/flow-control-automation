<template>
  <form v-bind="automation()" class="list-filter" role="search" @submit.prevent="applyFilter">
    <label :for="inputId">{{ label }}</label>
    <div class="list-filter__controls">
      <AppClearableInput
        v-bind="automation('input')"
        :id="inputId"
        v-model="filterValue"
        type="search"
        :placeholder="placeholder"
        autocomplete="off"
        @clear="clearFilter"
      />
      <AppButton text="Apply" type="submit" :icon="filterIcon" v-bind="automation('submit')" />
    </div>
  </form>
</template>

<script setup lang="ts">
import { computed } from 'vue';
import { useAutomation } from '@/composables/useAutomation';

import { ListFilterEmit } from '@/models/listViewEmits';
import filterIcon from '@/assets/icons/filter-icon.svg';
import AppButton from '@/components/AppButton.vue';
import AppClearableInput from '@/components/AppClearableInput.vue';

interface Props {
  modelValue: string;
  inputId?: string;
  label?: string;
  placeholder?: string;
  automation: string;
  active: boolean;
  disabled?: boolean;
}

const props = withDefaults(defineProps<Props>(), {
  inputId: 'list-filter',
  label: 'Filter list',
  placeholder: 'Enter filter text',
  disabled: false
});

type Emits = {
  'update:modelValue': [value: string];
  apply: [value: string];
  clear: [];
};

const emit = defineEmits<Emits>();

const automation = useAutomation(props.automation);

const filterValue = computed({
  get: () => props.modelValue,
  set: (value: string) => emit(ListFilterEmit.UpdateModelValue, value)
});

const applyFilter = (): void => {
  emit(ListFilterEmit.Apply, filterValue.value.trim());
};

const clearFilter = (): void => {
  emit(ListFilterEmit.UpdateModelValue, '');
  emit(ListFilterEmit.Clear);
};
</script>

<style scoped>
.list-filter {
  display: flex;
  gap: 0.35rem;
}

.list-filter__controls {
  display: flex;
  flex-wrap: wrap;
  gap: 0.5rem;
}

input {
  min-width: min(100%, 18rem);
  flex: 1;
}

input,
button {
  min-height: 2.75rem;
  border: 1px solid var(--color-border-default);
  border-radius: 0.35rem;
  background: var(--color-surface-subtle);
  color: var(--color-text-primary);
  font: inherit;
}

input {
  padding-inline: 0.75rem;
}

button {
  padding-inline: 0.9rem;
  cursor: pointer;
}

button:hover:not(:disabled) {
  border-color: var(--color-border-default);
}

button:disabled {
  cursor: not-allowed;
  opacity: 0.55;
}

input:focus-visible,
button:focus-visible {
  outline: 2px solid var(--color-action-primary);
  outline-offset: 2px;
  box-shadow: var(--shadow-focus);
}
</style>
