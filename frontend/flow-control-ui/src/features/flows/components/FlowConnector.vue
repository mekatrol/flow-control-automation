<template>
  <g
    class="flow-connector"
    :class="{ compatible, active }"
    :transform="`translate(${layout.x} ${layout.y})`"
    role="button"
    tabindex="0"
    :aria-label="`${layout.connector.label}, ${layout.connector.direction}, ${layout.connector.dataType}${compatible ? ', compatible destination' : ''}`"
    :aria-pressed="active"
    @click.stop="emit('activate')"
    @pointerdown.left.stop="emit('press')"
    @pointerup.left.stop="emit('release')"
    @focus="emit('preview')"
    @keydown.enter.prevent.stop="emit('activate')"
    @keydown.space.prevent.stop="emit('activate')"
  >
    <!-- SVG only hit-tests painted geometry. This transparent circle gives the
    connector a forgiving pointer target without making the visible port huge. -->
    <circle class="connector-hit-target" r="14" />
    <rect class="connector-port" x="-5" y="-5" width="10" height="10" rx="2" />
    <title>
      {{ layout.connector.label }} — {{ layout.connector.direction }}
      {{ layout.connector.dataType }}
    </title>
  </g>
</template>

<script setup lang="ts">
import type { ConnectorLayout } from '@/features/flows/geometry/connectorLayout';

defineProps<{ layout: ConnectorLayout; compatible?: boolean; active?: boolean }>();

// SVG groups are not native controls. The template supplies button semantics and
// keyboard activation, and stops pointer-down from starting the node's drag.
const emit = defineEmits<{ press: []; activate: []; release: []; preview: [] }>();
</script>

<style scoped>
.connector-hit-target {
  fill: var(--color-transparent);
  stroke: none;
}

.connector-port {
  fill: var(--color-surface-subtle);
  stroke: var(--color-text-primary);
  stroke-width: 2;
}

.flow-connector {
  cursor: crosshair;
  outline: none;
}

.flow-connector:focus .connector-port,
.flow-connector.compatible .connector-port {
  fill: var(--color-action-primary-surface);
  stroke: var(--color-action-primary-indicator);
  stroke-width: 4;
}

.flow-connector.active .connector-port {
  fill: var(--color-text-primary);
  stroke: var(--color-brand-accent);
  stroke-width: 4;
}
</style>
