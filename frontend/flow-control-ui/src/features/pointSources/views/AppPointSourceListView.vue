<template>
  <section class="configuration-page">
    <AppErrorNotice
      id="point-sources-error-notice"
      :message="error"
      retryable
      @[EVENTS.RETRY]="load"
    />

    <div class="page-heading">
      <div>
        <p>External systems</p>
        <h1>Point sources</h1>
        <p>Define reusable, read-only connections before mapping points.</p>
      </div>
      <RouterLink class="primary-link" :to="{ name: 'point-source-new' }">
        <AppSvg :src="newIcon" size="1em" />
        New source
      </RouterLink>
    </div>

    <AppListView
      v-if="!error"
      id="point-source-list"
      title="Configured point sources"
      :columns="columns"
      :rows="rows"
      :query="query"
      :total-items="filteredSources.length"
      :loading="loading"
      :page-size-options="[10, 25, 50]"
      empty-message="No point sources found."
      @query-change="query = $event"
    >
      <template #cell-name="{ row }">
        <RouterLink :to="{ name: 'point-source-detail', params: { sourceId: row.id } }">
          {{ row.name }}
        </RouterLink>
        <small v-if="row.description">{{ row.description }}</small>
      </template>

      <template #cell-status="{ row }">
        <span class="status" :class="{ enabled: row.enabled }">{{ row.status }}</span>
      </template>

      <template #cell-updatedAt="{ row }">
        <time v-if="row.updatedAt" :datetime="row.updatedAt">{{ formatDate(row.updatedAt) }}</time>
        <span v-else>—</span>
      </template>
    </AppListView>
  </section>
</template>

<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref } from 'vue';

import newIcon from '@/assets/icons/new-icon.svg';
import AppErrorNotice from '@/components/AppErrorNotice.vue';
import AppListView from '@/components/list-view/AppListView.vue';
import AppSvg from '@/components/AppSvg.vue';
import { EVENTS } from '@/constants/events';
import {
  pointSourceApi,
  type PointSourceKind,
  type PointSourceSummary
} from '@/features/pointSources/api/pointSourceApi';
import type { ListColumn, ListQuery, ListRow } from '@/models';

interface PointSourceRow extends ListRow {
  id: string;
  name: string;
  description: string;
  kind: string;
  status: string;
  enabled: boolean;
  updatedAt: string;
}

const columns: ListColumn<PointSourceRow>[] = [
  { key: 'name', label: 'Name', sortable: true },
  { key: 'kind', label: 'Kind', sortable: true, width: '12rem' },
  { key: 'status', label: 'Status', sortable: true, width: '10rem' },
  { key: 'updatedAt', label: 'Updated', sortable: true, width: '14rem' }
];

const sources = ref<PointSourceSummary[]>([]);
const loading = ref(false);
const error = ref('');
const query = ref<ListQuery<PointSourceRow>>({
  page: 1,
  pageSize: 25,
  filter: '',
  sort: { column: 'name', direction: 'asc' }
});

let controller: AbortController | undefined;

const kindLabel = (kind: PointSourceKind): string =>
  ({ homeAssistant: 'Home Assistant', mqtt: 'MQTT', httpJson: 'HTTP/JSON' })[kind];

const allRows = computed<PointSourceRow[]>(() =>
  sources.value.map((source) => ({
    id: source.id,
    name: source.name,
    description: source.description ?? '',
    kind: kindLabel(source.kind),
    status: source.enabled ? 'Enabled' : 'Disabled',
    enabled: source.enabled,
    updatedAt: source.updatedAt
  }))
);

const filteredSources = computed(() => {
  const filter = query.value.filter.trim().toLocaleLowerCase();
  if (!filter) return allRows.value;
  return allRows.value.filter((source) => source.name.toLocaleLowerCase().includes(filter));
});

const sortedSources = computed(() => {
  const sort = query.value.sort;
  if (!sort) return filteredSources.value;

  const direction = sort.direction === 'asc' ? 1 : -1;
  return [...filteredSources.value].sort(
    (left, right) => String(left[sort.column]).localeCompare(String(right[sort.column])) * direction
  );
});

const rows = computed(() => {
  const start = (query.value.page - 1) * query.value.pageSize;
  return sortedSources.value.slice(start, start + query.value.pageSize);
});

const formatDate = (value: string): string =>
  new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'short' }).format(
    new Date(value)
  );

const load = async (): Promise<void> => {
  controller?.abort();
  controller = new AbortController();
  loading.value = true;
  error.value = '';

  try {
    sources.value = (await pointSourceApi.list(controller.signal)).items;
  } catch (reason) {
    if (!controller.signal.aborted) {
      error.value = reason instanceof Error ? reason.message : 'Unable to load point sources';
    }
  } finally {
    loading.value = false;
  }
};

onMounted(() => void load());
onBeforeUnmount(() => controller?.abort());
</script>

<style scoped lang="css">
small {
  display: block;
  margin-top: var(--space-1-5);
  color: var(--color-text-secondary);
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

.status.enabled {
  color: var(--color-action-primary-strong);
  background: var(--color-action-primary-surface);
}
</style>
