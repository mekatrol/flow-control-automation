<template>
  <section class="catalogue-page" aria-labelledby="groups-heading">
    <div class="page-heading">
      <div>
        <p class="eyebrow">Point definitions</p>
        <h1 id="groups-heading">Point groups</h1>
        <p>Review reusable membership and shared source relationships.</p>
      </div>
      <RouterLink class="primary-link" :to="{ name: 'point-group-new' }">New group</RouterLink>
    </div>

    <form class="catalogue-filter" role="search" @submit.prevent="applyFilter">
      <label for="groups-filter">Filter point groups</label>
      <div>
        <input id="groups-filter" v-model="filter" type="search" autocomplete="off" />
        <button type="submit">Apply filter</button>
      </div>
    </form>

    <p v-if="store.loading" role="status">Loading point groups…</p>
    <div v-else-if="store.error" class="request-error" role="alert">
      <p>{{ store.error }}</p>
      <button type="button" @click="refresh">
        {{ store.unavailable ? 'Check again' : 'Retry' }}
      </button>
    </div>
    <p v-else-if="store.result.items.length === 0" class="empty-state" role="status">
      No point groups found.
    </p>
    <template v-else>
      <AppTable caption="Configured point groups">
        <template #head>
          <tr>
            <th scope="col">Name</th>
            <th scope="col">Shared source</th>
            <th scope="col">Revision</th>
            <th scope="col">Updated</th>
          </tr>
        </template>
        <template #body>
          <tr v-for="group in store.result.items" :key="group.id">
            <th scope="row">
              <RouterLink :to="{ name: 'point-group-detail', params: { resourceId: group.id } }">
                {{ group.name }}
              </RouterLink>
              <small>{{ group.description || group.id }}</small>
            </th>
            <td>{{ group.sourceId || 'None' }}</td>
            <td>{{ group.revision }}</td>
            <td>{{ formatDate(group.updatedAt) }}</td>
          </tr>
        </template>
      </AppTable>
      <TablePagination
        automation="point-groups-pagination"
        :page="store.result.page"
        :page-count="store.result.pageCount"
        :page-size="store.result.pageSize"
        :range-start="rangeStart"
        :range-end="rangeEnd"
        :total-items="store.result.totalItems"
        @update:page="setPage"
        @update:page-size="setPageSize"
      />
    </template>
  </section>
</template>

<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref } from 'vue';
import AppTable from '@/components/AppTable.vue';
import TablePagination from '@/components/TablePagination.vue';
import { usePointGroupsCatalogueStore } from '@/features/catalogues/stores/catalogues';

const store = usePointGroupsCatalogueStore();
const filter = ref('');
const page = ref(1);
const pageSize = ref(10);
const rangeStart = computed(() =>
  store.result.totalItems === 0 ? 0 : (store.result.page - 1) * store.result.pageSize + 1
);
const rangeEnd = computed(() =>
  Math.min(store.result.page * store.result.pageSize, store.result.totalItems)
);
const formatDate = (value?: string): string =>
  value
    ? new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'short' }).format(
        new Date(value)
      )
    : '—';
const refresh = (): Promise<void> =>
  store.load({ filter: filter.value, page: page.value, pageSize: pageSize.value });
const applyFilter = (): void => {
  page.value = 1;
  void refresh();
};
const setPage = (value: number): void => {
  page.value = value;
  void refresh();
};
const setPageSize = (value: number): void => {
  pageSize.value = value;
  page.value = 1;
  void refresh();
};
onMounted(() => void refresh());
onBeforeUnmount(store.cancel);
</script>
