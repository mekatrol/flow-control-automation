<template>
  <text class="node-label" x="46" y="25">{{ kindLabel || label }}</text>
</template>

<script setup lang="ts">
defineProps<{ label: string; kindLabel: string }>();
</script>

<style scoped>
.node-label {
  fill: #102133;
  pointer-events: none;
  font-size: 16px;
  font-weight: 500;
  text-transform: uppercase;
  user-select: none;
}
</style>
