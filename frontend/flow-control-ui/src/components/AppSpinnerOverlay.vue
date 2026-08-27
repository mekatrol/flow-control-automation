<template>
  <Transition name="spinner-overlay">
    <div
      v-if="isWaiting"
      class="spinner-overlay"
      role="status"
      aria-live="polite"
      aria-label="Please wait"
    >
      <span class="spinner" aria-hidden="true" />
      <span class="visually-hidden">Please wait</span>
    </div>
  </Transition>
</template>

<script setup lang="ts">
import { useWait } from '@/composables/useWait';

const { isWaiting } = useWait();
</script>

<style scoped>
.spinner-overlay {
  position: fixed;
  z-index: 1000;
  inset: 0;
  display: grid;
  place-items: center;
  cursor: wait;
  background: var(--color-modal-backdrop);
}

.spinner {
  width: var(--space-40);
  height: var(--space-40);
  border: var(--space-3) solid var(--color-surface-raised);
  border-top-color: var(--color-action-primary);
  border-radius: var(--radius-pill);
  box-shadow: var(--shadow-dialog);
  animation: spin 0.8s linear infinite;
}

.visually-hidden {
  position: absolute;
  width: 1px;
  height: 1px;
  padding: 0;
  overflow: hidden;
  clip: rect(0, 0, 0, 0);
  white-space: nowrap;
  border: 0;
}

.spinner-overlay-enter-active,
.spinner-overlay-leave-active {
  transition: opacity 0.15s ease;
}

.spinner-overlay-enter-from,
.spinner-overlay-leave-to {
  opacity: 0;
}

@keyframes spin {
  to {
    transform: rotate(360deg);
  }
}

@media (prefers-reduced-motion: reduce) {
  .spinner {
    animation-duration: 1.6s;
  }
}
</style>
