<template>
  <RouterLink v-slot="{ href, navigate }" :to="to" custom>
    <a
      :href="href"
      :aria-current="ariaCurrent"
      :aria-label="ariaLabel ?? text"
      class="app-link"
      v-bind="automation()"
      @click="navigate"
    >
      <span v-if="$slots.icon" class="link-icon-slot" v-bind="automation('icon')">
        <slot name="icon" />
      </span>

      <AppSvg
        v-else-if="icon"
        class="link-icon"
        :src="icon"
        v-bind="automation('icon')"
        :size="18"
      />

      <span v-if="!hideText" class="link-text" v-bind="automation('text')">
        {{ text }}
      </span>
    </a>
  </RouterLink>
</template>

<script setup lang="ts">
import AppSvg from '@/components/AppSvg.vue';
import { useAutomation } from '@/composables/useAutomation';
import type { RouteLocationRaw } from 'vue-router';
import type { AriaAttributes } from 'vue';

const props = withDefaults(
  defineProps<{
    text: string;
    to: RouteLocationRaw;
    automation: string;
    icon?: string;
    ariaCurrent?: AriaAttributes['aria-current'];
    ariaLabel?: string;
    hideText?: boolean;
  }>(),
  {
    icon: undefined,
    ariaCurrent: undefined,
    ariaLabel: undefined,
    hideText: false
  }
);

const automation = useAutomation(props.automation);
</script>

<style scoped>
.app-link {
  display: inline-flex;
  gap: var(--space-3);
  align-items: center;
  justify-content: center;
  min-height: var(--control-min-height);
  padding: var(--space-4) var(--space-6-5);
  color: var(--color-text-primary);
  font-weight: var(--font-weight-semibold);
  text-decoration: none;
  background: var(--color-surface-raised);
  border: var(--border-width-default) solid var(--color-border-default);
  border-radius: var(--radius-lg);
  cursor: pointer;
}

.app-link:hover {
  color: var(--color-action-primary-strong);
  background: var(--color-action-primary-surface);
  border-color: var(--color-action-primary);
}

.link-icon-slot {
  display: inline-grid;
  width: 18px;
  height: 18px;
  flex: 0 0 auto;
  place-items: center;
}

.link-icon-slot :deep(svg),
.link-icon-slot :deep(img),
.link-icon-slot :deep(span) {
  width: 100%;
  height: 100%;
}

.link-icon-slot :deep(svg) {
  fill: none;
  stroke: currentcolor;
  stroke-width: var(--stroke-width-standard);
  stroke-linecap: round;
  stroke-linejoin: round;
}

.link-text {
  white-space: nowrap;
}
</style>
