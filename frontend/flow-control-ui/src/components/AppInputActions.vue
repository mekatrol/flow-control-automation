<template>
  <div v-bind="automation()" class="input-actions">
    <label v-if="props.label" :for="inputId">
      {{ props.label }}
    </label>

    <div class="input-wrapper">
      <input
        :id="inputId"
        ref="input"
        v-model="value"
        v-bind="$attrs"
        :type="type"
        class="accessible-input"
        :aria-label="props.inputAriaLabel ?? props.label"
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
        <slot name="clear-icon">
          <svg class="default-clear-icon" viewBox="0 0 24 24" aria-hidden="true">
            <path d="M6 6l12 12M18 6L6 18" />
          </svg>
        </slot>
      </button>

      <button
        v-if="showAction"
        type="button"
        class="action-btn"
        :disabled="props.actionDisabled"
        :aria-label="props.actionAriaLabel"
        @click="action"
      >
        <slot name="action-icon">
          <svg viewBox="0 0 24 24" aria-hidden="true">
            <circle cx="11" cy="11" r="7" fill="none" stroke="currentColor" stroke-width="2" />
            <path
              d="M16.5 16.5L21 21"
              fill="none"
              stroke="currentColor"
              stroke-width="2"
              stroke-linecap="round"
            />
          </svg>
        </slot>
      </button>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, ref, useId, type InputHTMLAttributes } from 'vue';
import { useAutomation } from '@/composables/useAutomation';

type InputType = InputHTMLAttributes['type'];

defineOptions({
  // Native input attributes are forwarded explicitly to the underlying input.
  inheritAttrs: false
});

const props = withDefaults(
  defineProps<{
    id?: string;
    type?: InputType;
    label?: string;
    inputAriaLabel?: string;
    clearAriaLabel?: string;
    actionAriaLabel?: string;
    showAction?: boolean;
    automation: string;
    modelValue?: string;
    actionDisabled?: boolean;
  }>(),
  {
    id: undefined,
    modelValue: '',
    type: 'text',
    label: undefined,
    inputAriaLabel: undefined,
    clearAriaLabel: 'Clear input',
    actionAriaLabel: 'Search',
    showAction: false,
    actionDisabled: false
  }
);

type Emits = {
  'update:modelValue': [value: string];
  clear: [];
  action: [];
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

const action = (): void => {
  emit('action');
};
</script>

<style lang="css">
.input-actions {
  width: 100%;
  min-width: 0;
}

.input-wrapper {
  display: flex;
  align-items: center;

  width: 100%;
  min-width: 0;
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

.clear-btn,
.action-btn {
  flex: 0 0 auto;

  display: flex;
  align-items: center;
  justify-content: center;

  width: 2rem;
  height: 2rem;
  margin-right: var(--space-2);
  padding: 0;

  color: var(--color-text-muted);
  background: none;
  border: none;
  border-radius: var(--radius-sm);
  cursor: pointer;
}

.clear-btn svg,
.action-btn svg {
  display: block;

  width: 1.5rem;
  height: 1.5rem;
}

.default-clear-icon {
  fill: none;
  stroke: currentColor;
  stroke-width: 2.5;
  stroke-linecap: round;
}

.clear-btn:hover,
.action-btn:not(:disabled):hover {
  background: var(--color-surface-neutral);
}

.clear-btn-hidden {
  visibility: hidden;
  pointer-events: none;
  cursor: default;
}

.clear-btn:focus-visible,
.action-btn:focus-visible {
  outline: var(--outline-width-focus) solid var(--color-focus-ring);
  outline-offset: var(--space-0-5);
}

.action-btn:disabled {
  color: var(--color-text-disabled);
  cursor: not-allowed;
}
</style>
