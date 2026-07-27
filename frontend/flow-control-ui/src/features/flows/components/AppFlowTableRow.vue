<template>
  <tr class="flow-row" @click="openFlow">
    <td>
      <form
        v-if="editing"
        class="rename-flow"
        @click.stop
        @submit.prevent="$emit(EVENTS.SAVE_RENAME, flow.id)"
      >
        <label :for="`rename-${flow.id}`">Rename {{ flow.name }}</label>
        <input
          :id="`rename-${flow.id}`"
          :value="renameValue"
          type="text"
          @input="$emit(EVENTS.UPDATE_RENAME_VALUE, ($event.target as HTMLInputElement).value)"
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
          @click="$emit(EVENTS.CANCEL_RENAME)"
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
        @click="$emit(EVENTS.TOGGLE_DISABLED, flow.id, !flow.disabled)"
      />
      <AppButton
        v-bind="automation('rename')"
        text="Rename"
        :icon="renameFlowIcon"
        @click="$emit(EVENTS.BEGIN_RENAME, flow.id, flow.name)"
      />
      <AppButton
        v-bind="automation('delete')"
        text="Delete"
        :icon="deleteFlowIcon"
        @click="$emit(EVENTS.BEGIN_DELETE, flow.id)"
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
          @click="$emit(EVENTS.CONFIRM_DELETE, flow.id)"
        />
        <AppButton
          v-bind="automation('cancel-delete')"
          text="Cancel"
          :icon="cancelIcon"
          data-dialog-initial-focus
          @click="$emit(EVENTS.CANCEL_DELETE)"
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
import { EVENTS } from '@/constants/events';
import { useAutomation } from '@/composables/useAutomation';
import { useModalFocus } from '@/features/flows/composables/useModalFocus';
import type { FlowDefinition } from '@/features/flows/types';

const props = defineProps<{
  automation: string;
  flow: FlowDefinition;
  editing: boolean;
  renameValue: string;
  renaming: boolean;
  confirmingDelete: boolean;
  deleting: boolean;
  togglingDisabled: boolean;
}>();

const emit = defineEmits<{
  (event: typeof EVENTS.BEGIN_RENAME, flowId: string, name: string): void;
  (event: typeof EVENTS.UPDATE_RENAME_VALUE, value: string): void;
  (event: typeof EVENTS.SAVE_RENAME, flowId: string): void;
  (event: typeof EVENTS.CANCEL_RENAME): void;
  (event: typeof EVENTS.BEGIN_DELETE, flowId: string): void;
  (event: typeof EVENTS.CONFIRM_DELETE, flowId: string): void;
  (event: typeof EVENTS.CANCEL_DELETE): void;
  (event: typeof EVENTS.TOGGLE_DISABLED, flowId: string, disabled: boolean): void;
}>();

const router = useRouter();
const automation = useAutomation(props.automation);
const deleteDialog = ref<HTMLElement>();
const deleteDialogOpen = computed(() => props.confirmingDelete);
const setDeleteDialog = (element: Element | ComponentPublicInstance | null): void => {
  deleteDialog.value = element instanceof HTMLElement ? element : undefined;
};
const { handleKeydown: handleDeleteDialogKeydown } = useModalFocus(
  deleteDialog,
  deleteDialogOpen,
  () => emit(EVENTS.CANCEL_DELETE)
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
  font-weight: var(--font-weight-strong);
  text-decoration-thickness: 1px;
  text-underline-offset: 3px;
}

.flow-name:hover {
  color: var(--color-action-primary-strong);
}

.description {
  display: block;
  max-width: 410px;
  margin-top: var(--space-1-5);
  color: var(--color-text-muted);
  font-size: var(--font-size-lg);
}

.status {
  display: inline-block;
  padding: var(--space-2) var(--space-3-5);
  color: var(--color-text-secondary);
  font-size: var(--font-size-xs);
  font-weight: var(--font-weight-black);
  letter-spacing: 0.08em;
  background: var(--color-surface-neutral);
  border-radius: var(--radius-pill);
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
  gap: var(--space-3-5);
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
  font-size: var(--font-size-sm);
  font-weight: var(--font-weight-bold);
}

.rename-flow input {
  min-height: 44px;
  padding: var(--space-3-5);
  color: var(--color-text-primary);
  background: var(--color-surface-raised);
  border: var(--border-width-default) solid var(--color-border-default);
  border-radius: var(--radius-md);
}

.delete-confirmation {
  position: absolute;
  z-index: 2;
  top: calc(100% + 6px);
  right: 12px;
  padding: var(--space-5-5);
  color: var(--color-danger-text);
  background: var(--color-surface-raised);
  border: var(--border-width-default) solid var(--color-danger-border-subtle);
  border-radius: var(--radius-lg);
  box-shadow: var(--shadow-menu);
}
</style>
