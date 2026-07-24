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
          type="submit"
          text="Save name"
          :icon="saveIcon"
          hide-text
          :disabled="renaming"
        />
        <AppButton
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
    <td><span class="status" :class="flow.status">{{ flow.status }}</span></td>
    <td>{{ flow.nodes.length }}</td>
    <td>
      <time :datetime="flow.updatedAt">{{ formattedUpdatedAt }}</time>
    </td>
    <td class="actions" @click.stop>
      <AppButton
        text="Rename"
        :icon="renameFlowIcon"
        @click="$emit('beginRename', flow.id, flow.name)"
      />
      <AppButton text="Delete" :icon="deleteFlowIcon" @click="$emit('beginDelete', flow.id)" />
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
          text="Confirm delete"
          :icon="deleteFlowIcon"
          :disabled="deleting"
          @click="$emit('confirmDelete', flow.id)"
        />
        <AppButton
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

import cancelIcon from '@/assets/cancel-icon.svg';
import deleteFlowIcon from '@/assets/delete-flow-icon.svg';
import renameFlowIcon from '@/assets/rename-flow-icon.svg';
import saveIcon from '@/assets/save-icon.svg';
import AppButton from '@/components/AppButton.vue';
import { useModalFocus } from '@/features/flows/composables/useModalFocus';
import type { FlowDefinition } from '@/features/flows/types';

const props = defineProps<{
  flow: FlowDefinition;
  editing: boolean;
  renameValue: string;
  renaming: boolean;
  confirmingDelete: boolean;
  deleting: boolean;
}>();

const emit = defineEmits<{
  beginRename: [flowId: string, name: string];
  'update:renameValue': [value: string];
  saveRename: [flowId: string];
  cancelRename: [];
  beginDelete: [flowId: string];
  confirmDelete: [flowId: string];
  cancelDelete: [];
}>();

const router = useRouter();
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

.actions :deep(button),
.rename-flow :deep(button) {
  min-width: 44px;
  min-height: 44px;
  padding: 8px 10px;
  color: var(--color-text-secondary);
  font-weight: 700;
  background: var(--color-surface-neutral);
  border: 1px solid var(--color-border-default);
  border-radius: 8px;
  cursor: pointer;
}

.actions :deep(button:hover),
.rename-flow :deep(button:hover) {
  color: var(--color-action-primary-strong);
  background: var(--color-action-primary-surface);
  border-color: var(--color-action-primary);
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

.delete-confirmation :deep(button:first-of-type:not(:disabled)) {
  color: var(--color-text-on-strong);
  background: var(--color-danger-strong);
  border-color: var(--color-danger-strong);
}
</style>
