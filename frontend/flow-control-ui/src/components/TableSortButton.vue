<template>
  <AppButton
    class="sort-button"
    :text="label"
    :aria-label="accessibleLabel"
    :icon="direction === 'ascending' ? sortAscendingIcon : sortDescendingIcon"
    @click="$emit('toggle')"
  />
</template>

<script setup lang="ts">
import { computed } from 'vue';

import sortAscendingIcon from '@/assets/sort-ascending-icon.svg';
import sortDescendingIcon from '@/assets/sort-descending-icon.svg';
import AppButton from '@/components/AppButton.vue';
import type { SortDirection } from '@/composables/usePaginatedCollection';

const props = defineProps<{
  label: string;
  direction: SortDirection;
}>();

defineEmits<{
  toggle: [];
}>();

const accessibleLabel = computed(
  () =>
    `${props.label}, sorted ${props.direction}. Activate to sort ${
      props.direction === 'ascending' ? 'descending' : 'ascending'
    }.`
);
</script>

<style scoped>
.sort-button {
  display: inline-flex;
  gap: 7px;
  align-items: center;
  min-width: 44px;
  min-height: 44px;
  padding: 6px 8px;
  color: inherit;
  font: inherit;
  font-weight: 750;
  background: transparent;
  border: 0;
  border-radius: 6px;
  cursor: pointer;
}

.sort-button:hover {
  color: var(--color-action-primary-strong);
  background: var(--color-action-primary-surface);
}
</style>
