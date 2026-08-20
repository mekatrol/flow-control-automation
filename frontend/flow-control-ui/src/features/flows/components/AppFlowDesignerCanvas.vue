<template>
  <div v-bind="automation()" class="canvas-frame">
    <div class="canvas-toolbar">
      <span>{{ flow.nodes.length }} nodes</span>
      <span>{{ flow.connections.length }} connections</span>
      <span v-if="selectedConnectionId" class="selection">
        Selected connection: {{ selectedConnectionId }}
      </span>
      <div class="zoom-controls" aria-label="Canvas zoom controls">
        <AppButton
          v-bind="automation('zoom-out')"
          text="Zoom out"
          hide-text
          :disabled="zoom <= 0.5"
          @click="setZoom(zoom - 0.25)"
        >
          <template #icon>
            <svg aria-hidden="true" viewBox="0 0 24 24">
              <path d="M5 12h14" />
            </svg>
          </template>
        </AppButton>
        <output aria-live="polite">{{ Math.round(zoom * 100) }}%</output>
        <AppButton
          v-bind="automation('zoom-in')"
          text="Zoom in"
          hide-text
          :disabled="zoom >= 2"
          @click="setZoom(zoom + 0.25)"
        >
          <template #icon>
            <svg aria-hidden="true" viewBox="0 0 24 24">
              <path d="M5 12h14M12 5v14" />
            </svg>
          </template>
        </AppButton>
      </div>
      <label class="grid-toggle">
        <input v-model="snapToGrid" type="checkbox" />
        Snap to grid
      </label>
      <AppFlowDesignerToolbar
        v-bind="automation('toolbar')"
        :selected-node-id="selectedNodeId"
        :can-move-front="canMoveFront"
        :can-move-back="canMoveBack"
        @[EVENTS.REORDER]="handleReorder"
      />
      <div
        v-if="debugging && selectedNodeId"
        class="debug-actions"
        role="group"
        aria-label="Selected node breakpoints"
      >
        <AppButton
          v-bind="automation('breakpoint-before')"
          text="Breakpoint before"
          @click="setSelectedBreakpoint('before')"
        />
        <AppButton
          v-bind="automation('breakpoint-after')"
          text="Breakpoint after"
          @click="setSelectedBreakpoint('after')"
        />
        <AppButton
          v-bind="automation('breakpoint-clear')"
          text="Clear breakpoints"
          @click="setSelectedBreakpoint(null)"
        />
        <AppButton
          v-bind="automation('run-to-node')"
          text="Run to selected node"
          @click="emit(EVENTS.RUN_TO_NODE, selectedNodeId)"
        />
      </div>
    </div>

    <div class="designer-workspace">
      <AppFlowNodePalette
        v-bind="automation('node-palette')"
        @[EVENTS.ADD]="handleAddNode"
        @[EVENTS.LEARN]="handleLearn"
      />
      <div class="canvas-column">
        <p v-if="connectionError" class="connection-error" role="alert">{{ connectionError }}</p>

        <div
          ref="viewportElement"
          class="canvas-viewport"
          tabindex="0"
          :aria-label="`Scrollable designer viewport, ${Math.round(viewportWidth)} pixels wide`"
          @keydown="handleCanvasKeydown"
        >
          <svg
            ref="canvasElement"
            class="designer-canvas"
            :viewBox="`0 0 ${viewBoxSize.width} ${viewBoxSize.height}`"
            :style="{ width: `${canvasSize.width}px`, height: `${canvasSize.height}px` }"
            role="group"
            :aria-label="`${flow.name} flow graph`"
            @click.self="clearCanvasState"
            @pointermove="handlePointerMove"
            @pointerup="handleDragEnd"
            @pointercancel="handleDragCancel"
            @dragover.prevent
            @drop="handlePaletteDrop"
          >
            <defs>
              <pattern id="designer-grid" width="24" height="24" patternUnits="userSpaceOnUse">
                <path
                  d="M24 0H0V24"
                  fill="none"
                  stroke="var(--color-border-subtle)"
                  stroke-width="1"
                />
              </pattern>
            </defs>

            <rect
              data-canvas-background
              :width="viewBoxSize.width"
              :height="viewBoxSize.height"
              fill="url(#designer-grid)"
              @click="clearCanvasState"
            />

            <g class="connections">
              <AppFlowConnection
                v-for="rendered in renderedConnections"
                :id="rendered.connection.id"
                :key="rendered.connection.id"
                v-bind="automation(`connection-${rendered.connection.id}`)"
                :start="rendered.start"
                :end="rendered.end"
                :start-side="rendered.startSide"
                :end-side="rendered.endSide"
                :selected="rendered.connection.id === selectedConnectionId"
                :label="`Connection from ${rendered.connection.start.nodeId} to ${rendered.connection.end.nodeId}`"
                @[EVENTS.SELECT]="handleConnectionSelection"
              />
              <AppFlowConnection
                v-if="previewStart && previewEnd"
                id="connection-preview"
                v-bind="automation('connection-preview')"
                :start="previewStart"
                :end="previewEnd"
                :start-side="previewStartSide"
                preview
              />
            </g>

            <AppFlowNode
              v-for="node in orderedNodes"
              :key="node.id"
              v-bind="automation(`node-${node.id}`)"
              :node="node"
              :selected="node.id === selectedNodeId"
              :status="runtime?.nodes[node.id]?.state ?? flow.status"
              :status-value="
                runtime?.nodes[node.id]?.value === undefined
                  ? undefined
                  : String(runtime.nodes[node.id]?.value)
              "
              :connection-start="connectionStart"
              :compatible-connector-keys="compatibleConnectorKeys"
              :current="node.id === currentNodeId"
              :breakpoint-positions="breakpointPositions(node.id)"
              :connector-values="connectorValues?.[node.id]"
              @[EVENTS.SELECT]="handleNodeSelection"
              @[EVENTS.DRAG_START]="handleDragStart"
              @[EVENTS.CONNECTOR_PRESS]="handleConnectorPress"
              @[EVENTS.CONNECTOR_ACTIVATE]="handleConnectorActivate"
              @[EVENTS.CONNECTOR_RELEASE]="handleConnectorRelease"
              @[EVENTS.CONNECTOR_PREVIEW]="handleConnectorPreview"
            />

            <text
              v-if="flow.nodes.length === 0"
              class="empty-message"
              :x="viewBoxSize.width / 2"
              :y="viewBoxSize.height / 2"
            >
              This flow does not have any nodes yet.
            </text>
          </svg>
        </div>
      </div>
      <AppFlowNodeConfigurationPanel
        v-if="selectedNode"
        v-bind="automation('node-configuration')"
        :node="selectedNode"
        :flow-interface="flow.interface"
        @[EVENTS.UPDATE_LABEL]="handleNodeLabelUpdate"
        @[EVENTS.UPDATE_CONFIGURATION]="handleNodeConfigurationUpdate"
      />
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, nextTick, ref, watch } from 'vue';

