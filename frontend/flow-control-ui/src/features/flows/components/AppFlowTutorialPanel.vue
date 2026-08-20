<template>
  <section v-bind="automation()" class="tutorial" aria-labelledby="tutorial-title">
    <header>
      <div>
        <p class="category">{{ tutorial.category }}</p>
        <h2 id="tutorial-title">{{ tutorial.title }}</h2>
        <p>{{ tutorial.objective }}</p>
      </div>
      <AppButton
        v-bind="automation('close')"
        text="Close tutorial"
        :icon="closeIcon"
        @click="emit(EVENTS.CLOSE)"
      />
    </header>
    <ol>
      <li v-for="step in tutorial.guidance" :key="step.title">
        <h3>{{ step.title }}</h3>
        <p>{{ step.instruction }}</p>
        <p><strong>Expected:</strong> {{ step.observation }}</p>
      </li>
    </ol>
    <div class="actions">
      <AppButton
        v-bind="automation('open-example')"
        text="Open disposable example"
        :icon="openIcon"
        @click="emit(EVENTS.OPEN_TUTORIAL, tutorial)"
      />
      <AppButton
        v-bind="automation('copy-example')"
        text="Copy to my flows"
        :icon="copyIcon"
        @click="emit(EVENTS.COPY_TUTORIAL, tutorial)"
      />
    </div>
  </section>
</template>

<script setup lang="ts">
import closeIcon from '@/assets/icons/cancel-icon.svg';
import copyIcon from '@/assets/icons/copy-icon.svg';
import openIcon from '@/assets/icons/flow-design-icon.svg';
import AppButton from '@/components/AppButton.vue';
import { useAutomation } from '@/composables/useAutomation';
import { EVENTS } from '@/constants/events';
import type { FlowTutorial } from '@/features/flows/tutorialCatalogue';

const props = defineProps<{ automation: string; tutorial: FlowTutorial }>();
const emit = defineEmits<{
  (event: typeof EVENTS.CLOSE): void;
  (event: typeof EVENTS.OPEN_TUTORIAL | typeof EVENTS.COPY_TUTORIAL, tutorial: FlowTutorial): void;
}>();
const automation = useAutomation(props.automation);
</script>

<style scoped>
.tutorial {
  padding: var(--space-4);
  border: var(--border-width-default) solid var(--color-border-subtle);
  border-radius: var(--radius-lg);
  background: var(--color-surface-subtle);
}
header,
.actions {
  display: flex;
  flex-wrap: wrap;
  gap: var(--space-3);
  align-items: flex-start;
  justify-content: space-between;
}
.category {
  color: var(--color-text-subtle);
  text-transform: uppercase;
}
li + li {
  margin-top: var(--space-3);
}
</style>
