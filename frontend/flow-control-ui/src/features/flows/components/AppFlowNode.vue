<template>
  <g
    class="flow-node"
    :data-node-id="node.id"
    :data-node-category="definition.category"
    :class="{ selected, current, breakpoint: breakpointPositions?.length }"
    role="group"
    :aria-label="nodeAriaLabel"
  >
    <g
      class="node-selector"
      :data-node-category="definition.category"
      :transform="transform"
      role="button"
      tabindex="0"
      :aria-label="nodeAriaLabel"
      :aria-pressed="selected"
      @click="emit(EVENTS.SELECT, node.id)"
      @pointerdown.stop="emit(EVENTS.DRAG_START, node.id, $event)"
      @keydown.enter.prevent="emit(EVENTS.SELECT, node.id)"
      @keydown.space.prevent="emit(EVENTS.SELECT, node.id)"
    >
      <rect
        class="node-body"
        :width="definition.defaultSize.width"
        :height="definition.defaultSize.height"
        rx="2"
      />
      <AppFlowNodeIcon :icon="definition.icon" />
      <AppFlowNodeLabel :label="node.label" :kind-label="definition.label" />
      <AppFlowNodeStatus
        v-if="status"
        :status="status"
        :value="statusValue"
        :width="definition.defaultSize.width"
      />
      <AppFlowNodeMarker shape="square" color="orange" :x="definition.defaultSize.width - 60" />
      <AppFlowNodeMarker shape="triangle" color="green" :x="definition.defaultSize.width - 40" />
      <AppFlowNodeMarker shape="circle" color="blue" :x="definition.defaultSize.width - 20" />
      <text v-if="breakpointPositions?.includes('before')" class="breakpoint-marker" x="6" y="14">
        B
      </text>
      <text
        v-if="breakpointPositions?.includes('after')"
        class="breakpoint-marker"
        :x="definition.defaultSize.width - 14"
        y="14"
      >
        A
      </text>
    </g>
    <g :transform="transform">
      <AppFlowConnector
        v-for="layout in connectorLayouts"
        :key="layout.connector.id"
        :layout="layout"
        :compatible="compatibleConnectorKeys?.includes(connectorKey(layout.connector.id))"
        :active="
          connectionStart?.nodeId === node.id && connectionStart.connectorId === layout.connector.id
        "
        @[EVENTS.PRESS]="
          emit(EVENTS.CONNECTOR_PRESS, { nodeId: node.id, connectorId: layout.connector.id })
        "
        @[EVENTS.ACTIVATE]="
          emit(EVENTS.CONNECTOR_ACTIVATE, { nodeId: node.id, connectorId: layout.connector.id })
        "
        @[EVENTS.RELEASE]="
          emit(EVENTS.CONNECTOR_RELEASE, { nodeId: node.id, connectorId: layout.connector.id })
        "
        @[EVENTS.PREVIEW]="
          emit(EVENTS.CONNECTOR_PREVIEW, { nodeId: node.id, connectorId: layout.connector.id })
        "
      />
      <g v-for="layout in connectorLayouts" :key="`value-${layout.connector.id}`">
        <text
          v-if="connectorValues?.[layout.connector.id]"
          class="connector-value"
          :x="layout.x + (layout.connector.side === 'left' ? 8 : -8)"
          :y="layout.y - 8"
          :text-anchor="layout.connector.side === 'left' ? 'start' : 'end'"
        >
          {{ connectorText(layout.connector.id) }}
        </text>
      </g>
    </g>
  </g>
</template>

<script setup lang="ts">
import { computed } from 'vue';

import AppFlowNodeIcon from './AppFlowNodeIcon.vue';
import AppFlowNodeLabel from './AppFlowNodeLabel.vue';
import AppFlowNodeMarker from './AppFlowNodeMarker.vue';
import AppFlowNodeStatus from './AppFlowNodeStatus.vue';
import AppFlowConnector from './AppFlowConnector.vue';
import { layoutConnectors } from '@/features/flows/geometry/connectorLayout';
import { EVENTS } from '@/constants/events';
import { getNodeKind } from '@/features/flows/nodeKinds';
import type { FlowConnectionEndpoint, FlowNode } from '@/features/flows/types';
import type { ConnectorRuntimeValue } from '@/features/flows/api/flowRuntimeApi';

const props = defineProps<{
  node: FlowNode;
  selected: boolean;
  status?: 'draft' | 'deployed' | 'idle' | 'running' | 'stopped' | 'error';
  statusValue?: string;
  connectionStart?: FlowConnectionEndpoint;
  compatibleConnectorKeys?: string[];
  current?: boolean;
  breakpointPositions?: ('before' | 'after')[];
  connectorValues?: Record<string, ConnectorRuntimeValue>;
}>();

const emit = defineEmits<{
  (event: typeof EVENTS.SELECT, nodeId: string): void;
  (event: typeof EVENTS.DRAG_START, nodeId: string, nativeEvent: PointerEvent): void;
  (event: typeof EVENTS.CONNECTOR_PRESS, endpoint: FlowConnectionEndpoint): void;
  (event: typeof EVENTS.CONNECTOR_ACTIVATE, endpoint: FlowConnectionEndpoint): void;
  (event: typeof EVENTS.CONNECTOR_RELEASE, endpoint: FlowConnectionEndpoint): void;
  (event: typeof EVENTS.CONNECTOR_PREVIEW, endpoint: FlowConnectionEndpoint): void;
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
const connectorText = (connectorId: string): string => {
  const value = props.connectorValues?.[connectorId];
  return value
    ? `${value.value}${value.units ? ` ${value.units}` : ''} · ${value.quality} · ${value.state}`
    : '';
};
const nodeAriaLabel = computed(() => {
  const values = props.node.connectors
    .map((connector) => connectorText(connector.id))
    .filter(Boolean)
    .join(', ');
  const breakpoints = props.breakpointPositions?.length
    ? `, breakpoints ${props.breakpointPositions.join(' and ')}`
    : '';
  const status = props.status ? `, ${props.status}` : '';
  const statusValue = props.statusValue ? `, ${props.statusValue}` : '';
  return `${props.node.label}, ${definition.value.label} node${status}${statusValue}${values ? `, ${values}` : ''}${breakpoints}`;
});
</script>

<style scoped>
.flow-node {
  cursor: pointer;
  outline: none;
}

.node-body {
  stroke: var(--color-control-neutral);
  stroke-width: var(--stroke-width-fine);
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
  stroke-width: var(--stroke-width-heavy);
}
.flow-node.current .node-body {
  stroke: var(--color-warning-text);
  stroke-width: var(--stroke-width-heavy);
}
.flow-node.breakpoint .node-body {
  stroke-dasharray: 6 3;
}
.connector-value {
  fill: var(--color-text-primary);
  font-size: var(--font-size-xs);
  paint-order: stroke;
  stroke: var(--color-surface-raised);
  stroke-width: var(--stroke-width-heavy);
}
.breakpoint-marker {
  fill: var(--color-warning-text);
  font-size: var(--font-size-sm);
  font-weight: var(--font-weight-bold);
}
</style>
