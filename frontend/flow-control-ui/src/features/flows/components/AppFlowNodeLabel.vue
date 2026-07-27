<template>
  <text class="node-label" x="46" y="25">{{ kindLabel || label }}</text>
</template>

<script setup lang="ts">
defineProps<{ label: string; kindLabel: string }>();
</script>

<style scoped>
.node-label {
  fill: var(--color-text-primary);
  pointer-events: none;
  font-size: var(--font-size-base);
  font-weight: var(--font-weight-medium);
  text-transform: uppercase;
  user-select: none;
}
</style>
