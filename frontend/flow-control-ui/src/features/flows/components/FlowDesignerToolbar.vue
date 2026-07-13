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
      :disabled="!selectedNodeId || !canMoveFront"
      @click="emit('reorder', 'front')"
    >
      Bring to front
    </button>
    <button
      type="button"
      :disabled="!selectedNodeId || !canMoveFront"
      @click="emit('reorder', 'forward')"
    >
      Bring forward
    </button>
    <button
      type="button"
      :disabled="!selectedNodeId || !canMoveBack"
      @click="emit('reorder', 'backward')"
    >
      Send backward
    </button>
    <button
      type="button"
      :disabled="!selectedNodeId || !canMoveBack"
      @click="emit('reorder', 'back')"
    >
      Send to back
    </button>
  </div>
</template>

<style scoped>
.z-order-controls {
  display: flex;
  gap: 4px;
}

button {
  padding: 5px 7px;
  color: #34495b;
  font-size: 10px;
  font-weight: 700;
  background: #fff;
  border: 1px solid #cbd8e2;
  border-radius: 5px;
  cursor: pointer;
}

button:disabled {
  color: #9cabb7;
  cursor: default;
}

@media (max-width: 760px) {
  .z-order-controls {
    order: 2;
    width: 100%;
  }

  button {
    flex: 1;
  }
}
</style>
