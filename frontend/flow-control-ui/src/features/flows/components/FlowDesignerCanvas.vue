<script setup lang="ts">
import { computed, ref } from 'vue';

import FlowNode from './FlowNode.vue';
import type { FlowConnection, FlowDefinition, FlowNode as FlowNodeModel } from '../types';

const props = defineProps<{
  flow: FlowDefinition;
}>();

const selectedNodeId = ref<string>();

const nodesById = computed(() => new Map(props.flow.nodes.map((node) => [node.id, node])));

const connectionPath = (connection: FlowConnection): string => {
  const start = nodesById.value.get(connection.start.nodeId);
  const end = nodesById.value.get(connection.end.nodeId);

  if (!start || !end) {
    return '';
  }

  const startX = start.x + 210;
  const startY = start.y + 32;
  const endX = end.x;
  const endY = end.y + 32;
  const controlOffset = Math.max(80, Math.abs(endX - startX) * 0.45);

  return `M ${startX} ${startY} C ${startX + controlOffset} ${startY}, ${endX - controlOffset} ${endY}, ${endX} ${endY}`;
};

const selectNode = (nodeId: FlowNodeModel['id']): void => {
  selectedNodeId.value = nodeId;
};
</script>

<template>
  <div class="canvas-frame">
    <div class="canvas-toolbar">
      <span>{{ flow.nodes.length }} nodes</span>
      <span>{{ flow.connections.length }} connections</span>
      <span v-if="selectedNodeId" class="selection">Selected: {{ selectedNodeId }}</span>
    </div>

    <svg
      class="designer-canvas"
      viewBox="0 0 1100 560"
      role="img"
      :aria-label="`${flow.name} flow graph`"
      @click.self="selectedNodeId = undefined"
    >
      <defs>
        <pattern id="designer-grid" width="24" height="24" patternUnits="userSpaceOnUse">
          <path d="M24 0H0V24" fill="none" stroke="#d8e2ea" stroke-width="1" />
        </pattern>
      </defs>

      <rect width="1100" height="560" fill="url(#designer-grid)" />

      <g class="connections">
        <path
          v-for="connection in flow.connections"
          :key="connection.id"
          :d="connectionPath(connection)"
        />
      </g>

      <FlowNode
        v-for="node in flow.nodes"
        :key="node.id"
        :node="node"
        :selected="node.id === selectedNodeId"
        @select="selectNode"
      />

      <text v-if="flow.nodes.length === 0" class="empty-message" x="550" y="280">
        This flow does not have any nodes yet.
      </text>
    </svg>
  </div>
</template>

<style scoped>
.canvas-frame {
  overflow: hidden;
  background: #fff;
  border: 1px solid #d8e2ea;
  border-radius: 14px;
  box-shadow: 0 18px 45px rgb(31 55 75 / 8%);
}

.canvas-toolbar {
  display: flex;
  gap: 18px;
  align-items: center;
  min-height: 44px;
  padding: 0 16px;
  color: #627587;
  font-size: 12px;
  font-weight: 650;
  background: #f8fbfd;
  border-bottom: 1px solid #d8e2ea;
}

.selection {
  margin-left: auto;
  color: #0b6e63;
}

.designer-canvas {
  display: block;
  width: 100%;
  min-width: 760px;
  background: #fbfdfe;
}

.connections path {
  fill: none;
  stroke: #4f7b96;
  stroke-width: 3;
}

.empty-message {
  fill: #718394;
  font-size: 14px;
  text-anchor: middle;
}
</style>
