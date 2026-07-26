<template>
  <section class="catalogue-page" aria-labelledby="points-heading">
    <div class="page-heading">
      <div>
        <p class="eyebrow">Point definitions</p>
        <h1 id="points-heading">Points</h1>
        <p>Review standalone and grouped automation points and their capabilities.</p>
      </div>
      <RouterLink class="primary-link" :to="{ name: 'point-new' }">New point</RouterLink>
    </div>

    <form class="catalogue-filter" role="search" @submit.prevent="applyFilter">
      <label for="points-filter">Filter points</label>
      <div>
        <input id="points-filter" v-model="filter" type="search" autocomplete="off" />
        <button type="submit">Apply filter</button>
      </div>
    </form>

    <p v-if="store.loading" role="status">Loading points…</p>
    <div v-else-if="store.error" class="request-error" role="alert">
      <p>{{ store.error }}</p>
      <button type="button" @click="refresh">
        {{ store.unavailable ? 'Check again' : 'Retry' }}
      </button>
    </div>
    <p v-else-if="store.result.items.length === 0" class="empty-state" role="status">
      No points found.
    </p>
    <template v-else>
      <AppTable automation="points-table" caption="Configured points">
        <template #head>
          <tr>
            <th scope="col">Name</th>
            <th scope="col">Membership</th>
            <th scope="col">Source</th>
            <th scope="col">Implementation</th>
            <th scope="col">Direction</th>
            <th scope="col">Value type</th>
            <th scope="col">Capabilities</th>
            <th scope="col">Status</th>
          </tr>
        </template>
        <template #body>
          <tr v-for="point in store.result.items" :key="point.id">
            <th scope="row">
              <RouterLink :to="{ name: 'point-detail', params: { resourceId: point.id } }">
                {{ point.name }}
              </RouterLink>
              <small>{{ point.description || point.id }}</small>
            </th>
            <td>{{ point.groupId ? `Group: ${point.groupId}` : 'Standalone' }}</td>
            <td>{{ point.sourceId || (point.groupId ? 'Inherited from group' : 'None') }}</td>
            <td>{{ label(point.implementation) }}</td>
            <td>{{ label(point.direction) }}</td>
            <td>
              {{ label(point.valueType) }}
              <small v-if="point.units">{{ point.units }}</small>
            </td>
            <td>{{ capabilities(point.readable, point.commandable) }}</td>
            <td>{{ point.enabled ? 'Enabled' : 'Disabled' }}</td>
          </tr>
        </template>
      </AppTable>
      <TablePagination
        automation="points-pagination"
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
import { usePointsCatalogueStore } from '@/features/catalogues/stores/catalogues';

const store = usePointsCatalogueStore();
const filter = ref('');
const page = ref(1);
const pageSize = ref(10);
const rangeStart = computed(() =>
  store.result.totalItems === 0 ? 0 : (store.result.page - 1) * store.result.pageSize + 1
);
const rangeEnd = computed(() =>
  Math.min(store.result.page * store.result.pageSize, store.result.totalItems)
);
const label = (value: string): string =>
  value.replaceAll('_', ' ').replace(/^\w/, (first) => first.toUpperCase());
const capabilities = (readable: boolean, commandable: boolean): string =>
  [readable && 'Read', commandable && 'Command'].filter(Boolean).join(', ') || 'None';
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
