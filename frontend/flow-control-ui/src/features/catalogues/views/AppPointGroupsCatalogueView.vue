<template>
  <section class="catalogue-page" aria-labelledby="groups-heading">
    <AppErrorNotice
      id="point-groups-error-notice"
      :message="store.error"
      retryable
      @[EVENTS.RETRY]="refresh"
    />
    <div class="page-heading">
      <div>
        <p>Point definitions</p>
        <h1 id="groups-heading">Point groups</h1>
        <p>Review reusable membership and shared source relationships.</p>
      </div>
      <RouterLink class="primary-link" :to="{ name: 'point-group-new' }">
        <AppSvg :src="newIcon" size="1em" />
        New group
      </RouterLink>
    </div>

    <AppFilter constrained @[EVENTS.APPLY_FILTER]="applyFilter">
      <label class="app-filter-field" for="groups-filter">
        <span>Filter point groups</span>
        <input id="groups-filter" v-model="filter" type="search" autocomplete="off" />
      </label>
    </AppFilter>

    <p v-if="store.loading" role="status">Loading point groups…</p>
    <p
      v-else-if="!store.error && store.result.items.length === 0"
      class="empty-state"
      role="status"
    >
      No point groups found.
    </p>
    <template v-else-if="!store.error">
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
      <AppPagination
        :page="store.result.page"
        :page-count="store.result.pageCount"
        :page-size="store.result.pageSize"
        :total-items="store.result.totalItems"
        :page-size-options="[10, 25, 50, 100]"
        aria-label="Template pagination"
        @page-change="setPage"
        @page-size-change="setPageSize"
      />
    </template>
  </section>
</template>

<script setup lang="ts">
import { onBeforeUnmount, onMounted, ref } from 'vue';
import newIcon from '@/assets/icons/new-icon.svg';
import AppErrorNotice from '@/components/AppErrorNotice.vue';
import AppFilter from '@/components/AppFilter.vue';
import AppSvg from '@/components/AppSvg.vue';
import AppTable from '@/components/AppTable.vue';
import AppPagination from '@/components/AppPagination.vue';
import { EVENTS } from '@/constants/events';
import { usePointGroupsCatalogueStore } from '@/features/catalogues/stores/catalogues';

const store = usePointGroupsCatalogueStore();
const filter = ref('');
const page = ref(1);
const pageSize = ref(10);
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
