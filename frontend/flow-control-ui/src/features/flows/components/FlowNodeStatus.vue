<script setup lang="ts">
defineProps<{
  status: 'draft' | 'deployed' | 'idle' | 'running' | 'stopped' | 'error';
  value?: string;
}>();
</script>

<template>
  <g class="node-status" :aria-label="value ? `${status}: ${value}` : status">
    <circle cx="198" cy="12" r="5" />
    <title>{{ value ? `${status}: ${value}` : status }}</title>
  </g>
</template>

<style scoped>
.node-status circle {
  fill: #687784;
  stroke: #fff;
  stroke-width: 2;
}

.node-status[aria-label^='deployed'] circle,
.node-status[aria-label^='running'] circle {
  fill: #087f6f;
}

.node-status[aria-label^='error'] circle {
  fill: #b43c28;
}
</style>
