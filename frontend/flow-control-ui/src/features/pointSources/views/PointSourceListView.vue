<template>
  <section class="configuration-page">
    <div class="page-heading">
      <div>
        <p class="eyebrow">External systems</p>
        <h1>Point sources</h1>
        <p>Define reusable, read-only connections before mapping points.</p>
      </div>
      <RouterLink class="primary-link" :to="{ name: 'point-source-new' }">New source</RouterLink>
    </div>

    <p v-if="loading" role="status">Loading point sources…</p>
    <div v-if="error" class="request-error" role="alert">
      <span>{{ error }}</span>
      <button type="button" @click="load">Retry</button>
    </div>
    <div v-else class="source-list">
      <label for="source-filter">Filter by name</label>
      <input id="source-filter" v-model="filter" type="search" autocomplete="off" />
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
import {
  pointSourceApi,
  type PointSourceKind,
  type PointSourceSummary
} from '@/features/pointSources/api/pointSourceApi';

const sources = ref<PointSourceSummary[]>([]);
const filter = ref('');
const loading = ref(false);
const error = ref('');
let controller: AbortController | undefined;
const filtered = computed(() =>
  sources.value.filter(({ name }) => name.toLowerCase().includes(filter.value.trim().toLowerCase()))
);
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
