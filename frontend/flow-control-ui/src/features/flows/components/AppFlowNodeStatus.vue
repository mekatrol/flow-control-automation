<template>
  <g
    class="node-status"
    transform="translate(0 69)"
    :aria-label="value ? `${status}: ${value}` : status"
  >
    <rect class="status-background" y="-1" :width="width" height="14" rx="2" />
    <rect class="status-indicator" x="3" width="9" height="9" rx="2" />
    <text class="status-label" x="17" y="9">{{ status }}</text>
    <text v-if="value" class="status-value" :x="width - 5" y="9" text-anchor="end">
      {{ value }}
    </text>
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
  stroke-width: var(--stroke-width-fine);
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
  font-size: var(--font-size-sm);
  text-transform: uppercase;
}

.status-value {
  text-transform: none !important;
}
</style>
