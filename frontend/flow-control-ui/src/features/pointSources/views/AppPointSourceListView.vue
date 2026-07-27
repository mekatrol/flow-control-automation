<template>
  <section class="configuration-page">
    <div class="page-heading">
      <div>
        <p>External systems</p>
        <h1>Point sources</h1>
        <p>Define reusable, read-only connections before mapping points.</p>
      </div>
      <RouterLink class="primary-link" :to="{ name: 'point-source-new' }">
        <AppSvg :src="newIcon" automation="point-sources-new-icon" size="1em" />
        New source
      </RouterLink>
    </div>

    <p v-if="loading" role="status">Loading point sources…</p>
    <div v-if="error" class="request-error" role="alert">
      <span>{{ error }}</span>
      <AppButton automation="point-sources-retry" text="Retry" :icon="retryIcon" @click="load" />
    </div>
    <div v-else class="source-list">
      <AppFilter automation="point-sources-filter" constrained @[EVENTS.APPLY_FILTER]="applyFilter">
        <label class="app-filter-field" for="source-filter">
          <span>Filter by name</span>
          <input id="source-filter" v-model="filter" type="search" autocomplete="off" />
        </label>
      </AppFilter>
      <p v-if="!loading && filtered.length === 0" class="empty-state" role="status">
        No point sources found.
      </p>
      <table v-else>
        <caption class="visually-hidden">
          Configured point sources
        </caption>
        <thead>
          <tr>
            <th scope="col">Name</th>
            <th scope="col">Kind</th>
            <th scope="col">Status</th>
            <th scope="col">Updated</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="source in filtered" :key="source.id">
            <th scope="row">
              <RouterLink :to="{ name: 'point-source-detail', params: { sourceId: source.id } }">
                {{ source.name }}
              </RouterLink>
              <small>{{ source.description }}</small>
            </th>
            <td>{{ kindLabel(source.kind) }}</td>
            <td>{{ source.enabled ? 'Enabled' : 'Disabled' }}</td>
            <td>{{ formatDate(source.updatedAt) }}</td>
          </tr>
        </tbody>
      </table>
    </div>
  </section>
</template>

<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref } from 'vue';
import newIcon from '@/assets/icons/new-flow-icon.svg';
import retryIcon from '@/assets/icons/retry-icon.svg';
import AppButton from '@/components/AppButton.vue';
import AppFilter from '@/components/AppFilter.vue';
import AppSvg from '@/components/AppSvg.vue';
import { EVENTS } from '@/constants/events';
import {
  pointSourceApi,
  type PointSourceKind,
  type PointSourceSummary
} from '@/features/pointSources/api/pointSourceApi';

const sources = ref<PointSourceSummary[]>([]);
const filter = ref('');
const appliedFilter = ref('');
const loading = ref(false);
const error = ref('');
let controller: AbortController | undefined;
const filtered = computed(() =>
  sources.value.filter(({ name }) =>
    name.toLowerCase().includes(appliedFilter.value.trim().toLowerCase())
  )
);
const applyFilter = (): void => {
  appliedFilter.value = filter.value;
};
const kindLabel = (kind: PointSourceKind): string =>
  ({ home_assistant: 'Home Assistant', mqtt: 'MQTT', http_json: 'HTTP/JSON' })[kind];
const formatDate = (value: string): string =>
  value
    ? new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'short' }).format(
        new Date(value)
      )
    : '—';
const load = async (): Promise<void> => {
  controller?.abort();
  controller = new AbortController();
  loading.value = true;
  error.value = '';
  try {
    sources.value = (await pointSourceApi.list(controller.signal)).items;
  } catch (reason) {
    if (!controller.signal.aborted)
      error.value = reason instanceof Error ? reason.message : 'Unable to load point sources';
  } finally {
    loading.value = false;
  }
};
onMounted(() => void load());
onBeforeUnmount(() => controller?.abort());
</script>

<style scoped lang="css">
.source-list table {
  width: 100%;
  border-collapse: collapse;
}

.source-list th,
.source-list td {
  padding: var(--space-6-5);
  text-align: left;
  border-bottom: var(--border-width-default) solid var(--color-border-default);
}

.source-list th small {
  display: block;
  margin-top: var(--space-1-5);
  color: var(--color-text-secondary);
  font-weight: var(--font-weight-regular);
}

/* Mobile breakpoint (40rem): stacks page and navigation content for phone layouts. */
@media (max-width: 40rem) {
  .source-list {
    overflow-x: auto;
  }
}
</style>
