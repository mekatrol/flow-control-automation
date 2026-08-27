<template>
  <tr>
    <td :colspan="columnCount">
      <div class="footer-content">
        <slot>
          <span>{{ totalItems }} total results</span>
        </slot>
        <button v-if="showReset" type="button" @click="emit(ListFooterRowEmit.Reset)">
          Reset filters and sorting
        </button>
      </div>
    </td>
  </tr>
</template>

<script setup lang="ts">
import { ListFooterRowEmit } from '@/models/listViewEmits';

interface Props {
  columnCount: number;
  totalItems: number;
  showReset?: boolean;
}

withDefaults(defineProps<Props>(), {
  showReset: false
});

type Emits = {
  reset: [];
};

const emit = defineEmits<Emits>();
</script>

<style scoped>
td {
  padding: 0.75rem;
  border-block-start: 1px solid var(--color-border-default);
  background: var(--color-surface-disabled);
}

.footer-content {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  justify-content: space-between;
  gap: 0.75rem;
}

button {
  min-height: 2.75rem;
  border: 1px solid var(--color-border-default);
  border-radius: 0.35rem;
  background: var(--color-surface-subtle);
  color: var(--color-text-primary);
  font: inherit;
  padding-inline: 0.9rem;
  cursor: pointer;
}

button:focus-visible {
  outline: 2px solid var(--color-action-primary);
  outline-offset: 2px;
  box-shadow: var(--shadow-focus);
}
</style>