import AppButton from '@/components/AppButton.vue';
import { useAutomation } from '@/composables/useAutomation';
import { EVENTS } from '@/constants/events';
import AppFlowConnection from './AppFlowConnection.vue';
import AppFlowDesignerToolbar from './AppFlowDesignerToolbar.vue';
import AppFlowNode from './AppFlowNode.vue';
import AppFlowNodePalette from './AppFlowNodePalette.vue';
import AppFlowNodeConfigurationPanel from './AppFlowNodeConfigurationPanel.vue';
import {
  clientToSvgPoint,
  useDesignerViewport
} from '@/features/flows/composables/useDesignerViewport';
import { useDesignerSelection } from '@/features/flows/composables/useDesignerSelection';
import { useConnectionEditing } from '@/features/flows/composables/useConnectionEditing';
import {
  calculateDraggedPosition,
  constrainNodePosition,
  useNodeDragging
} from '@/features/flows/composables/useNodeDragging';
import { layoutConnectors, type Point } from '@/features/flows/geometry/connectorLayout';
import { getNodeKind } from '@/features/flows/nodeKinds';
import { canReorderNode, type ZOrderCommand } from '@/features/flows/graph/zOrder';
import { interpretDesignerKey } from '@/features/flows/graph/keyboardCommands';
import { validateConnection } from '@/features/flows/graph/connections';
import { createDefaultNode } from '@/features/flows/graph/createNode';
import type {
  ConnectorSide,
  FlowConnection as FlowConnectionModel,
  FlowConnectionEndpoint,
  FlowDefinition,
  FlowNodeConnector,
  FlowNode as FlowNodeModel
} from '@/features/flows/types';
import { flowNodeKinds } from '@/features/flows/nodeKinds';
import type { FlowRuntimeSnapshot } from '@/features/flows/api/flowRuntimeApi';
import type { ConnectorRuntimeValue } from '@/features/flows/api/flowRuntimeApi';
import type { FlowDebugBreakpoint } from '@/features/flows/api/flowDebugApi';

