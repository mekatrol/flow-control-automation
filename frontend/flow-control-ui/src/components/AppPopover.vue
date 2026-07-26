<template>
  <div
    v-bind="automation()"
    :id="id"
    ref="panel"
    class="popover-panel"
    :data-placement="placement"
    :aria-label="contentLabel"
    :popover="popoverMode"
    role="dialog"
  >
    <slot />
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue';
import { useAutomation } from '@/composables/useAutomation';

type PopoverMode = 'auto' | 'manual';
type PopoverPlacement = 'center' | 'bottom-start' | 'bottom-end' | 'top-start' | 'top-end';

// This component follows the no-JavaScript native Popover API pattern
// demonstrated in Kevin Powell's video:
// https://www.youtube.com/watch?v=Xh6nT6LK0kQ
//
// AppPopover only renders the popover surface. The trigger stays separate and
// should use native HTML such as:
//
//   <button type="button" popovertarget="app-options">App options</button>
//
// Keeping the component this small lets the browser own toggling, top-layer
// behavior, light-dismiss, Escape handling, CSS anchor positioning, and the
// popover transition lifecycle.
const props = withDefaults(
  defineProps<{
    id: string;
    contentLabel: string;
    automation?: string;
    popoverMode?: PopoverMode;
    placement?: PopoverPlacement;
  }>(),
  {
    automation: '',
    placement: 'center',
    popoverMode: 'auto'
  }
);

const panel = ref<HTMLElement>();
const automation = useAutomation(props.automation);

defineExpose({
  hide: (): void => panel.value?.hidePopover()
});
</script>
