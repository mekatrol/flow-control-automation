<template>
  <AppTable automation="flows-table" caption="Flows">
    <template #head>
      <tr>
        <th scope="col" :aria-sort="sortDirection">
          <TableSortButton
            automation="flows-name-sort"
            label="Name"
            :direction="sortDirection"
            @toggle="$emit('toggleSort')"
          />
        </th>
        <th scope="col">Status</th>
        <th scope="col">Nodes</th>
        <th scope="col">Updated</th>
        <th scope="col">Actions</th>
      </tr>
    </template>
    <template #body>
      <FlowTableRow
        v-for="flow in flows"
        :key="flow.id"
        :flow="flow"
        :editing="editingFlowId === flow.id"
        :rename-value="renameValue"
        :renaming="renaming"
        :confirming-delete="confirmingDeleteId === flow.id"
        :deleting="deleting"
        :toggling-disabled="togglingDisabledId === flow.id"
        @begin-rename="(...args) => $emit('beginRename', ...args)"
        @update:rename-value="$emit('update:renameValue', $event)"
        @save-rename="$emit('saveRename', $event)"
        @cancel-rename="$emit('cancelRename')"
        @begin-delete="$emit('beginDelete', $event)"
        @confirm-delete="$emit('confirmDelete', $event)"
        @cancel-delete="$emit('cancelDelete')"
        @toggle-disabled="(...args) => $emit('toggleDisabled', ...args)"
      />
    </template>
  </AppTable>
</template>

<script setup lang="ts">
import AppTable from '@/components/AppTable.vue';
import TableSortButton from '@/components/TableSortButton.vue';
import type { SortDirection } from '@/composables/usePaginatedCollection';
import FlowTableRow from '@/features/flows/components/FlowTableRow.vue';
import type { FlowDefinition } from '@/features/flows/types';

defineProps<{
  flows: FlowDefinition[];
  sortDirection: SortDirection;
  editingFlowId?: string;
  renameValue: string;
  renaming: boolean;
  confirmingDeleteId?: string;
  deleting: boolean;
  togglingDisabledId?: string;
}>();

defineEmits<{
  toggleSort: [];
  beginRename: [flowId: string, name: string];
  'update:renameValue': [value: string];
  saveRename: [flowId: string];
  cancelRename: [];
  beginDelete: [flowId: string];
  confirmDelete: [flowId: string];
  cancelDelete: [];
  toggleDisabled: [flowId: string, disabled: boolean];
}>();
</script>
