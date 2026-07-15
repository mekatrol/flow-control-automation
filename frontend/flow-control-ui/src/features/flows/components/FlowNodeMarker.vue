<template>
  <g class="node-marker" :class="color" :transform="`translate(${x} -8)`" aria-hidden="true">
    <circle v-if="shape === 'circle'" cx="7.5" cy="7.5" r="7.5" />
    <path v-else-if="shape === 'triangle'" d="M 0 14 L 15 14 L 7.5 2 Z" />
    <rect v-else x="1" y="1" width="13" height="13" rx="2" />
  </g>
</template>

<script setup lang="ts">
export type NodeMarkerShape = 'circle' | 'square' | 'triangle';
export type NodeMarkerColor = 'blue' | 'green' | 'orange';

defineProps<{
  shape: NodeMarkerShape;
  color: NodeMarkerColor;
  x: number;
}>();
</script>

<style scoped>
.node-marker {
  pointer-events: none;
  stroke-width: 1;
  stroke-linejoin: round;
}

.blue {
  fill: #53a3f3;
  stroke: #4587c9;
}

.green {
  fill: #6df168;
  stroke: #57bd53;
}

.orange {
  fill: #f60;
  stroke: #bd4b00;
}
</style>
