<template>
  <div v-bind="automation()" class="clearable-input">
    <label v-if="props.label" :for="inputId">
      {{ props.label }}
    </label>

    <div class="input-wrapper">
      <input
        :id="inputId"
        ref="input"
        v-model="value"
        v-bind="$attrs"
        type="text"
        class="accessible-input"
        :aria-label="props.label ? undefined : props.inputAriaLabel"
        @keydown.esc="clear"
      />

      <button
        type="button"
        class="clear-btn"
        :class="{ 'clear-btn-hidden': !value.trim().length }"
        :aria-label="props.clearAriaLabel"
        :aria-controls="inputId"
        @click="clear"
      >
        <svg viewBox="0 0 24 24" aria-hidden="true">
          <path d="M6 6l12 12M18 6L6 18" />
        </svg>
      </button>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, ref, useId } from 'vue';
import { useAutomation } from '@/composables/useAutomation';

defineOptions({
  // Native input attributes are forwarded explicitly to the underlying input.
  inheritAttrs: false
});

const props = withDefaults(
  defineProps<{
    id?: string;
    label?: string;
    inputAriaLabel?: string;
    clearAriaLabel?: string;
    automation: string;
    modelValue?: string;
  }>(),
  {
    id: undefined,
    label: undefined,
    inputAriaLabel: 'Input',
    clearAriaLabel: 'Clear input',
    modelValue: ''
  }
);

type Emits = {
  'update:modelValue': [value: string];
  clear: [];
};

const emit = defineEmits<Emits>();

const input = ref<HTMLInputElement | null>(null);
const generatedId = useId();
const inputId = computed((): string => props.id ?? generatedId);

const automation = useAutomation(props.automation);

const value = computed({
  get: (): string => props.modelValue,
  set: (newValue: string): void => {
    emit('update:modelValue', newValue);
  }
});

const clear = (): void => {
  // Don't emit if model already cleared, to avoid unnecessary re-renders in parent components.
  if (!value.value) {
    return;
  }

  emit('update:modelValue', '');
  emit('clear');
  input.value?.focus();
};
</script>

<style lang="css">
.clearable-input {
  width: 100%;
  min-width: 0;
}

.input-wrapper {
  width: 100%;
  min-width: 0;
}

.input-wrapper {
  display: flex;
  align-items: center;

  width: 100%;
  min-height: var(--control-min-height);

  background: var(--color-surface-raised);
  border: var(--border-width-default) solid var(--color-border-default);
  border-radius: var(--radius-lg);
}

.accessible-input {
  flex: 1 1 0;
  min-width: 0;

  padding: var(--space-4);
  box-sizing: border-box;

  color: var(--color-text-primary);
  font-size: 16px;
  background: transparent;

  border: none;
  outline: none;
  box-shadow: none;
}

.accessible-input:focus,
.accessible-input:focus-visible {
  border: none;
  outline: none;
  box-shadow: none;
}

.input-wrapper:focus-within {
  outline: var(--outline-width-focus) solid var(--color-focus-ring);
  outline-offset: var(--space-0-5);
}

.clear-btn {
  flex: 0 0 auto;

  display: flex;
  align-items: center;
  justify-content: center;

  width: 1.5rem;
  height: 1.5rem;
  margin-right: var(--space-2);
  padding: 0;

  background: none;
  border: none;
  cursor: pointer;
  color: var(--color-text-muted);
}

.clear-btn svg {
  width: 1.5rem;
  height: 1.5rem;
  display: block;

  fill: none;
  stroke: currentColor;
  stroke-width: 2.5;
  stroke-linecap: round;
}

.clear-btn-hidden {
  visibility: hidden;
  pointer-events: none;
  cursor: default;
}

.clear-btn:focus-visible {
  outline: var(--outline-width-focus) solid var(--color-focus-ring);
  outline-offset: var(--space-0-5);
  border-radius: var(--radius-sm);
}
</style>
