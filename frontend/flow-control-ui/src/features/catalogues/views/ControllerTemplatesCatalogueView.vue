<template>
  <section class="catalogue-page" aria-labelledby="templates-heading">
    <div class="page-heading">
      <div>
        <p class="eyebrow">Deployment targets</p>
        <h1 id="templates-heading">Controller templates</h1>
        <p>Review the capabilities and limits available to flow targets.</p>
      </div>
      <RouterLink class="primary-link" :to="{ name: 'controller-template-new' }">
        New template
      </RouterLink>
    </div>

    <div class="catalogue-filter">
      <label for="templates-filter">Filter controller templates</label>
      <input
        id="templates-filter"
        v-model="store.filter"
        type="search"
        autocomplete="off"
        @input="store.page = 1"
      />
    </div>

    <p v-if="store.loading" role="status">Loading controller templates…</p>
    <div v-else-if="store.error" class="request-error" role="alert">
      <p>{{ store.error }}</p>
      <button type="button" @click="store.load">
        {{ store.unavailable ? 'Check again' : 'Retry' }}
      </button>
    </div>
    <p v-else-if="store.result.items.length === 0" class="empty-state" role="status">
      No controller templates found.
    </p>
    <template v-else>
      <AppTable caption="Controller templates">
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
      <TablePagination
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
import { computed, onBeforeUnmount, onMounted } from 'vue';
import AppTable from '@/components/AppTable.vue';
import TablePagination from '@/components/TablePagination.vue';
import type { ControllerTemplateSummary } from '@/features/catalogues/api/catalogueDto';
import { useControllerTemplatesCatalogueStore } from '@/features/catalogues/stores/catalogues';

const store = useControllerTemplatesCatalogueStore();
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
const setPageSize = (value: number): void => {
  store.pageSize = value;
  store.page = 1;
};
onMounted(() => void store.load());
onBeforeUnmount(store.cancel);
</script>
