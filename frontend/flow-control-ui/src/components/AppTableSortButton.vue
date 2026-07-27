<template>
  <AppButton
    v-bind="automation('toggle')"
    :text="label"
    :aria-label="accessibleLabel"
    :icon="direction === 'ascending' ? sortAscendingIcon : sortDescendingIcon"
    @click="$emit(EVENTS.TOGGLE)"
  />
</template>

<script setup lang="ts">
import { computed } from 'vue';

import sortAscendingIcon from '@/assets/icons/sort-ascending-icon.svg';
import sortDescendingIcon from '@/assets/icons/sort-descending-icon.svg';
import AppButton from '@/components/AppButton.vue';
import { useAutomation } from '@/composables/useAutomation';
import type { SortDirection } from '@/composables/usePaginatedCollection';
import { EVENTS } from '@/constants/events';

const props = defineProps<{
  automation: string;
  label: string;
  direction: SortDirection;
}>();

defineEmits<{
  (event: typeof EVENTS.TOGGLE): void;
}>();

const automation = useAutomation(props.automation);
const accessibleLabel = computed(
  () =>
    `${props.label}, sorted ${props.direction}. Activate to sort ${
      props.direction === 'ascending' ? 'descending' : 'ascending'
    }.`
);
</script>
