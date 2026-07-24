<template>
  <div class="canvas-frame">
    <div class="canvas-toolbar">
      <span>{{ flow.nodes.length }} nodes</span>
      <span>{{ flow.connections.length }} connections</span>
      <span v-if="selectedConnectionId" class="selection">
        Selected connection: {{ selectedConnectionId }}
      </span>
      <div class="zoom-controls" aria-label="Canvas zoom controls">
        <button
          type="button"
          aria-label="Zoom out"
          :disabled="zoom <= 0.5"
          @click="setZoom(zoom - 0.25)"
        >
          −
        </button>
        <output aria-live="polite">{{ Math.round(zoom * 100) }}%</output>
        <button
          type="button"
          aria-label="Zoom in"
          :disabled="zoom >= 2"
          @click="setZoom(zoom + 0.25)"
        >
          +
        </button>
      </div>
      <label class="grid-toggle">
        <input v-model="snapToGrid" type="checkbox" />
        Snap to grid
      </label>
      <FlowDesignerToolbar
        :selected-node-id="selectedNodeId"
        :can-move-front="canMoveFront"
        :can-move-back="canMoveBack"
        @reorder="handleReorder"
      />
    </div>

    <div class="designer-workspace">
      <FlowNodePalette @add="handleAddNode" />
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
              <FlowConnection
                v-for="rendered in renderedConnections"
                :id="rendered.connection.id"
                :key="rendered.connection.id"
                :start="rendered.start"
                :end="rendered.end"
                :start-side="rendered.startSide"
                :end-side="rendered.endSide"
                :selected="rendered.connection.id === selectedConnectionId"
                :label="`Connection from ${rendered.connection.start.nodeId} to ${rendered.connection.end.nodeId}`"
                @select="handleConnectionSelection"
              />
              <FlowConnection
                v-if="previewStart && previewEnd"
                id="connection-preview"
                :start="previewStart"
                :end="previewEnd"
                :start-side="previewStartSide"
                preview
              />
            </g>

            <FlowNode
              v-for="node in orderedNodes"
              :key="node.id"
              :node="node"
              :selected="node.id === selectedNodeId"
              :status="runtime?.nodes[node.id]?.state ?? flow.status"
              :status-value="runtime?.nodes[node.id]?.value"
              :connection-start="connectionStart"
              :compatible-connector-keys="compatibleConnectorKeys"
              @select="handleNodeSelection"
              @dragstart="handleDragStart"
              @connectorpress="handleConnectorPress"
              @connectoractivate="handleConnectorActivate"
              @connectorrelease="handleConnectorRelease"
              @connectorpreview="handleConnectorPreview"
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
      <FlowNodeConfigurationPanel
        v-if="selectedNode"
        :node="selectedNode"
        @update-label="emit('updateNodeLabel', selectedNode.id, $event)"
        @update-configuration="
          (key, value) => emit('updateNodeConfiguration', selectedNode!.id, key, value)
        "
      />
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, nextTick, ref } from 'vue';

import FlowConnection from './FlowConnection.vue';
import FlowDesignerToolbar from './FlowDesignerToolbar.vue';
import FlowNode from './FlowNode.vue';
import FlowNodePalette from './FlowNodePalette.vue';
import FlowNodeConfigurationPanel from './FlowNodeConfigurationPanel.vue';
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

const props = defineProps<{
  flow: FlowDefinition;
  runtime?: FlowRuntimeSnapshot;
}>();

const emit = defineEmits<{
  moveNode: [nodeId: string, x: number, y: number];
  reorderNode: [nodeId: string, command: ZOrderCommand];
  deleteNode: [nodeId: string];
  addConnection: [start: FlowConnectionEndpoint, end: FlowConnectionEndpoint];
  deleteConnection: [connectionId: string];
  addNode: [node: FlowNodeModel];
  updateNodeLabel: [nodeId: string, label: string];
  updateNodeConfiguration: [
    nodeId: string,
    key: string,
    value: FlowNodeModel['configuration'][string]
  ];
}>();

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
  emit('addNode', node);
  selectNode(node.id);
};
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
  emit('addNode', node);
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
  if (selectedNodeId.value) emit('reorderNode', selectedNodeId.value, command);
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
    if (nodeId) emit('deleteNode', nodeId);
    if (selectedLinkId) emit('deleteConnection', selectedLinkId);
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
  emit('moveNode', nodeId, position.x, position.y);
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
  emit('addConnection', connectionStart.value, endpoint);
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
  emit('moveNode', state.nodeId, position.x, position.y);
};

const handleDragEnd = (event: PointerEvent): void => {
  finishDrag(event.pointerId);
};

const handleDragCancel = (event: PointerEvent): void => {
  const state = dragState.value;
  const originalPosition = cancelDrag(event.pointerId);
  if (state && originalPosition) {
    emit('moveNode', state.nodeId, originalPosition.x, originalPosition.y);
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
  border: 1px solid var(--color-border-subtle);
  border-radius: 14px;
  box-shadow: 0 18px 45px var(--color-shadow-panel);
}

.canvas-toolbar {
  display: flex;
  flex-wrap: wrap;
  gap: 18px;
  align-items: center;
  min-height: 44px;
  padding: 0 16px;
  color: var(--color-text-muted);
  font-size: 12px;
  font-weight: 650;
  background: var(--color-surface-subtle);
  border-bottom: 1px solid var(--color-border-subtle);
}

.designer-workspace {
  position: relative;
  display: flex;
  min-height: 0;
  padding-left: 220px;
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

@media (max-width: 720px) {
  .designer-workspace {
    padding-left: 0;
    flex-direction: column;
  }

  .designer-workspace :deep(.node-palette) {
    position: static;
    width: auto;
    min-width: 0;
    max-height: min(240px, 35%);
    overflow-y: auto;
    border-right: 0;
    border-bottom: 1px solid var(--color-border-subtle);
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
  margin: 0;
  padding: 9px 16px;
  color: var(--color-danger-text);
  font-size: 12px;
  font-weight: 650;
  background: var(--color-danger-surface);
  border-bottom: 1px solid var(--color-danger-border-subtle);
}

.zoom-controls {
  display: flex;
  gap: 8px;
  align-items: center;
  margin-left: auto;
}

.selection + .zoom-controls {
  margin-left: 0;
}

.zoom-controls button {
  display: grid;
  width: 28px;
  height: 28px;
  padding: 0;
  place-items: center;
  color: var(--color-text-primary);
  background: var(--color-surface-raised);
  border: 1px solid var(--color-border-default);
  border-radius: 6px;
  cursor: pointer;
}

.zoom-controls button:disabled {
  color: var(--color-text-placeholder);
  cursor: default;
}

.zoom-controls output {
  width: 38px;
  color: var(--color-text-secondary);
  font-size: 11px;
  text-align: center;
}

.grid-toggle {
  display: flex;
  gap: 6px;
  align-items: center;
  color: var(--color-text-secondary);
  white-space: nowrap;
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
  font-size: 14px;
  text-anchor: middle;
}
</style>
