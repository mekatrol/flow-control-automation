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
   * stroke values cannot inherit `currentColor` from this document. First
   * collapse every authored colour to black, then invert it for dark system
   * themes. This gives black icons in light mode and white icons in dark mode
   * regardless of whether the source SVG was originally black or white.
   */
  filter: brightness(0);
}

@media (prefers-color-scheme: dark) {
  .node-icon-foreground {
    filter: brightness(0) invert(1);
  }
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
