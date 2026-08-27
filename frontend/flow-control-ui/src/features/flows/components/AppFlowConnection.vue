<template>
  <g
    v-if="path"
    class="connection-group"
    :class="{ selected, preview }"
    :data-connection-id="id"
    :role="preview ? undefined : 'button'"
    :tabindex="preview ? undefined : 0"
    :aria-label="preview ? undefined : label || `Connection ${id}`"
    @click.stop="!preview && emit(EVENTS.SELECT, id)"
    @keydown.enter.prevent="!preview && emit(EVENTS.SELECT, id)"
    @keydown.space.prevent="!preview && emit(EVENTS.SELECT, id)"
  >
    <path v-if="!preview" class="connection-hit-area" :d="path" />
    <path class="flow-connection" :d="path" />
  </g>
</template>

<script setup lang="ts">
import { computed } from 'vue';

import { connectionPath } from '@/features/flows/geometry/connectionPath';
import { EVENTS } from '@/constants/events';
import type { Point } from '@/features/flows/geometry/connectorLayout';
import type { ConnectorSide } from '@/features/flows/types';

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
const emit = defineEmits<{
  (event: typeof EVENTS.SELECT, id: string): void;
}>();
// The same calculated curve drives both the visible stroke and its larger hit
// target, ensuring selection follows exactly what the user sees.
const path = computed(() => connectionPath(props.start, props.end, props.startSide, props.endSide));
</script>

<style scoped>
.flow-connection {
  fill: none;
  stroke: var(--color-connection-default);
  stroke-width: var(--stroke-width-strong);
}

.connection-group {
  cursor: pointer;
  outline: none;
}

.connection-hit-area {
  /* A three-pixel SVG stroke is difficult to click or tap. This transparent
     stroke enlarges its pointer target without changing the rendered line. */
  fill: none;
  stroke: var(--color-transparent);
  stroke-width: var(--stroke-width-hit-target);
}

.connection-group:hover .flow-connection,
.connection-group:focus .flow-connection,
.connection-group.selected .flow-connection {
  stroke: var(--color-action-primary);
  stroke-width: var(--stroke-width-extra-heavy);
}

.connection-group.preview {
  /* A preview is feedback for an unfinished gesture, not a selectable graph
     object, so pointer events pass through it to the canvas and connectors. */
  pointer-events: none;
}

.connection-group.preview .flow-connection {
  stroke: var(--color-action-primary);
  stroke-dasharray: 8 6;
  opacity: 0.8;
}
</style>
