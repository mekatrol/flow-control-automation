<template>
  <button
    v-bind="automation()"
    data-app-button
    :type="type"
    :aria-label="hideText ? text : ariaLabel"
  >
    <span v-if="$slots.icon" class="button-icon-slot">
      <slot name="icon" />
    </span>
    <AppSvg
      v-else-if="icon"
      class="button-icon"
      :src="icon"
      automation="app-button-icon"
      :size="18"
    />
    <span v-if="!hideText" class="button-text">{{ text }}</span>
  </button>
</template>

<script setup lang="ts">
import AppSvg from '@/components/AppSvg.vue';
import { useAutomation } from '@/composables/useAutomation';

const props = withDefaults(
  defineProps<{
    text: string;
    automation?: string;
    icon?: string;
    ariaLabel?: string;
    hideText?: boolean;
    type?: 'button' | 'submit' | 'reset';
  }>(),
  {
    automation: '',
    icon: undefined,
    ariaLabel: undefined,
    hideText: false,
    type: 'button'
  }
);

const automation = useAutomation(props.automation);
</script>

<style scoped>
button {
  display: inline-flex;
  gap: 7px;
  align-items: center;
  justify-content: center;
  min-height: 44px;
  padding: 9px 14px;
  color: var(--color-text-primary);
  font-weight: 650;
  background: var(--color-surface-raised);
  border: 1px solid var(--color-border-default);
  border-radius: 8px;
  cursor: pointer;
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
  stroke-width: 1.8;
  stroke-linecap: round;
  stroke-linejoin: round;
}

</style>
