<script setup lang="ts">
import { computed } from 'vue';

import FlowNodeIcon from './FlowNodeIcon.vue';
import FlowNodeLabel from './FlowNodeLabel.vue';
import FlowNodeStatus from './FlowNodeStatus.vue';
import FlowConnector from './FlowConnector.vue';
import { layoutConnectors } from '../geometry/connectorLayout';
import { getNodeKind } from '../nodeKinds';
import type { FlowConnectionEndpoint, FlowNode } from '../types';

const props = defineProps<{
  node: FlowNode;
  selected: boolean;
  status?: 'draft' | 'deployed' | 'idle' | 'running' | 'stopped' | 'error';
  statusValue?: string;
  marker?: string;
  connectionStart?: FlowConnectionEndpoint;
  compatibleConnectorKeys?: string[];
}>();

const emit = defineEmits<{
  select: [nodeId: string];
  dragstart: [nodeId: string, event: PointerEvent];
  connectorpress: [endpoint: FlowConnectionEndpoint];
  connectoractivate: [endpoint: FlowConnectionEndpoint];
  connectorrelease: [endpoint: FlowConnectionEndpoint];
  connectorpreview: [endpoint: FlowConnectionEndpoint];
}>();

// A node is positioned by translating one SVG group. Its body, label, status,
// and connectors can then use stable coordinates local to that group. Because an
// SVG group has no native control semantics, the template also supplies focus,
// button behaviour, and an announced selected state.
const transform = computed(() => `translate(${props.node.x} ${props.node.y})`);
const definition = computed(() => getNodeKind(props.node.kind));
const connectorLayouts = computed(() =>
  // Connector coordinates come from the kind's declared size rather than the
  // browser's measured pixels, so persisted paths remain deterministic at zoom.
  layoutConnectors(
    props.node.connectors,
    definition.value.defaultSize.width,
    definition.value.defaultSize.height
  )
);
const connectorKey = (connectorId: string): string => `${props.node.id}:${connectorId}`;
</script>

<template>
  <g
    class="flow-node"
    :data-node-id="node.id"
    :class="{ selected }"
    :transform="transform"
    role="button"
    tabindex="0"
    :aria-label="`${node.label}, ${definition.label} node${status ? `, ${status}` : ''}${statusValue ? `, ${statusValue}` : ''}`"
    :aria-pressed="selected"
    @click="emit('select', node.id)"
    @pointerdown.stop="emit('dragstart', node.id, $event)"
    @keydown.enter.prevent="emit('select', node.id)"
    @keydown.space.prevent="emit('select', node.id)"
  >
    <rect
      class="node-body"
      :width="definition.defaultSize.width"
      :height="definition.defaultSize.height"
      rx="12"
      :fill="node.color || definition.color"
    />
    <rect class="node-icon-panel" width="58" :height="definition.defaultSize.height" rx="12" />
    <path class="node-icon-panel-end" :d="`M46 0h12v${definition.defaultSize.height}H46z`" />
    <FlowNodeIcon :icon="definition.icon" />
    <FlowNodeLabel :label="node.label" :kind-label="definition.label" />
    <FlowNodeStatus v-if="status" :status="status" :value="statusValue" />
    <text v-if="marker" class="node-marker" x="198" y="57" text-anchor="end">{{ marker }}</text>
    <FlowConnector
      v-for="layout in connectorLayouts"
      :key="layout.connector.id"
      :layout="layout"
      :compatible="compatibleConnectorKeys?.includes(connectorKey(layout.connector.id))"
      :active="
        connectionStart?.nodeId === node.id && connectionStart.connectorId === layout.connector.id
      "
      @press="emit('connectorpress', { nodeId: node.id, connectorId: layout.connector.id })"
      @activate="emit('connectoractivate', { nodeId: node.id, connectorId: layout.connector.id })"
      @release="emit('connectorrelease', { nodeId: node.id, connectorId: layout.connector.id })"
      @preview="emit('connectorpreview', { nodeId: node.id, connectorId: layout.connector.id })"
    />
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

.node-marker {
  fill: #102133;
  font-size: 9px;
  font-weight: 800;
  pointer-events: none;
}

.flow-node:hover .node-body,
.flow-node:focus .node-body,
.flow-node.selected .node-body {
  stroke: #0b6e63;
  stroke-width: 4;
}
</style>
