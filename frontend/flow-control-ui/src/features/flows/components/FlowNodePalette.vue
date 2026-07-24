<template>
  <aside class="node-palette" aria-label="Function block toolbox">
    <h2>Function blocks</h2>
    <label>
      <span>Find a node</span>
      <input v-model="query" type="search" placeholder="Search nodes" />
    </label>
    <div v-if="Object.keys(groups).length" class="palette-groups">
      <section v-for="(definitions, category) in groups" :key="category">
        <h3>{{ category }}</h3>
        <button
          v-for="definition in definitions"
          :key="definition.kind"
          type="button"
          draggable="true"
          @click="emit('add', definition.kind)"
          @dragstart="startPaletteDrag(definition.kind, $event)"
        >
          <img :src="getNodeIconUrl(definition.icon)" alt="" />
          Add {{ definition.label }} node
        </button>
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

import type { FlowNodeKind } from '@/features/flows/types';

const emit = defineEmits<{ add: [kind: FlowNodeKind] }>();
const query = ref('');
const groups = computed(() => groupNodeKinds(filterNodeKinds(query.value)));

const startPaletteDrag = (kind: FlowNodeKind, event: DragEvent): void => {
  event.dataTransfer?.setData('application/x-flow-node-function-type', kind);
  if (event.dataTransfer) event.dataTransfer.effectAllowed = 'copy';
};
</script>

<style scoped>
.node-palette {
  width: 220px;
  min-width: 220px;
  padding: 14px;
  overflow-y: auto;
  overscroll-behavior-y: contain;
  background: var(--color-surface-subtle);
  border-right: 1px solid var(--color-border-subtle);
  scrollbar-gutter: stable;
}

.node-palette > h2 {
  margin: 0 0 12px;
  color: var(--color-palette-heading);
  font-size: 14px;
}

label {
  display: grid;
  gap: 10px;
  align-items: center;
  color: var(--color-text-secondary);
  font-size: 11px;
  font-weight: 700;
}

input {
  width: 100%;
  padding: 7px 9px;
  border: 1px solid var(--color-border-default);
  border-radius: 6px;
}

.palette-groups {
  display: grid;
  gap: 14px;
  margin-top: 10px;
}

section {
  display: grid;
  gap: 5px;
  align-items: center;
}

h3 {
  margin: 0 0 3px;
  color: var(--color-text-subtle);
  font-size: 9px;
  letter-spacing: 0.08em;
  text-transform: uppercase;
}

button {
  width: 100%;
  padding: 6px 8px;
  color: var(--color-palette-heading);
  font-size: 10px;
  background: var(--color-surface-raised);
  border: 1px solid var(--color-border-default);
  border-radius: 6px;
  cursor: pointer;
  white-space: nowrap;
  text-align: left;
  touch-action: none;
}

button img {
  width: 18px;
  height: 18px;
  margin-right: 6px;
  /*
   * Palette buttons have a light background. Normalize both black-authored and
   * white-authored SVG assets to black so every block's icon remains visible;
   * canvas nodes apply their own light/dark theme treatment separately.
   */
  filter: brightness(0);
  vertical-align: middle;
}

p {
  margin: 10px 0 0;
  color: var(--color-text-subtle);
  font-size: 11px;
}
</style>
