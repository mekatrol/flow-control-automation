<template>
  <section v-bind="automation()" class="catalogue-page" aria-labelledby="points-heading">
    <AppErrorNotice
      id="points-error-notice"
      v-bind="automation('error')"
      :message="errorMessage"
      retryable
      retry-label="Check again"
      @[EVENTS.RETRY]="refresh"
    />
    <div class="page-heading">
      <div>
        <p>Point definitions</p>
        <h1 id="points-heading">Points</h1>
        <p>Review standalone and grouped automation points and their capabilities.</p>
      </div>
      <RouterLink class="primary-link" :to="{ name: 'point-new' }">
        <AppSvg :src="newIcon" v-bind="automation('new-icon')" size="1em" />
        New point
      </RouterLink>
    </div>

    <AppFilter v-bind="automation('filter')" constrained @[EVENTS.APPLY_FILTER]="applyFilter">
      <label class="app-filter-field" for="points-filter">
        <span>Filter points</span>
        <input id="points-filter" v-model="filter" type="search" autocomplete="off" />
      </label>
    </AppFilter>

    <p v-if="store.loading" role="status">Loading points…</p>
    <p
      v-else-if="!store.error && store.result.items.length === 0"
      class="empty-state"
      role="status"
    >
      No points found.
    </p>
    <template v-else-if="!store.error">
      <AppTable v-bind="automation('table')" caption="Configured points">
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
      <AppTablePagination
        v-bind="automation('pagination')"
        :page="store.result.page"
        :page-count="store.result.pageCount"
        :page-size="store.result.pageSize"
        :range-start="rangeStart"
        :range-end="rangeEnd"
        :total-items="store.result.totalItems"
        @[EVENTS.UPDATE_PAGE]="setPage"
        @[EVENTS.UPDATE_PAGE_SIZE]="setPageSize"
      />
    </template>
  </section>
</template>

<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref } from 'vue';
import newIcon from '@/assets/icons/new-flow-icon.svg';
import AppErrorNotice from '@/components/AppErrorNotice.vue';
import { useAutomation } from '@/composables/useAutomation';
import AppFilter from '@/components/AppFilter.vue';
import AppSvg from '@/components/AppSvg.vue';
import AppTable from '@/components/AppTable.vue';
import AppTablePagination from '@/components/AppTablePagination.vue';
import { EVENTS } from '@/constants/events';
import { usePointsCatalogueStore } from '@/features/catalogues/stores/catalogues';

const automation = useAutomation('points-catalogue');
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
const errorMessage = computed(() =>
  store.errorStatus === 404
    ? `${store.error} This backend does not support the points API. Check the deployed backend version and try again.`
    : store.error
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
