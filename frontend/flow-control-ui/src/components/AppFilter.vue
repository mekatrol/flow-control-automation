<template>
  <form
    :class="[
      'app-filter',
      { 'app-filter--constrained': constrained, 'app-filter--stacked': layout === 'stacked' }
    ]"
    role="search"
    @submit.prevent="emit(EVENTS.APPLY_FILTER)"
  >
    <div class="app-filter-fields">
      <slot></slot>
    </div>
    <AppButton class="app-filter-apply" type="submit" :text="applyText" :icon="filterIcon" />
  </form>
</template>

<script setup lang="ts">
import filterIcon from '@/assets/icons/filter-icon.svg';
import AppButton from '@/components/AppButton.vue';
import { EVENTS } from '@/constants/events';

withDefaults(
  defineProps<{
    applyText?: string;
    constrained?: boolean;
    layout?: 'inline' | 'stacked';
  }>(),
  {
    applyText: 'Apply filter',
    constrained: false,
    layout: 'inline'
  }
);

const emit = defineEmits<{
  (event: typeof EVENTS.APPLY_FILTER): void;
}>();
</script>

<style scoped>
.app-filter {
  display: flex;
  flex-wrap: wrap;
  gap: var(--space-3-5);
  align-items: end;
  margin-bottom: var(--space-8);
}

.app-filter--constrained {
  max-width: var(--filter-max-width);
}

.app-filter-fields {
  display: flex;
  flex: 1 1 auto;
  flex-wrap: wrap;
  gap: var(--space-3-5);
  align-items: end;
  min-width: 0;
}

.app-filter--stacked,
.app-filter--stacked .app-filter-fields {
  align-items: stretch;
  flex-direction: column;
}

.app-filter :deep(.app-filter-field) {
  display: grid;
  flex: 1 1 auto;
  gap: var(--space-3);
  min-width: 0;
  color: var(--color-text-primary);
  font-size: var(--font-size-md);
  font-weight: var(--font-weight-bold);
}

.app-filter :deep(.app-filter-field.multi-select) {
  display: grid;
  align-items: stretch;
}

.app-filter :deep(.app-filter-field--content) {
  flex: 0 1 max-content;
  width: max-content;
  max-width: 100%;
}

.app-filter :deep(input[type='search']) {
  width: 100%;
  min-height: var(--control-min-height);
  padding: var(--space-4);
  color: var(--color-text-primary);
  background: var(--color-surface-raised);
  border: var(--border-width-default) solid var(--color-border-default);
  border-radius: var(--radius-md);
}

.app-filter-apply {
  flex: 0 0 auto;
}

/* Mobile breakpoint (40rem): stacks filter fields and actions for phone layouts. */
@media (max-width: 40rem) {
  .app-filter:not(.app-filter--stacked),
  .app-filter:not(.app-filter--stacked) .app-filter-fields {
    align-items: stretch;
    flex-direction: column;
  }

  .app-filter :deep(.app-filter-field--content) {
    width: 100%;
  }
}
</style>
