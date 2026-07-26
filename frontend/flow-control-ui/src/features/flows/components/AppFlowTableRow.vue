<template>
  <tr class="flow-row" @click="openFlow">
    <td>
      <form
        v-if="editing"
        class="rename-flow"
        @click.stop
        @submit.prevent="$emit('saveRename', flow.id)"
      >
        <label :for="`rename-${flow.id}`">Rename {{ flow.name }}</label>
        <input
          :id="`rename-${flow.id}`"
          :value="renameValue"
          type="text"
          @input="$emit('update:renameValue', ($event.target as HTMLInputElement).value)"
        />
        <AppButton
          v-bind="automation('save-name')"
          type="submit"
          text="Save name"
          :icon="saveIcon"
          hide-text
          :disabled="renaming"
        />
        <AppButton
          v-bind="automation('cancel-rename')"
          text="Cancel"
          :icon="cancelIcon"
          hide-text
          @click="$emit('cancelRename')"
        />
      </form>
      <RouterLink
        v-else
        class="flow-name"
        :to="{ name: 'flow-designer', params: { flowId: flow.id } }"
        @click.stop
      >
        {{ flow.name }}
      </RouterLink>
      <span class="description">{{ flow.description }}</span>
    </td>
    <td>
      <span class="status" :class="[flow.status, { disabled: flow.disabled }]">
        {{ flow.disabled ? `${flow.status} · disabled` : flow.status }}
      </span>
    </td>
    <td>{{ flow.nodes.length }}</td>
    <td>
      <time :datetime="flow.updatedAt">{{ formattedUpdatedAt }}</time>
    </td>
    <td class="actions" @click.stop>
      <AppButton
        v-bind="automation('toggle-disabled')"
        :text="flow.disabled ? 'Enable' : 'Disable'"
        :icon="flow.disabled ? enableFlowIcon : disableFlowIcon"
        :disabled="togglingDisabled"
        @click="$emit('toggleDisabled', flow.id, !flow.disabled)"
      />
      <AppButton
        v-bind="automation('rename')"
        text="Rename"
        :icon="renameFlowIcon"
        @click="$emit('beginRename', flow.id, flow.name)"
      />
      <AppButton
        v-bind="automation('delete')"
        text="Delete"
        :icon="deleteFlowIcon"
        @click="$emit('beginDelete', flow.id)"
      />
      <div
        v-if="confirmingDelete"
        :ref="setDeleteDialog"
        class="delete-confirmation"
        role="alertdialog"
        :aria-label="`Delete ${flow.name}?`"
        :aria-describedby="`delete-description-${flow.id}`"
        aria-modal="true"
        tabindex="-1"
        @keydown="handleDeleteDialogKeydown"
      >
        <span :id="`delete-description-${flow.id}`">Delete this flow?</span>
        <AppButton
          v-bind="automation('confirm-delete')"
          text="Confirm delete"
          :icon="deleteFlowIcon"
          :disabled="deleting"
          @click="$emit('confirmDelete', flow.id)"
        />
        <AppButton
          v-bind="automation('cancel-delete')"
          text="Cancel"
          :icon="cancelIcon"
          data-dialog-initial-focus
          @click="$emit('cancelDelete')"
        />
      </div>
    </td>
  </tr>
</template>

<script setup lang="ts">
import { computed, ref, type ComponentPublicInstance } from 'vue';
import { useRouter } from 'vue-router';

import cancelIcon from '@/assets/icons/cancel-icon.svg';
import deleteFlowIcon from '@/assets/icons/delete-flow-icon.svg';
import disableFlowIcon from '@/assets/icons/disable-flow-icon.svg';
import enableFlowIcon from '@/assets/icons/enable-flow-icon.svg';
import renameFlowIcon from '@/assets/icons/rename-flow-icon.svg';
import saveIcon from '@/assets/icons/save-icon.svg';
import AppButton from '@/components/AppButton.vue';
import { useAutomation } from '@/composables/useAutomation';
import { useModalFocus } from '@/features/flows/composables/useModalFocus';
import type { FlowDefinition } from '@/features/flows/types';

const props = defineProps<{
  flow: FlowDefinition;
  editing: boolean;
  renameValue: string;
  renaming: boolean;
  confirmingDelete: boolean;
  deleting: boolean;
  togglingDisabled: boolean;
}>();

const emit = defineEmits<{
  beginRename: [flowId: string, name: string];
  'update:renameValue': [value: string];
  saveRename: [flowId: string];
  cancelRename: [];
  beginDelete: [flowId: string];
  confirmDelete: [flowId: string];
  cancelDelete: [];
  toggleDisabled: [flowId: string, disabled: boolean];
}>();

const router = useRouter();
const automation = useAutomation(`flow-row-${props.flow.id}`);
const deleteDialog = ref<HTMLElement>();
const deleteDialogOpen = computed(() => props.confirmingDelete);
const setDeleteDialog = (element: Element | ComponentPublicInstance | null): void => {
  deleteDialog.value = element instanceof HTMLElement ? element : undefined;
};
const { handleKeydown: handleDeleteDialogKeydown } = useModalFocus(
  deleteDialog,
  deleteDialogOpen,
  () => emit('cancelDelete')
);
const formattedUpdatedAt = computed(() =>
  new Intl.DateTimeFormat(undefined, {
    dateStyle: 'medium',
    timeStyle: 'short'
  }).format(new Date(props.flow.updatedAt))
);

const openFlow = (): void => {
  void router.push({ name: 'flow-designer', params: { flowId: props.flow.id } });
};
</script>

<style scoped>
.flow-row {
  cursor: pointer;
}

.flow-row:hover,
.flow-row:focus-within {
  background: var(--color-action-primary-surface-subtle);
}

.flow-name {
  display: inline-block;
  color: var(--color-text-primary);
  font-weight: 750;
  text-decoration-thickness: 1px;
  text-underline-offset: 3px;
}

.flow-name:hover {
  color: var(--color-action-primary-strong);
}

.description {
  display: block;
  max-width: 410px;
  margin-top: 4px;
  color: var(--color-text-muted);
  font-size: 13px;
}

.status {
  display: inline-block;
  padding: 5px 8px;
  color: var(--color-text-secondary);
  font-size: 10px;
  font-weight: 800;
  letter-spacing: 0.08em;
  background: var(--color-surface-neutral);
  border-radius: 999px;
  text-transform: uppercase;
}

.status.deployed {
  color: var(--color-action-primary-strong);
  background: var(--color-action-primary-surface);
}

.actions,
.rename-flow,
.delete-confirmation {
  display: flex;
  gap: 8px;
  align-items: center;
}

.actions {
  position: relative;
  white-space: nowrap;
}

.rename-flow {
  flex-wrap: wrap;
}

.rename-flow label {
  width: 100%;
  color: var(--color-text-primary);
  font-size: 11px;
  font-weight: 700;
}

.rename-flow input {
  min-height: 44px;
  padding: 8px;
  color: var(--color-text-primary);
  background: var(--color-surface-raised);
  border: 1px solid var(--color-border-default);
  border-radius: 7px;
}

.delete-confirmation {
  position: absolute;
  z-index: 2;
  top: calc(100% + 6px);
  right: 12px;
  padding: 12px;
  color: var(--color-danger-text);
  background: var(--color-surface-raised);
  border: 1px solid var(--color-danger-border-subtle);
  border-radius: 8px;
  box-shadow: 0 10px 30px var(--color-shadow-dialog);
}
</style>
