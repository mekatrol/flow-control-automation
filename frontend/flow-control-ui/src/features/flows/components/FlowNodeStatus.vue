<script setup lang="ts">
defineProps<{
  status: 'draft' | 'deployed' | 'idle' | 'running' | 'stopped' | 'error';
  value?: string;
  width: number;
}>();
</script>

<template>
  <g
    class="node-status"
    transform="translate(0 49)"
    :aria-label="value ? `${status}: ${value}` : status"
  >
    <rect class="status-background" y="-1" :width="width" height="14" rx="2" />
    <rect class="status-indicator" x="3" width="9" height="9" rx="2" />
    <text x="17" y="9">{{ value || status }}</text>
    <title>{{ value ? `${status}: ${value}` : status }}</title>
  </g>
</template>

<style scoped>
.status-background {
  fill: #fcfcfc;
  fill-opacity: 0.9;
}

.status-indicator {
  fill: #687784;
  stroke: #4f5c66;
  stroke-width: 1;
}

.node-status[aria-label^='deployed'] .status-indicator,
.node-status[aria-label^='running'] .status-indicator {
  fill: #087f6f;
}

.node-status[aria-label^='error'] .status-indicator {
  fill: #b43c28;
}

.node-status text {
  fill: #222;
  font-size: 11px;
  text-transform: uppercase;
}
</style>
