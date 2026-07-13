<script lang="ts">
import { flowNodeKinds, getNodeKind, type NodeKindDefinition } from '../nodeKinds';

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

import type { FlowNodeKind } from '../types';

const emit = defineEmits<{ add: [kind: FlowNodeKind] }>();
const query = ref('');
const groups = computed(() => groupNodeKinds(filterNodeKinds(query.value)));
</script>

<template>
  <aside class="node-palette" aria-label="Node palette">
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
          @click="emit('add', definition.kind)"
        >
          <span aria-hidden="true">{{ definition.icon }}</span>
          Add {{ definition.label }} node
        </button>
      </section>
    </div>
    <p v-else>No node kinds match “{{ query }}”.</p>
  </aside>
</template>

<style scoped>
.node-palette {
  padding: 12px 16px;
  background: #f8fbfd;
  border-bottom: 1px solid #d8e2ea;
}

label {
  display: flex;
  gap: 10px;
  align-items: center;
  color: #34495b;
  font-size: 11px;
  font-weight: 700;
}

input {
  width: min(260px, 100%);
  padding: 7px 9px;
  border: 1px solid #cbd8e2;
  border-radius: 6px;
}

.palette-groups {
  display: flex;
  gap: 18px;
  margin-top: 10px;
  overflow-x: auto;
}

section {
  display: flex;
  gap: 5px;
  align-items: center;
}

h3 {
  margin: 0 3px 0 0;
  color: #718394;
  font-size: 9px;
  letter-spacing: 0.08em;
  text-transform: uppercase;
}

button {
  padding: 6px 8px;
  color: #244052;
  font-size: 10px;
  background: #fff;
  border: 1px solid #cbd8e2;
  border-radius: 6px;
  cursor: pointer;
  white-space: nowrap;
}

p {
  margin: 10px 0 0;
  color: #718394;
  font-size: 11px;
}
</style>
