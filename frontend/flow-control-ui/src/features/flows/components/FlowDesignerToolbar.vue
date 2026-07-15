<template>
  <div class="z-order-controls" role="toolbar" aria-label="Node stacking order">
    <button
      type="button"
      aria-label="Bring to front"
      title="Bring to front"
      :disabled="!selectedNodeId || !canMoveFront"
      @click="emit('reorder', 'front')"
    >
      <svg data-icon="front" aria-hidden="true" viewBox="0 0 28 28">
        <rect class="layer layer-back" x="2" y="9" width="13" height="13" rx="2" />
        <rect class="layer layer-front" x="7" y="4" width="13" height="13" rx="2" />
        <path class="direction" d="M 24 20 V 7 M 20.5 10.5 L 24 7 L 27.5 10.5" />
        <path class="destination" d="M 20.5 4 H 27.5" />
      </svg>
    </button>
    <button
      type="button"
      aria-label="Bring forward"
      title="Bring forward"
      :disabled="!selectedNodeId || !canMoveFront"
      @click="emit('reorder', 'forward')"
    >
      <svg data-icon="forward" aria-hidden="true" viewBox="0 0 28 28">
        <rect class="layer layer-back" x="2" y="9" width="13" height="13" rx="2" />
        <rect class="layer layer-front" x="7" y="4" width="13" height="13" rx="2" />
        <path class="direction" d="M 24 21 V 8 M 20.5 11.5 L 24 8 L 27.5 11.5" />
      </svg>
    </button>
    <button
      type="button"
      aria-label="Send backward"
      title="Send backward"
      :disabled="!selectedNodeId || !canMoveBack"
      @click="emit('reorder', 'backward')"
    >
      <svg data-icon="backward" aria-hidden="true" viewBox="0 0 28 28">
        <rect class="layer layer-back" x="2" y="9" width="13" height="13" rx="2" />
        <rect class="layer layer-front" x="7" y="4" width="13" height="13" rx="2" />
        <path class="direction" d="M 24 7 V 20 M 20.5 16.5 L 24 20 L 27.5 16.5" />
      </svg>
    </button>
    <button
      type="button"
      aria-label="Send to back"
      title="Send to back"
      :disabled="!selectedNodeId || !canMoveBack"
      @click="emit('reorder', 'back')"
    >
      <svg data-icon="back" aria-hidden="true" viewBox="0 0 28 28">
        <rect class="layer layer-back" x="2" y="9" width="13" height="13" rx="2" />
        <rect class="layer layer-front" x="7" y="4" width="13" height="13" rx="2" />
        <path class="direction" d="M 24 7 V 20 M 20.5 16.5 L 24 20 L 27.5 16.5" />
        <path class="destination" d="M 20.5 23 H 27.5" />
      </svg>
    </button>
  </div>
</template>

<script setup lang="ts">
import type { ZOrderCommand } from '../graph/zOrder';

defineProps<{
  selectedNodeId?: string;
  canMoveFront: boolean;
  canMoveBack: boolean;
}>();

const emit = defineEmits<{ reorder: [command: ZOrderCommand] }>();
</script>

<style scoped>
.z-order-controls {
  display: flex;
  gap: 4px;
}

button {
  display: grid;
  width: 40px;
  height: 40px;
  padding: 6px;
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
  width: 28px;
  height: 28px;
  fill: none;
  stroke: currentcolor;
  stroke-width: 1.8;
  stroke-linecap: round;
  stroke-linejoin: round;
}

.layer-front {
  fill: currentcolor;
  fill-opacity: 0.28;
}

.layer-back {
  fill: none;
  opacity: 0.72;
}

.direction {
  stroke-width: 2.4;
}

.destination {
  stroke-width: 2.4;
}

@media (max-width: 760px) {
  .z-order-controls {
    order: 2;
  }
}
</style>
