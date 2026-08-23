<template>
  <div v-bind="automation()" class="designer-heading">
    <div>
      <RouterLink :to="{ name: 'flows' }">← All flows</RouterLink>

      <div class="title-row">
        <h1>{{ flow.name }}</h1>
        <span :class="flow.status">{{ flow.status }}</span>
        <span v-if="flow.disabled" class="disabled">disabled</span>
        <span v-if="dirty" class="dirty-state" role="status"> Unsaved changes </span>

        <span
          class="runtime-state"
          role="status"
          :aria-label="`Runtime state: ${runtimeState ?? 'disconnected'}`"
        >
          {{ runtimeState ?? 'disconnected' }}
        </span>
      </div>

      <p>{{ flow.description }}</p>
    </div>

    <div class="heading-actions">
      <AppFlowDebugTargetSelector
        v-if="workspaceMode === 'debugger'"
        v-bind="automation('debug-target')"
        :model-value="debugTargetId"
        :targets="debugTargets"
        :loading="loading"
        :error="controllerTemplatesError"
        @update:model-value="emit('update:debugTargetId', $event)"
      />

      <AppButton
        v-if="versionView === 'draft'"
        v-bind="automation('save')"
        :text="saving ? 'Saving…' : 'Save flow'"
        :icon="saveIcon"
        :disabled="saving"
        @click="emit('save')"
      />

      <AppButton
        v-if="versionView === 'draft'"
        v-bind="automation('compile')"
        :text="compiling ? 'Compiling…' : 'Compile'"
        :icon="compileIcon"
        :disabled="compiling"
        @click="emit('compile')"
      />

      <AppButton
        v-if="versionView === 'draft'"
        v-bind="automation('deploy')"
        :text="deploying ? 'Deploying…' : 'Deploy flow'"
        :icon="deployIcon"
        :disabled="dirty || deploying || !pointReferencesValid"
        @click="emit('deploy')"
      />

      <AppButton
        v-if="flow.status === 'deployed'"
        v-bind="automation('toggle-disabled')"
        :text="
          togglingDisabled
            ? flow.disabled
              ? 'Enabling…'
              : 'Disabling…'
            : flow.disabled
              ? 'Enable'
              : 'Disable'
        "
        :icon="flow.disabled ? enableFlowIcon : disableFlowIcon"
        :disabled="togglingDisabled"
        @click="emit('toggleDisabled', !flow.disabled)"
      />

      <AppButton
        v-bind="automation('refresh-runtime')"
        text="Refresh runtime"
        :icon="refreshIcon"
        @click="emit('refreshRuntime')"
      />
    </div>
  </div>
</template>

<script setup lang="ts">
import type { FlowDefinition } from '@/features/flows/types';
import type { WorkspaceMode, VersionView } from '@/features/flows/types/flowDesigner';
import type { FlowDebugTarget } from '@/features/flows/debugTargets';

import { useAutomation } from '@/composables/useAutomation';

import AppButton from '@/components/AppButton.vue';

import compileIcon from '@/assets/icons/compile-icon.svg';
import disableFlowIcon from '@/assets/icons/disable-flow-icon.svg';
import enableFlowIcon from '@/assets/icons/enable-flow-icon.svg';
import refreshIcon from '@/assets/icons/refresh-icon.svg';
import saveIcon from '@/assets/icons/save-icon.svg';
import deployIcon from '@/assets/icons/deploy-icon.svg';

import AppFlowDebugTargetSelector from '@/features/flows/components/AppFlowDebugTargetSelector.vue';

const props = defineProps<{
  flow: FlowDefinition;
  dirty: boolean;
  runtimeState?: string;

  workspaceMode: WorkspaceMode;
  versionView: VersionView;

  debugTargetId: string;
  debugTargets: FlowDebugTarget[];
  controllerTemplatesError: string;

  saving: boolean;
  compiling: boolean;
  deploying: boolean;
  loading: boolean;
  togglingDisabled: boolean;

  pointReferencesValid: boolean;

  automation: string;
}>();

const automation = useAutomation(props.automation);

const emit = defineEmits<{
  save: [];
  compile: [];
  deploy: [];
  toggleDisabled: [disabled: boolean];
  refreshRuntime: [];

  /**
   * AppFlowDesignerHeader does not own the selected debug target.
   *
   * The source of truth remains in AppFlowDesignerView. When the user
   * selects another target, this event propagates that selection back
   * to the parent.
   */
  'update:debugTargetId': [value: string];
}>();
</script>
