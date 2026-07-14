<script setup lang="ts">
import type { ZOrderCommand } from '../graph/zOrder';

defineProps<{
  selectedNodeId?: string;
  canMoveFront: boolean;
  canMoveBack: boolean;
}>();

const emit = defineEmits<{ reorder: [command: ZOrderCommand] }>();
</script>

<template>
  <div class="z-order-controls" role="toolbar" aria-label="Node stacking order">
    <button
      type="button"
      aria-label="Bring to front"
      title="Bring to front"
      :disabled="!selectedNodeId || !canMoveFront"
      @click="emit('reorder', 'front')"
    >
      <svg aria-hidden="true" viewBox="0 0 26 26">
        <rect x="1" y="1" width="10" height="10" fill="none" rx="2" />
        <rect x="14" y="14" width="10" height="10" fill="none" rx="2" />
        <rect class="filled" x="6" y="6" width="12" height="12" rx="2" />
      </svg>
    </button>
    <button
      type="button"
      aria-label="Bring forward"
      title="Bring forward"
      :disabled="!selectedNodeId || !canMoveFront"
      @click="emit('reorder', 'forward')"
    >
      <svg aria-hidden="true" viewBox="0 0 26 26">
        <rect x="12" y="12" width="10" height="10" fill="none" rx="2" />
        <rect class="filled" x="4" y="4" width="12" height="12" rx="2" />
      </svg>
    </button>
    <button
      type="button"
      aria-label="Send backward"
      title="Send backward"
      :disabled="!selectedNodeId || !canMoveBack"
      @click="emit('reorder', 'backward')"
    >
      <svg aria-hidden="true" viewBox="0 0 26 26">
        <rect class="filled" x="12" y="12" width="10" height="10" rx="2" />
        <rect x="4" y="4" width="12" height="12" fill="none" rx="2" />
      </svg>
    </button>
    <button
      type="button"
      aria-label="Send to back"
      title="Send to back"
      :disabled="!selectedNodeId || !canMoveBack"
      @click="emit('reorder', 'back')"
    >
      <svg aria-hidden="true" viewBox="0 0 26 26">
        <rect class="filled" x="1" y="1" width="10" height="10" rx="2" />
        <rect class="filled" x="14" y="14" width="10" height="10" rx="2" />
        <rect x="6" y="6" width="12" height="12" fill="none" rx="2" />
      </svg>
    </button>
  </div>
</template>

<style scoped>
.z-order-controls {
  display: flex;
  gap: 4px;
}

button {
  display: grid;
  width: 40px;
  height: 40px;
  padding: 7px;
  color: #fff;
  background: #1e90ff;
  border: 0;
  cursor: pointer;
}

button:hover:not(:disabled),
button:focus-visible {
  background: #000;
}

button:disabled {
  color: #ddd;
  background: #666;
  cursor: default;
}

svg {
  width: 26px;
  height: 26px;
  fill: none;
  stroke: currentcolor;
  stroke-width: 2;
}

.filled {
  fill: currentcolor;
}

@media (max-width: 760px) {
  .z-order-controls {
    order: 2;
  }
}
</style>
