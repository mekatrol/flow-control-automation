<template>
  <div class="form-group" v-bind="automation()">
    <slot></slot>
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
