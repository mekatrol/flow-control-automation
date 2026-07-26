<template>
  <div class="form" v-bind="automation()">
    <slot name="header"></slot>
    <form>
      <slot></slot>
      <slot name="footer"></slot>
    </form>
  </div>
</template>

<script setup lang="ts">
import { useAutomation } from '@/composables/useAutomation';

const props = withDefaults(
  defineProps<{
    automation: string;
  }>(),
  {}
);

const automation = useAutomation(props.automation);
</script>