const props = defineProps<{
  automation: string;
  flow: FlowDefinition;
  runtime?: FlowRuntimeSnapshot;
  currentNodeId?: string;
  breakpoints?: FlowDebugBreakpoint[];
  connectorValues?: Record<string, Record<string, ConnectorRuntimeValue>>;
  debugging?: boolean;
  focusNodeId?: string;
}>();

const automation = useAutomation(props.automation);
const emit = defineEmits<{
  (event: typeof EVENTS.SET_BREAKPOINT, nodeId: string, position: 'before' | 'after' | null): void;
  (event: typeof EVENTS.RUN_TO_NODE, nodeId: string): void;
  (event: typeof EVENTS.MOVE_NODE, nodeId: string, x: number, y: number): void;
  (event: typeof EVENTS.REORDER_NODE, nodeId: string, command: ZOrderCommand): void;
  (event: typeof EVENTS.DELETE_NODE, nodeId: string): void;
  (
    event: typeof EVENTS.ADD_CONNECTION,
    start: FlowConnectionEndpoint,
    end: FlowConnectionEndpoint
  ): void;
  (event: typeof EVENTS.DELETE_CONNECTION, connectionId: string): void;
  (event: typeof EVENTS.ADD_NODE, node: FlowNodeModel): void;
  (event: typeof EVENTS.LEARN, kind: FlowNodeModel['kind']): void;
  (event: typeof EVENTS.UPDATE_NODE_LABEL, nodeId: string, label: string): void;
  (
    event: typeof EVENTS.UPDATE_NODE_CONFIGURATION,
    nodeId: string,
    key: string,
    value: FlowNodeModel['configuration'][string]
  ): void;
}>();

const handleNodeLabelUpdate = (label: string): void => {
  if (selectedNode.value) emit(EVENTS.UPDATE_NODE_LABEL, selectedNode.value.id, label);
};

const handleNodeConfigurationUpdate = (
  key: string,
  value: FlowNodeModel['configuration'][string]
): void => {
  if (selectedNode.value) {
    emit(EVENTS.UPDATE_NODE_CONFIGURATION, selectedNode.value.id, key, value);
  }
};
const breakpointPositions = (nodeId: string): ('before' | 'after')[] =>
  (props.breakpoints ?? [])
    .filter((breakpoint) => breakpoint.nodeId === nodeId)
    .map((breakpoint) => breakpoint.position);
const setSelectedBreakpoint = (position: 'before' | 'after' | null): void => {
  if (selectedNodeId.value) emit(EVENTS.SET_BREAKPOINT, selectedNodeId.value, position);
};

