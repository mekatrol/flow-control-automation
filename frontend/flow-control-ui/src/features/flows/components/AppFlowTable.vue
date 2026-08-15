<template>
  <AppTable v-bind="automation()" caption="Flows">
    <template #head>
      <tr>
        <th scope="col" :aria-sort="sortDirection">
          <AppTableSortButton
            v-bind="automation('name-sort')"
            label="Name"
            :direction="sortDirection"
            @[EVENTS.TOGGLE]="$emit(EVENTS.TOGGLE_SORT)"
          />
        </th>
        <th scope="col">Status</th>
        <th scope="col">Nodes</th>
        <th scope="col">Updated</th>
        <th scope="col">Actions</th>
      </tr>
    </template>
    <template #body>
      <AppFlowTableRow
        v-for="flow in flows"
        :key="flow.id"
        v-bind="automation(`row-${flow.id}`)"
        :flow="flow"
        :editing="editingFlowId === flow.id"
        :rename-value="renameValue"
        :renaming="renaming"
        :confirming-delete="confirmingDeleteId === flow.id"
        :deleting="deleting"
        :toggling-disabled="togglingDisabledId === flow.id"
        @[EVENTS.BEGIN_RENAME]="forwardBeginRename"
        @[EVENTS.UPDATE_RENAME_VALUE]="forwardRenameValue"
        @[EVENTS.SAVE_RENAME]="forwardSaveRename"
        @[EVENTS.CANCEL_RENAME]="forwardCancelRename"
        @[EVENTS.BEGIN_DELETE]="forwardBeginDelete"
        @[EVENTS.CONFIRM_DELETE]="forwardConfirmDelete"
        @[EVENTS.CANCEL_DELETE]="forwardCancelDelete"
        @[EVENTS.TOGGLE_DISABLED]="forwardToggleDisabled"
      />
    </template>
  </AppTable>
</template>

<script setup lang="ts">
import AppTable from '@/components/AppTable.vue';
import AppTableSortButton from '@/components/AppTableSortButton.vue';
import { useAutomation } from '@/composables/useAutomation';
import { EVENTS } from '@/constants/events';
import type { SortDirection } from '@/composables/usePaginatedCollection';
import AppFlowTableRow from '@/features/flows/components/AppFlowTableRow.vue';
import type { FlowDefinition } from '@/features/flows/types';

const props = defineProps<{
  automation: string;
  flows: FlowDefinition[];
  sortDirection: SortDirection;
  editingFlowId?: string;
  renameValue: string;
  renaming: boolean;
  confirmingDeleteId?: string;
  deleting: boolean;
  togglingDisabledId?: string;
}>();

const automation = useAutomation(props.automation);
const emit = defineEmits<{
  (event: typeof EVENTS.TOGGLE_SORT): void;
  (event: typeof EVENTS.BEGIN_RENAME, flowId: string, name: string): void;
  (event: typeof EVENTS.UPDATE_RENAME_VALUE, value: string): void;
  (event: typeof EVENTS.SAVE_RENAME, flowId: string): void;
  (event: typeof EVENTS.CANCEL_RENAME): void;
  (event: typeof EVENTS.BEGIN_DELETE, flowId: string): void;
  (event: typeof EVENTS.CONFIRM_DELETE, flowId: string): void;
  (event: typeof EVENTS.CANCEL_DELETE): void;
  (event: typeof EVENTS.TOGGLE_DISABLED, flowId: string, disabled: boolean): void;
}>();

const forwardBeginRename = (flowId: string, name: string): void =>
  emit(EVENTS.BEGIN_RENAME, flowId, name);
const forwardRenameValue = (value: string): void => emit(EVENTS.UPDATE_RENAME_VALUE, value);
const forwardSaveRename = (flowId: string): void => emit(EVENTS.SAVE_RENAME, flowId);
const forwardCancelRename = (): void => emit(EVENTS.CANCEL_RENAME);
const forwardBeginDelete = (flowId: string): void => emit(EVENTS.BEGIN_DELETE, flowId);
const forwardConfirmDelete = (flowId: string): void => emit(EVENTS.CONFIRM_DELETE, flowId);
const forwardCancelDelete = (): void => emit(EVENTS.CANCEL_DELETE);
const forwardToggleDisabled = (flowId: string, disabled: boolean): void =>
  emit(EVENTS.TOGGLE_DISABLED, flowId, disabled);
</script>
