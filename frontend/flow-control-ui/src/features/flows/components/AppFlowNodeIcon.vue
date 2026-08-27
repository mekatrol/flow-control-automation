<template>
  <g class="node-icon" aria-hidden="true">
    <path class="node-icon-shade" d="M2 0h38v40H2a2 2 0 0 1-2-2V2a2 2 0 0 1 2-2Z" />
    <image
      class="node-icon-foreground"
      :href="getNodeIconUrl(icon)"
      x="8"
      y="8"
      width="24"
      height="24"
    />
    <path class="node-icon-separator" d="M39.5.5v39" />
  </g>
</template>

<script setup lang="ts">
import { getNodeIconUrl } from '@/features/flows/nodeKinds';

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

.node-icon-shade {
  fill: var(--color-control-contrast);
  fill-opacity: 0.3;
}

.node-icon-separator {
  fill: none;
  stroke: var(--color-node-icon-outline);
  stroke-opacity: 0.4;
}
</style>