const viewportElement = ref<HTMLElement>();
const canvasElement = ref<SVGSVGElement>();
const snapToGrid = ref(true);
const {
  zoom,
  width: viewportWidth,
  canvasSize,
  viewBoxSize,
  setZoom
} = useDesignerViewport(viewportElement);
const {
  selectedNodeId,
  selectedConnectionId,
  selectNode,
  selectConnection,
  clearSelection,
  handleSelectionKeydown
} = useDesignerSelection();
watch(
  () => props.focusNodeId,
  (nodeId) => {
    if (nodeId && nodesById.value.has(nodeId)) selectNode(nodeId);
  }
);
const { dragState, startDrag, finishDrag, cancelDrag } = useNodeDragging();
const {
  connectionStart,
  previewEnd,
  connectionError,
  beginConnection,
  updatePreview,
  reportConnectionError,
  cancelConnection
} = useConnectionEditing();

const nodesById = computed(() => new Map(props.flow.nodes.map((node) => [node.id, node])));

const connectorPoint = (nodeId: string, connectorId: string): Point | undefined => {
  const node = nodesById.value.get(nodeId);
  if (!node) return undefined;
  const size = getNodeKind(node.kind).defaultSize;
  const layout = layoutConnectors(node.connectors, size.width, size.height).find(
    ({ connector }) => connector.id === connectorId
  );
  // Connector layout is relative to the node. Connections need canvas-wide
  // coordinates, so add the node's persisted position before drawing the path.
  return layout ? { x: node.x + layout.x, y: node.y + layout.y } : undefined;
};

interface ConnectionEndpoints {
  start: Point | undefined;
  end: Point | undefined;
  startSide: ConnectorSide | undefined;
  endSide: ConnectorSide | undefined;
}

const connectionEndpoints = (connection: FlowConnectionModel): ConnectionEndpoints => ({
  start: connectorPoint(connection.start.nodeId, connection.start.connectorId),
  end: connectorPoint(connection.end.nodeId, connection.end.connectorId),
  startSide: connectorAt(connection.start)?.side,
  endSide: connectorAt(connection.end)?.side
});
const renderedConnections = computed(() =>
  // Resolve each pair once per graph update. A large graph previously repeated
  // both node and connector lookups for the start and end template bindings.
  props.flow.connections.map((connection) => ({
    connection,
    ...connectionEndpoints(connection)
  }))
);
const connectorAt = (endpoint: FlowConnectionEndpoint): FlowNodeConnector | undefined =>
  nodesById.value
    .get(endpoint.nodeId)
    ?.connectors.find((connector) => connector.id === endpoint.connectorId);
const compatibleConnectorKeys = computed(() => {
  const start = connectionStart.value;
  if (!start) return [];
  // Reuse final connection validation for highlighting so the visual guidance
  // cannot disagree with the rule enforced when the user completes the link.
  return props.flow.nodes.flatMap((node) =>
    node.connectors
      .filter(
        (connector) =>
          validateConnection(props.flow, start, { nodeId: node.id, connectorId: connector.id })
            .valid
      )
      .map((connector) => `${node.id}:${connector.id}`)
  );
});
const previewStart = computed(() =>
  connectionStart.value
    ? connectorPoint(connectionStart.value.nodeId, connectionStart.value.connectorId)
    : undefined
);
const previewStartSide = computed(() =>
  connectionStart.value ? connectorAt(connectionStart.value)?.side : undefined
);

const orderedNodes = computed(() =>
  // SVG elements later in the document are painted on top of earlier elements.
  // Rendering by z-order makes the saved stacking order visible and interactive.
  [...props.flow.nodes].sort((left, right) => left.zOrder - right.zOrder)
);
const selectedNode = computed(() =>
  selectedNodeId.value ? nodesById.value.get(selectedNodeId.value) : undefined
);
const canMoveFront = computed(() =>
  selectedNodeId.value ? canReorderNode(props.flow.nodes, selectedNodeId.value, 'front') : false
);
const canMoveBack = computed(() =>
  selectedNodeId.value ? canReorderNode(props.flow.nodes, selectedNodeId.value, 'back') : false
);

