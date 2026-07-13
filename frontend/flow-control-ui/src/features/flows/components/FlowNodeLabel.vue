<script setup lang="ts">
defineProps<{ label: string; kindLabel: string }>();
</script>

<template>
  <text class="node-label" x="74" y="29">{{ label }}</text>
  <text class="node-kind" x="74" y="47">{{ kindLabel }}</text>
</template>

<style scoped>
.node-label,
.node-kind {
  fill: #102133;
  pointer-events: none;
}

.node-label {
  font-size: 13px;
  font-weight: 750;
}

.node-kind {
  font-size: 10px;
  letter-spacing: 0.08em;
  text-transform: uppercase;
  opacity: 0.7;
}
</style>
