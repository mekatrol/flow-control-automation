<template>
  <nav v-if="versionView === 'draft'" class="workspace-modes" aria-label="Flow workspace mode">
    <AppLink
      text="Design"
      :to="{ name: ROUTE_NAMES.flowDesigner, params: { flowId } }"
      :aria-current="workspaceMode === 'design' ? 'page' : undefined"
      :icon="designIcon"
      :disabled="saving || loading"
      @click="emit('save')"
    />
    <AppLink
      text="Simulate"
      :to="{ name: ROUTE_NAMES.flowSimulator, params: { flowId } }"
      :aria-current="workspaceMode === 'simulator' ? 'page' : undefined"
      :icon="simulateIcon"
      :disabled="saving || loading"
      @click="emit('save')"
    />
    <AppLink
      text="Debug"
      :to="{ name: ROUTE_NAMES.flowDebugger, params: { flowId } }"
      :aria-current="workspaceMode === 'debugger' ? 'page' : undefined"
      :icon="debugIcon"
      :disabled="saving || loading"
      @click="emit('save')"
    />
  </nav>
</template>

<script setup lang="ts">
import { ROUTE_NAMES } from '@/router';
import type { WorkspaceMode, VersionView } from '@/features/flows/types/flowDesigner';

import AppLink from '@/components/AppLink.vue';

import designIcon from '@/assets/icons/flow-design-icon.svg';
import simulateIcon from '@/assets/icons/flow-simulate-icon.svg';
import debugIcon from '@/assets/icons/flow-debug-icon.svg';

defineProps<{
  flowId: string;

  workspaceMode: WorkspaceMode;
  versionView: VersionView;

  saving: boolean;
  loading: boolean;
}>();

const emit = defineEmits<{
  save: [];
}>();
</script>

<style lang="css">
.workspace-modes {
  display: flex;
  gap: var(--space-2);
  margin-bottom: var(--space-3);
}

.workspace-modes a {
  min-height: var(--control-min-height);
  padding: var(--space-2) var(--space-4);
  color: var(--color-text-primary);
  background: var(--color-surface-raised);
  border: var(--border-width-default) solid var(--color-border-default);
  border-radius: var(--radius-lg);
  text-decoration: none;
}

.workspace-modes a[aria-current='page'] {
  color: var(--color-action-primary-strong);
  background: var(--color-action-primary-surface);
  border-color: var(--color-action-primary);
}
</style>
