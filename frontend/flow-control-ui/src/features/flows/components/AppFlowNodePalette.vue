<template>
  <aside class="node-palette" aria-label="Function block toolbox">
    <h2>Function blocks</h2>
    <AppFilter layout="stacked" :show-apply="false">
      <label class="app-filter-field">
        <span>Find a function</span>
        <input v-model="filter" type="search" placeholder="Search nodes" />
      </label>
    </AppFilter>
    <div v-if="Object.keys(groups).length" class="palette-groups">
      <section v-for="(definitions, category) in groups" :key="category">
        <h3>{{ category }}</h3>
        <div v-for="definition in definitions" :key="definition.nodeType" class="palette-item">
          <AppButton
            class="palette-add-button"
            :text="definition.label"
            draggable="true"
            :aria-label="`Add ${definition.label} node`"
            @click="emit(EVENTS.ADD, definition.nodeType)"
            @dragstart="startPaletteDrag(definition.nodeType, $event)"
          >
            <template #icon>
              <AppSvg :src="getNodeIconUrl(definition.icon)" size="100%" />
            </template>
          </AppButton>
        </div>
      </section>
    </div>
    <p v-else>No node types match “{{ filter }}”.</p>
  </aside>
</template>

<script lang="ts">
import {
  getNodeIconUrl,
  getNodeTypeDefinition,
  paletteNodeTypes,
  type FlowNodeTypeDefinition
} from '@/features/flows/nodeTypes';

export const filterNodeTypes = (query: string): FlowNodeTypeDefinition[] => {
  const search = query.trim().toLocaleLowerCase();
  // Search the registry rather than rendered labels so filtering remains a pure,
  // testable operation and category names are searchable as well as node names.
  return paletteNodeTypes
    .map(getNodeTypeDefinition)
    .filter(
      (definition) =>
        !search ||
        definition.label.toLocaleLowerCase().includes(search) ||
        definition.category.includes(search)
    );
};

export const groupNodeTypes = (
  definitions: FlowNodeTypeDefinition[]
): Partial<Record<FlowNodeTypeDefinition['category'], FlowNodeTypeDefinition[]>> => {
  // Build groups from the filtered result so empty categories disappear instead
  // of leaving headings with no actions beneath them.
  const categories: FlowNodeTypeDefinition['category'][] = ['io', 'control', 'timing', 'maths'];
  const groups: Partial<Record<FlowNodeTypeDefinition['category'], FlowNodeTypeDefinition[]>> = {};
  for (const category of categories) {
    const categoryDefinitions = definitions
      .filter((definition) => definition.category === category)
      .sort((left, right) => left.label.localeCompare(right.label));
    if (categoryDefinitions.length) {
      groups[category] = categoryDefinitions;
    }
  }
  return groups;
};
</script>

<script setup lang="ts">
import { computed, ref } from 'vue';

import AppButton from '@/components/AppButton.vue';
import AppFilter from '@/components/AppFilter.vue';
import AppSvg from '@/components/AppSvg.vue';
import { EVENTS } from '@/constants/events';
import type { FlowNodeType } from '@/features/flows/types';

const emit = defineEmits<{
  (event: typeof EVENTS.ADD, type: FlowNodeType): void;
}>();
const filter = ref('');
const groups = computed(() => groupNodeTypes(filterNodeTypes(filter.value)));

const startPaletteDrag = (type: FlowNodeType, event: DragEvent): void => {
  event.dataTransfer?.setData('application/x-flow-node-function-type', type);
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
.palette-item {
  display: grid;
  min-width: 0;
}

.palette-add-button {
  width: 100%;
  min-width: 0;
  padding-inline: var(--space-4);
  justify-content: flex-start;
  text-align: left;
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
