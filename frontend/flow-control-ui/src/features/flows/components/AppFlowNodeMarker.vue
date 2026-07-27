<template>
  <g
    v-bind="automation()"
    class="node-marker"
    :class="color"
    :transform="`translate(${x} -8)`"
    aria-hidden="true"
  >
    <circle v-if="shape === 'circle'" cx="7.5" cy="7.5" r="7.5" />
    <path v-else-if="shape === 'triangle'" d="M 0 14 L 15 14 L 7.5 2 Z" />
    <rect v-else x="1" y="1" width="13" height="13" rx="2" />
  </g>
</template>

<script setup lang="ts">
export type NodeMarkerShape = 'circle' | 'square' | 'triangle';
export type NodeMarkerColor = 'blue' | 'green' | 'orange';
import { useAutomation } from '@/composables/useAutomation';

const props = defineProps<{
  automation: string;
  shape: NodeMarkerShape;
  color: NodeMarkerColor;
  x: number;
}>();
const automation = useAutomation(props.automation);
</script>

<style scoped>
.node-marker {
  pointer-events: none;
  stroke-width: var(--stroke-width-fine);
  stroke-linejoin: round;
}

.blue {
  fill: var(--color-marker-blue-fill);
  stroke: var(--color-marker-blue-stroke);
}

.green {
  fill: var(--color-marker-green-fill);
  stroke: var(--color-marker-green-stroke);
}

.orange {
  fill: var(--color-marker-orange-fill);
  stroke: var(--color-marker-orange-stroke);
}
</style>
