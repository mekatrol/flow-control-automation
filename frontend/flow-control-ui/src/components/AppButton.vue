<template>
  <button
    v-bind="automation()"
    data-app-button
    :class="props.size"
    :type="type"
    :aria-label="hideText ? text : ariaLabel"
  >
    <span v-if="$slots.icon" class="button-icon-slot" v-bind="automation('icon')">
      <slot name="icon" />
    </span>
    <AppSvg
      v-else-if="icon"
      class="button-icon"
      :src="icon"
      v-bind="automation('icon')"
      :size="18"
    />
    <span v-if="!hideText" class="button-text" v-bind="automation('text')">{{ text }}</span>
  </button>
</template>

<script setup lang="ts">
import AppSvg from '@/components/AppSvg.vue';
import { useAutomation } from '@/composables/useAutomation';

const props = withDefaults(
  defineProps<{
    text: string;
    automation: string;
    icon?: string;
    ariaLabel?: string;
    hideText?: boolean;
    type?: 'button' | 'submit' | 'reset';
    size?: string;
  }>(),
  {
    icon: undefined,
    ariaLabel: undefined,
    hideText: false,
    type: 'button',
    size: undefined
  }
);

const automation = useAutomation(props.automation);
</script>

<style scoped>
button {
  display: inline-flex;
  gap: var(--space-3);
  align-items: center;
  justify-content: center;
  min-height: var(--control-min-height);
  padding: var(--space-4) var(--space-6-5);
  color: var(--color-text-primary);
  font-weight: var(--font-weight-semibold);
  background: var(--color-surface-raised);
  border: var(--border-width-default) solid var(--color-border-default);
  border-radius: var(--radius-lg);
  cursor: pointer;
}

button.light-weight {
  font-weight: var(--font-weight-regular);
}

button:hover:not(:disabled) {
  color: var(--color-action-primary-strong);
  background: var(--color-action-primary-surface);
  border-color: var(--color-action-primary);
}

button:disabled {
  color: var(--color-text-muted);
  cursor: not-allowed;
  background: var(--color-surface-disabled);
  border-style: dashed;
}

.button-icon-slot {
  display: inline-block;
  width: 18px;
  height: 18px;
  flex: 0 0 auto;
}

.button-icon-slot {
  display: inline-grid;
  place-items: center;
}

.button-icon-slot :deep(svg),
.button-icon-slot :deep(img),
.button-icon-slot :deep(span) {
  width: 100%;
  height: 100%;
}

.button-icon-slot :deep(svg) {
  fill: none;
  stroke: currentcolor;
  stroke-width: var(--stroke-width-standard);
  stroke-linecap: round;
  stroke-linejoin: round;
}

.button-text {
  white-space: nowrap;
}
</style>
