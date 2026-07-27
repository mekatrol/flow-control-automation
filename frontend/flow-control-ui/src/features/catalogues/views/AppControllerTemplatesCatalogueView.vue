<template>
  <section class="catalogue-page" aria-labelledby="templates-heading">
    <AppErrorNotice
      id="controller-templates-error-notice"
      automation="controller-templates-error"
      :message="store.error"
      retryable
      @retry="store.load"
    />
    <div class="page-heading">
      <div>
        <p>Deployment targets</p>
        <h1 id="templates-heading">Controller templates</h1>
        <p>Review the capabilities and limits available to flow targets.</p>
      </div>
      <RouterLink class="primary-link" :to="{ name: 'controller-template-new' }">
        <AppSvg :src="newIcon" automation="controller-templates-new-icon" size="1em" />
        New template
      </RouterLink>
    </div>

    <AppFilter
      automation="controller-templates-filter"
      constrained
      @[EVENTS.APPLY_FILTER]="applyFilter"
    >
      <label class="app-filter-field" for="templates-filter">
        <span>Filter controller templates</span>
        <input id="templates-filter" v-model="filter" type="search" autocomplete="off" />
      </label>
    </AppFilter>

    <p v-if="store.loading" role="status">Loading controller templates…</p>
    <p
      v-else-if="!store.error && store.result.items.length === 0"
      class="empty-state"
      role="status"
    >
      No controller templates found.
    </p>
    <template v-else-if="!store.error">
      <AppTable automation="controller-templates-table" caption="Controller templates">
        <template #head>
          <tr>
            <th scope="col">Name</th>
            <th scope="col">Type</th>
            <th scope="col">Point support</th>
            <th scope="col">Connectors</th>
            <th scope="col">Flow functions</th>
            <th scope="col">Execution</th>
            <th scope="col">Limits</th>
          </tr>
        </template>
        <template #body>
          <tr v-for="template in store.result.items" :key="template.id">
            <th scope="row">
              <RouterLink
                :to="{
                  name: 'controller-template-detail',
                  params: { resourceId: template.id }
                }"
              >
                {{ template.name }}
              </RouterLink>
              <small>{{ template.description || template.id }}</small>
            </th>
            <td>{{ template.readOnly ? 'Built-in, read-only' : 'Custom' }}</td>
            <td>
              {{ list(template.capabilities.pointTypes) }}
              <small>{{ list(template.capabilities.pointDirections) }}</small>
            </td>
            <td>{{ list(template.capabilities.connectorDataTypes) }}</td>
            <td>{{ template.capabilities.flowFunctions.length }} supported</td>
            <td>{{ list(template.capabilities.executionModes) }}</td>
            <td>{{ limits(template.limits) }}</td>
          </tr>
        </template>
      </AppTable>
      <AppTablePagination
        automation="controller-templates-pagination"
        :page="store.result.page"
        :page-count="store.result.pageCount"
        :page-size="store.result.pageSize"
        :range-start="rangeStart"
        :range-end="rangeEnd"
        :total-items="store.result.totalItems"
        @update:page="store.page = $event"
        @update:page-size="setPageSize"
      />
    </template>
  </section>
</template>

<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref } from 'vue';
import newIcon from '@/assets/icons/new-flow-icon.svg';
import AppErrorNotice from '@/components/AppErrorNotice.vue';
import AppFilter from '@/components/AppFilter.vue';
import AppSvg from '@/components/AppSvg.vue';
import AppTable from '@/components/AppTable.vue';
import AppTablePagination from '@/components/AppTablePagination.vue';
import { EVENTS } from '@/constants/events';
import type { ControllerTemplateSummary } from '@/features/catalogues/api/catalogueDto';
import { useControllerTemplatesCatalogueStore } from '@/features/catalogues/stores/catalogues';

const store = useControllerTemplatesCatalogueStore();
const filter = ref(store.filter);
const rangeStart = computed(() =>
  store.result.totalItems === 0 ? 0 : (store.result.page - 1) * store.result.pageSize + 1
);
const rangeEnd = computed(() =>
  Math.min(store.result.page * store.result.pageSize, store.result.totalItems)
);
const list = (values: string[]): string =>
  values.map((value) => value.replaceAll('_', ' ')).join(', ');
const limits = (value: ControllerTemplateSummary['limits']): string => {
  const configured = [
    value.maxFlows && `${value.maxFlows} flows`,
    value.maxNodesPerFlow && `${value.maxNodesPerFlow} nodes`,
    value.maxConnectionsPerFlow && `${value.maxConnectionsPerFlow} connections`,
    value.minimumIntervalMilliseconds && `${value.minimumIntervalMilliseconds} ms minimum`
  ].filter(Boolean);
  return configured.length ? configured.join(', ') : 'Unrestricted';
};
const applyFilter = (): void => {
  store.filter = filter.value;
  store.page = 1;
};
const setPageSize = (value: number): void => {
  store.pageSize = value;
  store.page = 1;
};
onMounted(() => void store.load());
onBeforeUnmount(store.cancel);
</script>