const handleNodeSelection = (nodeId: FlowNodeModel['id']): void => selectNode(nodeId);
const handleConnectionSelection = (connectionId: string): void => selectConnection(connectionId);
const handleAddNode = (kind: FlowNodeModel['kind']): void => {
  const zOrder = Math.max(-1, ...props.flow.nodes.map((node) => node.zOrder)) + 1;
  // Stagger new nodes so repeated additions do not completely cover each other.
  // Wrapping after eight additions keeps the starting position within the canvas.
  const offset = (props.flow.nodes.length % 8) * 24;
  const node = createDefaultNode(kind, { x: 48 + offset, y: 72 + offset }, zOrder);
  emit(EVENTS.ADD_NODE, node);
  selectNode(node.id);
};
const handleLearn = (kind: FlowNodeModel['kind']): void => emit(EVENTS.LEARN, kind);
const addNodeAt = (kind: FlowNodeModel['kind'], position: Point): void => {
  const zOrder = Math.max(-1, ...props.flow.nodes.map((node) => node.zOrder)) + 1;
  const size = getNodeKind(kind).defaultSize;
  const constrained = constrainNodePosition(position, {
    width: viewBoxSize.value.width,
    height: viewBoxSize.value.height,
    nodeWidth: size.width,
    nodeHeight: size.height
  });
  const node = createDefaultNode(kind, constrained, zOrder);
  emit(EVENTS.ADD_NODE, node);
  selectNode(node.id);
};

const handlePaletteDrop = (event: DragEvent): void => {
  event.preventDefault();
  const kind = event.dataTransfer?.getData('application/x-flow-node-function-type');
  if (!kind || !flowNodeKinds.includes(kind as FlowNodeModel['kind'])) return;
  const point = pointerToCanvas(event as unknown as PointerEvent);
  if (!point) return;
  const size = getNodeKind(kind as FlowNodeModel['kind']).defaultSize;
  // Centre the new block under the cursor instead of placing its top-left corner
  // there, which makes the drop position match the user's drag intent.
  addNodeAt(kind as FlowNodeModel['kind'], {
    x: point.x - size.width / 2,
    y: point.y - size.height / 2
  });
};
const handleReorder = (command: ZOrderCommand): void => {
  if (selectedNodeId.value) emit(EVENTS.REORDER_NODE, selectedNodeId.value, command);
};

const handleCanvasKeydown = (event: KeyboardEvent): void => {
  if (event.key === 'Escape' && connectionStart.value) {
    event.preventDefault();
    cancelConnection();
    return;
  }
  if (handleSelectionKeydown(event)) return;
  const nodeId = selectedNodeId.value;
  const selectedLinkId = selectedConnectionId.value;
  if (!nodeId && !selectedLinkId) return;
  const command = interpretDesignerKey(event);
  if (!command) return;
  event.preventDefault();

  if (command.type === 'delete') {
    if (nodeId) emit(EVENTS.DELETE_NODE, nodeId);
    if (selectedLinkId) emit(EVENTS.DELETE_CONNECTION, selectedLinkId);
    clearSelection();
    // Deletion removes the focused SVG element. Restore focus after Vue updates
    // the document so keyboard users remain inside the designer.
    void nextTick(() => viewportElement.value?.focus());
    return;
  }

  if (!nodeId) return;
  const node = nodesById.value.get(nodeId);
  if (!node) return;
  const size = getNodeKind(node.kind).defaultSize;
  const position = constrainNodePosition(
    { x: node.x + command.deltaX, y: node.y + command.deltaY },
    {
      width: viewBoxSize.value.width,
      height: viewBoxSize.value.height,
      nodeWidth: size.width,
      nodeHeight: size.height
    }
  );
  emit(EVENTS.MOVE_NODE, nodeId, position.x, position.y);
};

