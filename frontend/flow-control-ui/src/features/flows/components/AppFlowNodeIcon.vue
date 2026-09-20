<template>
  <g class="node-icon" aria-hidden="true">
    <image
      class="node-icon-foreground"
      :href="getNodeIconUrl(icon)"
      x="16"
      y="16"
      width="40"
      height="40"
      preserveAspectRatio="xMidYMid meet"
    />
  </g>
</template>

<script setup lang="ts">
import { getNodeIconUrl } from '@/features/flows/nodeTypes';

defineProps<{ icon: string }>();
</script>

<style scoped>
.node-icon {
  pointer-events: none;
}

.node-icon-foreground {
  /*
   * These files are loaded as external SVG images, so their internal fill and
   * stroke values cannot inherit `currentColor` from this document. The theme
   * filter normalizes both black- and white-authored assets for the active
   * light, dark, or system theme.
   */
  filter: var(--filter-node-icon-foreground);
}
</style>
