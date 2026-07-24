<template>
  <button data-app-button :type="type" :aria-label="hideText ? text : undefined">
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
    hideText?: boolean;
    type?: 'button' | 'submit' | 'reset';
  }>(),
  {
    icon: undefined,
    hideText: false,
    type: 'button'
  }
);
</script>

<style scoped>
.button-icon {
  width: 18px;
  height: 18px;
  flex: 0 0 auto;
  background-color: currentcolor;
  mask-position: center;
  mask-repeat: no-repeat;
  mask-size: contain;
}
</style>
