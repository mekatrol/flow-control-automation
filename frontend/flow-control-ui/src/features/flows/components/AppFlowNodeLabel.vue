<template>
  <text v-bind="automation()" class="node-label" x="46" y="25">{{ kindLabel || label }}</text>
</template>

<script setup lang="ts">
import { useAutomation } from '@/composables/useAutomation';

const props = defineProps<{ automation: string; label: string; kindLabel: string }>();
const automation = useAutomation(props.automation);
</script>

<style scoped>
.node-label {
  fill: var(--color-text-primary);
  pointer-events: none;
  font-size: var(--font-size-base);
  font-weight: var(--font-weight-medium);
  text-transform: uppercase;
  user-select: none;
}
</style>