const handleConnectorActivate = (endpoint: FlowConnectionEndpoint): void => {
  const connector = connectorAt(endpoint);
  const point = connectorPoint(endpoint.nodeId, endpoint.connectorId);
  if (!connector || !point) return;

  if (!connectionStart.value) {
    if (connector.direction !== 'output') {
      reportConnectionError('Start a connection from an output connector.');
      return;
    }
    // Give keyboard activation an immediately visible, non-zero preview. Pointer
    // movement replaces this temporary endpoint as soon as the cursor moves.
    beginConnection(endpoint, { x: point.x + 40, y: point.y + 20 });
    return;
  }

  if (
    connectionStart.value.nodeId === endpoint.nodeId &&
    connectionStart.value.connectorId === endpoint.connectorId
  )
    return;

  const validation = validateConnection(props.flow, connectionStart.value, endpoint);
  if (!validation.valid) {
    reportConnectionError(validation.message ?? 'That connection is not valid.');
    return;
  }
  emit(EVENTS.ADD_CONNECTION, connectionStart.value, endpoint);
  cancelConnection();
};

const handleConnectorPress = (endpoint: FlowConnectionEndpoint): void => {
  // Pointer-down starts a possible drag. When a connection is already active,
  // defer completion to either pointer release (drag) or click (two clicks).
  if (!connectionStart.value) handleConnectorActivate(endpoint);
};

const handleConnectorPreview = (endpoint: FlowConnectionEndpoint): void => {
  const start = connectionStart.value;
  // Pointer-down focuses the source connector. Do not let that focus event fold
  // the just-created preview back onto its own start point.
  if (start?.nodeId === endpoint.nodeId && start.connectorId === endpoint.connectorId) return;
  const point = connectorPoint(endpoint.nodeId, endpoint.connectorId);
  if (point) updatePreview(point);
};

const handleConnectorRelease = (endpoint: FlowConnectionEndpoint): void => {
  const start = connectionStart.value;
  if (!start) return;
  // Releasing on the connector where the gesture began is a normal click: keep
  // the preview active so the established click-source, click-destination flow
  // still works. Releasing over a different port completes a pointer drag.
  if (start.nodeId === endpoint.nodeId && start.connectorId === endpoint.connectorId) return;
  handleConnectorActivate(endpoint);
};

const clearCanvasState = (): void => {
  clearSelection();
  cancelConnection();
};

const pointerToCanvas = (event: PointerEvent): Point | undefined => {
  const rect = canvasElement.value?.getBoundingClientRect();
  if (!rect || rect.width === 0 || rect.height === 0) return undefined;
  // Pointer coordinates use displayed CSS pixels. Convert them into the
  // responsive SVG viewBox so zoom does not change graph-space movement.
  return clientToSvgPoint({ x: event.clientX, y: event.clientY }, rect, {
    x: 0,
    y: 0,
    ...viewBoxSize.value
  });
};

const handleDragStart = (nodeId: string, event: PointerEvent): void => {
  const node = nodesById.value.get(nodeId);
  const point = pointerToCanvas(event);
  if (!node || !point || event.button !== 0) return;
  selectNode(nodeId);
  // Pointer capture keeps delivering move and release events to this gesture
  // even when a fast drag leaves the node or the visible SVG boundary.
  try {
    (event.currentTarget as Element).setPointerCapture?.(event.pointerId);
  } catch {
    // Synthetic accessibility tests and a pointer cancelled by the browser
    // between dispatch and capture have no active pointer to capture. The drag
    // can still proceed through the canvas-level move and release listeners.
  }
  startDrag({
    nodeId,
    pointerId: event.pointerId,
    pointerStart: point,
    nodeStart: { x: node.x, y: node.y }
  });
};

const handlePointerMove = (event: PointerEvent): void => {
  const state = dragState.value;
  const point = pointerToCanvas(event);
  if (!point) return;
  // Connection previews follow every pointer move, while node movement proceeds
  // only when this pointer owns an active drag.
  updatePreview(point);
  if (!state || state.pointerId !== event.pointerId) return;
  const node = nodesById.value.get(state.nodeId);
  if (!node) return;
  const size = getNodeKind(node.kind).defaultSize;
  const position = calculateDraggedPosition(
    state,
    point,
    {
      width: viewBoxSize.value.width,
      height: viewBoxSize.value.height,
      nodeWidth: size.width,
      nodeHeight: size.height
    },
    24,
    snapToGrid.value
  );
  emit(EVENTS.MOVE_NODE, state.nodeId, position.x, position.y);
};

