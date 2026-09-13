<template>
  <section class="list-page" aria-labelledby="points-heading">
    <AppErrorNotice
      id="points-error-notice"
      :message="errorMessage"
      retryable
      retry-label="Check again"
      @[EVENTS.RETRY]="refresh"
    />

    <AppListView
      v-if="!store.error"
      id="points-list"
      title="Configured points"
      :columns="columns"
      :rows="rows"
      :query="query"
      :total-items="store.result.totalItems"
      :loading="isSpinnerVisible"
      empty-message="No points found."
      @query-change="updateQuery"
    >
      <template #header>
        <div class="list-header">
          <h2 class="list-heading">Points</h2>
        </div>
      </template>

      <template #cell-name="{ row }">
        <RouterLink
          v-if="row.sourceId"
          :to="{ name: 'point-source-detail', params: { sourceId: row.sourceId } }"
        >
          {{ row.name }}
        </RouterLink>
        <span v-else>{{ row.name }}</span>
        <small>{{ row.description || row.id }}</small>
      </template>

      <template #cell-valueType="{ row }">
        {{ row.valueType }}
        <small v-if="row.units">{{ row.units }}</small>
      </template>

      <template #cell-status="{ row }">
        <span class="status" :class="{ enabled: row.enabled }">{{ row.status }}</span>
      </template>
    </AppListView>
  </section>
</template>

<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref } from 'vue';

import AppErrorNotice from '@/components/AppErrorNotice.vue';
import AppListView from '@/components/list-view/AppListView.vue';
import { EVENTS } from '@/constants/events';
import type { PointSummary } from '@/features/points/api/pointDto';
import { usePointsStore } from '@/features/points/stores/points';
import { useSpinner } from '@/composables/useSpinner';
import type { ListColumn, ListQuery, ListRow } from '@/models';

interface PointRow extends ListRow {
  id: string;
  name: string;
  description: string;
  source: string;
  sourceId: string;
  mapping: string;
  direction: string;
  valueType: string;
  units: string;
  capabilities: string;
  status: string;
  enabled: boolean;
}

const columns: ListColumn<PointRow>[] = [
  { key: 'name', label: 'Name', sortable: true },
  { key: 'source', label: 'Source', width: '12rem' },
  { key: 'mapping', label: 'Mapping', width: '14rem' },
  { key: 'direction', label: 'Direction', width: '9rem' },
  { key: 'valueType', label: 'Value type', width: '10rem' },
  { key: 'capabilities', label: 'Capabilities', width: '12rem' },
  { key: 'status', label: 'Status', width: '9rem' }
];

const { isSpinnerVisible } = useSpinner();

const store = usePointsStore();
const query = ref<ListQuery<PointRow>>({
  page: 1,
  pageSize: 10,
  filter: '',
  sort: { column: 'name', direction: 'asc' }
});

const errorMessage = computed(() =>
  store.errorStatus === 404
    ? `${store.error} This backend does not support the points API. Check the deployed backend version and try again.`
    : store.error
);
const label = (value: string): string =>
  value.replaceAll('_', ' ').replace(/^\w/, (first) => first.toUpperCase());
const capabilities = (readable: boolean, commandable: boolean): string =>
  [readable && 'Read', commandable && 'Command'].filter(Boolean).join(', ') || 'None';

const toRow = (point: PointSummary): PointRow => ({
  id: point.id,
  name: point.name,
  description: point.description ?? '',
  source: point.sourceName ?? label(point.sourceKind),
  sourceId: point.sourceId ?? '',
  mapping: point.mapping,
  direction: label(point.direction),
  valueType: label(point.valueType),
  units: point.units ?? '',
  capabilities: capabilities(point.readable, point.commandable),
  status: point.enabled ? 'Enabled' : 'Disabled',
  enabled: point.enabled
});

const rows = computed(() => store.result.items.map(toRow));

const refresh = (): Promise<void> =>
  store.load({
    filter: query.value.filter,
    page: query.value.page,
    pageSize: query.value.pageSize,
    sort: query.value.sort?.direction === 'desc' ? 'descending' : 'ascending'
  });

const updateQuery = (nextQuery: ListQuery<PointRow>): void => {
  query.value = nextQuery;
  void refresh();
};

onMounted(() => void refresh());
onBeforeUnmount(store.cancel);
</script>

<style scoped lang="css">
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

.status.enabled {
  color: var(--color-action-primary-strong);
  background: var(--color-action-primary-surface);
}
</style>
