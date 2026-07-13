<script setup lang="ts">
import { computed } from 'vue';

import type { FlowNode } from '../types';

const props = defineProps<{
  node: FlowNode;
  selected: boolean;
}>();

const emit = defineEmits<{
  select: [nodeId: string];
}>();

const transform = computed(() => `translate(${props.node.x} ${props.node.y})`);
</script>

<template>
  <g
    class="flow-node"
    :class="{ selected }"
    :transform="transform"
    role="button"
    tabindex="0"
    :aria-label="`${node.label} node`"
    :aria-pressed="selected"
    @click="emit('select', node.id)"
    @keydown.enter.prevent="emit('select', node.id)"
    @keydown.space.prevent="emit('select', node.id)"
  >
    <rect class="node-body" width="210" height="64" rx="12" :fill="node.color" />
    <rect class="node-icon-panel" width="58" height="64" rx="12" />
    <path class="node-icon-panel-end" d="M46 0h12v64H46z" />
    <text class="node-icon" x="29" y="39" text-anchor="middle">
      {{ node.kind.slice(0, 1).toUpperCase() }}
    </text>
    <text class="node-label" x="74" y="29">{{ node.label }}</text>
    <text class="node-kind" x="74" y="47">{{ node.kind }}</text>
    <circle class="connector" cx="0" cy="32" r="6" />
    <circle class="connector" cx="210" cy="32" r="6" />
  </g>
</template>

<style scoped>
.flow-node {
  cursor: pointer;
  outline: none;
  filter: drop-shadow(0 6px 10px rgb(16 33 51 / 14%));
}

.node-body {
  stroke: rgb(16 33 51 / 50%);
  stroke-width: 1.5;
}

.node-icon-panel,
.node-icon-panel-end {
  fill: rgb(16 33 51 / 16%);
  pointer-events: none;
}

.node-icon {
  fill: #102133;
  font-size: 18px;
  font-weight: 800;
  pointer-events: none;
}

.node-label,
.node-kind {
  fill: #102133;
  pointer-events: none;
}

.node-label {
  font-size: 13px;
  font-weight: 750;
}

.node-kind {
  font-size: 10px;
  letter-spacing: 0.08em;
  text-transform: uppercase;
  opacity: 0.7;
}

.connector {
  fill: #f8fbfd;
  stroke: #102133;
  stroke-width: 2;
}

.flow-node:hover .node-body,
.flow-node:focus .node-body,
.flow-node.selected .node-body {
  stroke: #0b6e63;
  stroke-width: 4;
}
</style>
