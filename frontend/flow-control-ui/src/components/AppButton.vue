<template>
  <button data-app-button :type="type" :aria-label="hideText ? text : ariaLabel">
    <slot name="icon">
      <span
        v-if="icon"
        class="button-icon"
        :style="{ maskImage: `url(&quot;${icon}&quot;)` }"
        aria-hidden="true"
      />
    </slot>
    <span v-if="!hideText" class="button-text">{{ text }}</span>
  </button>
</template>

<script setup lang="ts">
withDefaults(
  defineProps<{
    text: string;
    icon?: string;
    ariaLabel?: string;
    hideText?: boolean;
    type?: 'button' | 'submit' | 'reset';
  }>(),
  {
    icon: undefined,
    ariaLabel: undefined,
    hideText: false,
    type: 'button'
  }
);
</script>

<style scoped>
button {
  display: inline-flex;
  gap: 7px;
  align-items: center;
  justify-content: center;
}

.button-icon {
  display: inline-block;
  width: 18px;
  height: 18px;
  flex: 0 0 auto;
  background-color: currentcolor;
  mask-position: center;
  mask-repeat: no-repeat;
  mask-size: contain;
}
</style>
