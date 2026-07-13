<script setup lang="ts">
import type { ConnectorLayout } from '../geometry/connectorLayout';

defineProps<{ layout: ConnectorLayout; compatible?: boolean; active?: boolean }>();

// SVG groups are not native controls. The template supplies button semantics and
// keyboard activation, and stops pointer-down from starting the node's drag.
const emit = defineEmits<{ activate: []; preview: [] }>();
</script>

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
    @pointerdown.stop
    @focus="emit('preview')"
    @keydown.enter.prevent.stop="emit('activate')"
    @keydown.space.prevent.stop="emit('activate')"
  >
    <circle r="6" />
    <title>
      {{ layout.connector.label }} — {{ layout.connector.direction }}
      {{ layout.connector.dataType }}
    </title>
  </g>
</template>

<style scoped>
.flow-connector circle {
  fill: #f8fbfd;
  stroke: #102133;
  stroke-width: 2;
}

.flow-connector {
  cursor: crosshair;
  outline: none;
}

.flow-connector:focus circle,
.flow-connector.compatible circle {
  fill: #dff6ee;
  stroke: #087f6f;
  stroke-width: 4;
}

.flow-connector.active circle {
  fill: #102133;
  stroke: #65d6ad;
  stroke-width: 4;
}
</style>
