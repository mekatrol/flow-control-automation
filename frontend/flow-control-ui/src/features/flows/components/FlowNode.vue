<template>
  <g
    class="flow-node"
    :data-node-id="node.id"
    :data-node-category="definition.category"
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
      rx="2"
    />
    <FlowNodeIcon :icon="definition.icon" />
    <FlowNodeLabel :label="node.label" :kind-label="definition.label" />
    <FlowNodeStatus
      v-if="status"
      :status="status"
      :value="statusValue"
      :width="definition.defaultSize.width"
    />
    <!-- These legacy function indicators intentionally overlap the top edge.
    Their shape as well as colour communicates state, so they remain distinct
    for people who cannot distinguish the colours. -->
    <FlowNodeMarker shape="square" color="orange" :x="definition.defaultSize.width - 60" />
    <FlowNodeMarker shape="triangle" color="green" :x="definition.defaultSize.width - 40" />
    <FlowNodeMarker shape="circle" color="blue" :x="definition.defaultSize.width - 20" />
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

<script setup lang="ts">
import { computed } from 'vue';

import FlowNodeIcon from './FlowNodeIcon.vue';
import FlowNodeLabel from './FlowNodeLabel.vue';
import FlowNodeMarker from './FlowNodeMarker.vue';
import FlowNodeStatus from './FlowNodeStatus.vue';
import FlowConnector from './FlowConnector.vue';
import { layoutConnectors } from '@/features/flows/geometry/connectorLayout';
import { getNodeKind } from '@/features/flows/nodeKinds';
import type { FlowConnectionEndpoint, FlowNode } from '@/features/flows/types';

const props = defineProps<{
  node: FlowNode;
  selected: boolean;
  status?: 'draft' | 'deployed' | 'idle' | 'running' | 'stopped' | 'error';
  statusValue?: string;
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

<style scoped>
.flow-node {
  cursor: pointer;
  outline: none;
}

.node-body {
  stroke: var(--color-control-neutral);
  stroke-width: 1;
}

.flow-node[data-node-category='logic'] .node-body {
  fill: var(--color-node-logic);
}

.flow-node[data-node-category='maths'] .node-body {
  fill: var(--color-node-maths);
}

.flow-node[data-node-category='override'] .node-body {
  fill: var(--color-node-override);
}

.flow-node[data-node-category='routing'] .node-body {
  fill: var(--color-node-routing);
}

.flow-node[data-node-category='timing'] .node-body {
  fill: var(--color-node-timing);
}

.flow-node:hover .node-body,
.flow-node:focus .node-body,
.flow-node.selected .node-body {
  stroke: var(--color-action-primary-text);
  stroke-width: 4;
}
</style>
