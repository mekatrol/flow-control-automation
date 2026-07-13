<script setup lang="ts">
defineProps<{ icon: string }>();
</script>

<template>
  <text class="node-icon" x="29" y="39" text-anchor="middle" aria-hidden="true">{{ icon }}</text>
</template>

<style scoped>
.node-icon {
  fill: #102133;
  font-size: 18px;
  font-weight: 800;
  pointer-events: none;
}
</style>
