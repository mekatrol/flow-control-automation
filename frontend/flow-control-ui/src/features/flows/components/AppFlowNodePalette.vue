<template>
  <aside class="node-palette" aria-label="Function block toolbox">
    <h2>Function blocks</h2>
    <form role="search" @submit.prevent="applyFilter">
      <label>
        <span>Find a node</span>
        <input v-model="filter" type="search" placeholder="Search nodes" />
      </label>
      <AppButton
        automation="flow-node-palette-apply-filter"
        type="submit"
        text="Apply filter"
        :icon="filterIcon"
      />
    </form>
    <div v-if="Object.keys(groups).length" class="palette-groups">
      <section v-for="(definitions, category) in groups" :key="category">
        <h3>{{ category }}</h3>
        <AppButton
          v-for="definition in definitions"
          :key="definition.kind"
          v-bind="automation(`add-${definition.kind}`)"
          :text="definition.label"
          draggable="true"
          :aria-label="`Add ${definition.label} node`"
          @click="emit('add', definition.kind)"
          @dragstart="startPaletteDrag(definition.kind, $event)"
        >
          <template #icon>
            <AppSvg
              :src="getNodeIconUrl(definition.icon)"
              :automation="`flow-node-palette-${definition.kind}-icon`"
              size="100%"
            />
          </template>
        </AppButton>
      </section>
    </div>
    <p v-else>No node kinds match “{{ query }}”.</p>
  </aside>
</template>

<script lang="ts">
import {
  flowNodeKinds,
  getNodeIconUrl,
  getNodeKind,
  type NodeKindDefinition
} from '@/features/flows/nodeKinds';

export const filterNodeKinds = (query: string): NodeKindDefinition[] => {
  const search = query.trim().toLocaleLowerCase();
  // Search the registry rather than rendered labels so filtering remains a pure,
  // testable operation and category names are searchable as well as node names.
  return flowNodeKinds
    .map(getNodeKind)
    .filter(
      (definition) =>
        !search ||
        definition.label.toLocaleLowerCase().includes(search) ||
        definition.category.includes(search)
    );
};

export const groupNodeKinds = (
  definitions: NodeKindDefinition[]
): Partial<Record<NodeKindDefinition['category'], NodeKindDefinition[]>> => {
  // Build groups from the filtered result so empty categories disappear instead
  // of leaving headings with no actions beneath them.
  const groups: Partial<Record<NodeKindDefinition['category'], NodeKindDefinition[]>> = {};
  for (const definition of definitions) {
    (groups[definition.category] ??= []).push(definition);
  }
  return groups;
};
</script>

<script setup lang="ts">
import { computed, ref } from 'vue';

import filterIcon from '@/assets/icons/filter-icon.svg';
import AppButton from '@/components/AppButton.vue';
import AppSvg from '@/components/AppSvg.vue';
import { useAutomation } from '@/composables/useAutomation';
import type { FlowNodeKind } from '@/features/flows/types';

const emit = defineEmits<{ add: [kind: FlowNodeKind] }>();
const automation = useAutomation('flow-node-palette');
const filter = ref('');
const query = ref('');
const groups = computed(() => groupNodeKinds(filterNodeKinds(query.value)));
const applyFilter = (): void => {
  query.value = filter.value;
};

const startPaletteDrag = (kind: FlowNodeKind, event: DragEvent): void => {
  event.dataTransfer?.setData('application/x-flow-node-function-type', kind);
  if (event.dataTransfer) event.dataTransfer.effectAllowed = 'copy';
};
</script>

<style scoped>
.node-palette {
  width: 220px;
  min-width: 220px;
  padding: var(--space-6-5);
  overflow-y: auto;
  overscroll-behavior-y: contain;
  background: var(--color-surface-subtle);
  border-right: var(--border-width-default) solid var(--color-border-subtle);
  scrollbar-gutter: stable;
}

.node-palette > h2 {
  margin: var(--space-0) var(--space-0) var(--space-5-5);
  color: var(--color-palette-heading);
  font-size: var(--font-size-xl);
}

form,
label {
  display: grid;
  gap: var(--space-4-5);
}

label {
  align-items: center;
  color: var(--color-text-secondary);
  font-size: var(--font-size-sm);
  font-weight: var(--font-weight-bold);
}

input {
  width: 100%;
  padding: var(--space-3) var(--space-4);
  border: var(--border-width-default) solid var(--color-border-default);
  border-radius: var(--radius-sm);
}

.palette-groups {
  display: grid;
  gap: var(--space-6-5);
  margin-top: var(--space-4-5);
}

section {
  display: grid;
  gap: var(--space-2);
  align-items: center;
}

h3 {
  margin: var(--space-0) var(--space-0) var(--space-1);
  color: var(--color-text-subtle);
  font-size: var(--font-size-2xs);
  letter-spacing: 0.08em;
  text-transform: uppercase;
}

p {
  margin: var(--space-4-5) var(--space-0) var(--space-0);
  color: var(--color-text-subtle);
  font-size: var(--font-size-sm);
}
</style>
