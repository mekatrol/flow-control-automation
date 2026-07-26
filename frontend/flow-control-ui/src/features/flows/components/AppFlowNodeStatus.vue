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

<script setup lang="ts">
defineProps<{
  status: 'draft' | 'deployed' | 'idle' | 'running' | 'stopped' | 'error';
  value?: string;
  width: number;
}>();
</script>

<style scoped>
.status-background {
  fill: var(--color-node-status-surface);
  fill-opacity: 0.9;
}

.status-indicator {
  fill: var(--color-node-status-fill);
  stroke: var(--color-node-status-stroke);
  stroke-width: 1;
}

.node-status[aria-label^='deployed'] .status-indicator,
.node-status[aria-label^='running'] .status-indicator {
  fill: var(--color-action-primary-indicator);
}

.node-status[aria-label^='error'] .status-indicator {
  fill: var(--color-danger-border);
}

.node-status text {
  fill: var(--color-node-status-stopped);
  font-size: 11px;
  text-transform: uppercase;
}
</style>