const handleDragEnd = (event: PointerEvent): void => {
  finishDrag(event.pointerId);
};

const handleDragCancel = (event: PointerEvent): void => {
  const state = dragState.value;
  const originalPosition = cancelDrag(event.pointerId);
  if (state && originalPosition) {
    emit(EVENTS.MOVE_NODE, state.nodeId, originalPosition.x, originalPosition.y);
  }
};
</script>

<style scoped>
.canvas-frame {
  display: flex;
  min-width: 0;
  min-height: 0;
  overflow: hidden;
  flex-direction: column;
  background: var(--color-surface-raised);
  border: var(--border-width-default) solid var(--color-border-subtle);
  border-radius: var(--radius-3xl);
  box-shadow: var(--shadow-panel);
}

.canvas-toolbar {
  display: flex;
  flex-wrap: wrap;
  gap: var(--space-9);
  align-items: center;
  min-height: 44px;
  padding: var(--space-0) var(--space-8);
  color: var(--color-text-muted);
  font-size: var(--font-size-md);
  font-weight: var(--font-weight-semibold);
  background: var(--color-surface-subtle);
  border-bottom: var(--border-width-default) solid var(--color-border-subtle);
}

.designer-workspace {
  position: relative;
  display: flex;
  min-height: 0;
  padding-left: var(--space-designer-palette-offset);
  flex: 1;
  overflow: hidden;
}

.designer-workspace :deep(.node-palette) {
  position: absolute;
  inset: 0 auto 0 0;
}

.designer-workspace :deep(.configuration-panel) {
  width: 280px;
  min-width: 280px;
}

.canvas-column {
  display: flex;
  min-width: 0;
  min-height: 0;
  flex: 1;
  flex-direction: column;
}

/* Tablet breakpoint (48rem): reflows multi-column controls and workspace panels. */
@media (max-width: 48rem) {
  .designer-workspace {
    padding-left: var(--space-0);
    flex-direction: column;
  }

  .designer-workspace :deep(.node-palette) {
    position: static;
    width: auto;
    min-width: 0;
    max-height: min(240px, 35%);
    overflow-y: auto;
    border-right: var(--border-width-none);
    border-bottom: var(--border-width-default) solid var(--color-border-subtle);
  }

  .designer-workspace :deep(.configuration-panel) {
    width: min(280px, 42vw);
    min-width: min(280px, 42vw);
  }
}

.selection {
  margin-left: auto;
  color: var(--color-action-primary-text);
}

.connection-error {
  margin: var(--space-0);
  padding: var(--space-4) var(--space-8);
  color: var(--color-danger-text);
  font-size: var(--font-size-md);
  font-weight: var(--font-weight-semibold);
  background: var(--color-danger-surface);
  border-bottom: var(--border-width-default) solid var(--color-danger-border-subtle);
}

.zoom-controls {
  display: flex;
  gap: var(--space-3-5);
  align-items: center;
  margin-left: auto;
}

.selection + .zoom-controls {
  margin-left: var(--space-0);
}

.zoom-controls output {
  width: 38px;
  color: var(--color-text-secondary);
  font-size: var(--font-size-sm);
  text-align: center;
}

.grid-toggle {
  display: flex;
  gap: var(--space-2-5);
  align-items: center;
  color: var(--color-text-secondary);
  white-space: nowrap;
}
.debug-actions {
  display: flex;
  flex-wrap: wrap;
  gap: var(--space-2);
}

.canvas-viewport {
  width: 100%;
  max-width: 100%;
  min-height: 0;
  overflow: auto;
  flex: 1;
  outline-offset: -3px;
}

.designer-canvas {
  display: block;
  max-width: none;
  background: var(--color-surface-inset);
}

.empty-message {
  fill: var(--color-text-subtle);
  font-size: var(--font-size-xl);
  text-anchor: middle;
}
</style>
