<template>
  <span
    v-bind="automation()"
    class="app-svg"
    :style="svgStyle"
    :role="label ? 'img' : undefined"
    :aria-label="label"
    :aria-hidden="label ? undefined : 'true'"
  />
</template>

<script setup lang="ts">
import { computed } from 'vue';
import type { CSSProperties } from 'vue';

import { useAutomation } from '@/composables/useAutomation';

export type AppSvgSize = number | string;

const props = withDefaults(
  defineProps<{
    src: string;
    automation: string;
    size?: AppSvgSize;
    width?: AppSvgSize;
    height?: AppSvgSize;
    color?: string;
    fit?: 'contain' | 'cover';
    label?: string;
  }>(),
  {
    size: '1em',
    width: undefined,
    height: undefined,
    color: undefined,
    fit: 'contain',
    label: undefined
  }
);

const automation = useAutomation(props.automation);

const cssSize = (value: AppSvgSize): string =>
  typeof value === 'number' ? `${String(value)}px` : value;

const escapedSource = computed(() => props.src.replaceAll('\\', '\\\\').replaceAll('"', '\\"'));
const svgStyle = computed<CSSProperties>(() => ({
  width: cssSize(props.width ?? props.size),
  height: cssSize(props.height ?? props.size),
  color: props.color,
  maskImage: `url("${escapedSource.value}")`,
  maskSize: props.fit
}));
</script>

<style scoped>
.app-svg {
  display: inline-block;
  flex: 0 0 auto;
  background-color: currentcolor;
  mask-position: center;
  mask-repeat: no-repeat;
}
</style>
