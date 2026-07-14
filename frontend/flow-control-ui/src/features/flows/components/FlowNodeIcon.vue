<script setup lang="ts">
import { getNodeIconUrl } from '../nodeKinds';

defineProps<{ icon: string }>();
</script>

<template>
  <g class="node-icon" aria-hidden="true">
    <path class="node-icon-shade" d="M2 0h38v40H2a2 2 0 0 1-2-2V2a2 2 0 0 1 2-2Z" />
    <image :href="getNodeIconUrl(icon)" x="8" y="8" width="24" height="24" />
    <path class="node-icon-separator" d="M39.5.5v39" />
  </g>
</template>

<style scoped>
.node-icon {
  pointer-events: none;
}

.node-icon-shade {
  fill: #000;
  fill-opacity: 0.3;
}

.node-icon-separator {
  fill: none;
  stroke: #444;
  stroke-opacity: 0.4;
}
</style>
