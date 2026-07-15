<template>
  <g
    v-if="path"
    class="connection-group"
    :class="{ selected, preview }"
    :data-connection-id="id"
    :role="preview ? undefined : 'button'"
    :tabindex="preview ? undefined : 0"
    :aria-label="preview ? undefined : label || `Connection ${id}`"
    @click.stop="!preview && emit('select', id)"
    @keydown.enter.prevent="!preview && emit('select', id)"
    @keydown.space.prevent="!preview && emit('select', id)"
  >
    <path v-if="!preview" class="connection-hit-area" :d="path" />
    <path class="flow-connection" :d="path" />
  </g>
</template>

<script setup lang="ts">
import { computed } from 'vue';

import { connectionPath } from '../geometry/connectionPath';
import type { Point } from '../geometry/connectorLayout';
import type { ConnectorSide } from '../types';

const props = defineProps<{
  id: string;
  start?: Point;
  end?: Point;
  startSide?: ConnectorSide;
  endSide?: ConnectorSide;
  selected?: boolean;
  preview?: boolean;
  label?: string;
}>();
const emit = defineEmits<{ select: [id: string] }>();
// The same calculated curve drives both the visible stroke and its larger hit
// target, ensuring selection follows exactly what the user sees.
const path = computed(() =>
  connectionPath(props.start, props.end, props.startSide, props.endSide)
);
</script>

<style scoped>
.flow-connection {
  fill: none;
  stroke: #4f7b96;
  stroke-width: 3;
}

.connection-group {
  cursor: pointer;
  outline: none;
}

.connection-hit-area {
  /* A three-pixel SVG stroke is difficult to click or tap. This transparent
     stroke enlarges its pointer target without changing the rendered line. */
  fill: none;
  stroke: transparent;
  stroke-width: 16;
}

.connection-group:hover .flow-connection,
.connection-group:focus .flow-connection,
.connection-group.selected .flow-connection {
  stroke: #0b7568;
  stroke-width: 5;
}

.connection-group.preview {
  /* A preview is feedback for an unfinished gesture, not a selectable graph
     object, so pointer events pass through it to the canvas and connectors. */
  pointer-events: none;
}

.connection-group.preview .flow-connection {
  stroke: #0b7568;
  stroke-dasharray: 8 6;
  opacity: 0.8;
}
</style>
