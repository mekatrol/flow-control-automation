<template>
  <section v-bind="automation()" class="compile-results" aria-label="Error List">
    <header>
      <h2>Error List</h2>
      <span>{{ errorCount }} {{ errorCount === 1 ? 'Error' : 'Errors' }}</span>
      <span>0 Warnings</span>
      <span v-if="result?.success" class="compile-success">Build succeeded</span>
    </header>
    <table v-if="result?.diagnostics.length">
      <thead>
        <tr>
          <th>Severity</th>
          <th>Code</th>
          <th>Description</th>
          <th>Path</th>
        </tr>
      </thead>
      <tbody>
        <tr
          v-for="(diagnostic, index) in result.diagnostics"
          :key="`${diagnostic.displayCode}-${diagnostic.path}-${index}`"
          :class="{ actionable: Boolean(nodeIdFor(diagnostic.path)) }"
          tabindex="0"
          @click="selectDiagnostic(diagnostic.path)"
          @keydown.enter="selectDiagnostic(diagnostic.path)"
        >
          <td class="severity">Error</td>
          <td>{{ diagnostic.displayCode }}</td>
          <td>{{ diagnostic.message }}</td>
          <td>{{ diagnostic.path || 'Flow' }}</td>
        </tr>
      </tbody>
    </table>
    <p v-else-if="result?.success">
      Draft compiled successfully · {{ result.instructionCount ?? 0 }} instructions ·
      {{ result.pointCount ?? 0 }} points
    </p>
    <p v-else>No compilation results.</p>
  </section>
</template>

<script setup lang="ts">
import { computed } from 'vue';
import { useAutomation } from '@/composables/useAutomation';
import type { FlowCompileResult } from '@/features/flows/api/flowCompileApi';

const props = defineProps<{ automation: string; result?: FlowCompileResult; nodeIds: string[] }>();
const emit = defineEmits<{ (event: 'selectDiagnostic', nodeId: string): void }>();
const automation = useAutomation(props.automation);
const errorCount = computed(() => props.result?.diagnostics.length ?? 0);
const nodeIdFor = (path: string): string | undefined => {
  const match = /^\/nodes\/(\d+)(?:\/|$)/.exec(path);
  return match ? props.nodeIds[Number(match[1])] : undefined;
};
const selectDiagnostic = (path: string): void => {
  const nodeId = nodeIdFor(path);
  if (nodeId) emit('selectDiagnostic', nodeId);
};
</script>

<style scoped>
.compile-results {
  margin-block: var(--space-4);
  overflow: hidden;
  background: var(--color-surface-raised);
  border: var(--border-width-default) solid var(--color-border-default);
  border-radius: var(--radius-lg);
}
header {
  display: flex;
  align-items: center;
  gap: var(--space-5);
  padding: var(--space-3) var(--space-4);
  border-bottom: var(--border-width-default) solid var(--color-border-subtle);
}
h2 {
  margin: 0;
  font-size: var(--font-size-lg);
}
.compile-success {
  color: var(--color-info-text);
}
table {
  width: 100%;
  border-collapse: collapse;
}
th,
td {
  padding: var(--space-2) var(--space-4);
  text-align: left;
  border-bottom: var(--border-width-default) solid var(--color-border-subtle);
}
.severity {
  color: var(--color-danger-strong);
  font-weight: var(--font-weight-semibold);
}
.actionable {
  cursor: pointer;
}
.actionable:hover {
  background: var(--color-action-primary-surface);
}
p {
  margin: 0;
  padding: var(--space-4);
}
</style>
