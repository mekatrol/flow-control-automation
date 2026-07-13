<script setup lang="ts">
import { storeToRefs } from 'pinia';

import { useFlowsStore } from '../stores/flows';

const flowStore = useFlowsStore();
const { flows } = storeToRefs(flowStore);

const formatUpdatedAt = (updatedAt: string): string =>
  new Intl.DateTimeFormat(undefined, {
    dateStyle: 'medium',
    timeStyle: 'short'
  }).format(new Date(updatedAt));
</script>

<template>
  <section class="flow-library">
    <div class="page-heading">
      <div>
        <p class="eyebrow">Automation workspace</p>
        <h1>Flows</h1>
        <p>Design, inspect, and deploy independent automation flows.</p>
      </div>
      <button type="button" disabled>New flow</button>
    </div>

    <div class="flow-grid">
      <RouterLink
        v-for="flow in flows"
        :key="flow.id"
        class="flow-card"
        :to="{ name: 'flow-designer', params: { flowId: flow.id } }"
      >
        <div class="flow-card-heading">
          <span class="status" :class="flow.status">{{ flow.status }}</span>
          <span class="arrow" aria-hidden="true">→</span>
        </div>
        <h2>{{ flow.name }}</h2>
        <p>{{ flow.description }}</p>
        <dl>
          <div>
            <dt>Nodes</dt>
            <dd>{{ flow.nodes.length }}</dd>
          </div>
          <div>
            <dt>Updated</dt>
            <dd>{{ formatUpdatedAt(flow.updatedAt) }}</dd>
          </div>
        </dl>
      </RouterLink>
    </div>
  </section>
</template>

<style scoped>
.flow-library {
  width: min(1180px, calc(100% - 40px));
  margin: 0 auto;
  padding: 58px 0;
}

.page-heading {
  display: flex;
  gap: 32px;
  align-items: end;
  justify-content: space-between;
  margin-bottom: 34px;
}

.eyebrow {
  margin: 0 0 8px;
  color: #0b7568;
  font-size: 11px;
  font-weight: 800;
  letter-spacing: 0.13em;
  text-transform: uppercase;
}

h1 {
  margin: 0;
  color: #102133;
  font-size: clamp(34px, 5vw, 52px);
  letter-spacing: -0.04em;
}

.page-heading p:last-child {
  max-width: 560px;
  margin: 10px 0 0;
  color: #627587;
}

button {
  padding: 11px 16px;
  color: #76909d;
  font-weight: 700;
  background: #e5ecef;
  border: 0;
  border-radius: 9px;
}

.flow-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(300px, 1fr));
  gap: 18px;
}

.flow-card {
  padding: 22px;
  color: inherit;
  background: #fff;
  border: 1px solid #dce5ec;
  border-radius: 14px;
  box-shadow: 0 12px 30px rgb(31 55 75 / 5%);
  text-decoration: none;
  transition:
    border-color 160ms ease,
    transform 160ms ease,
    box-shadow 160ms ease;
}

.flow-card:hover,
.flow-card:focus-visible {
  border-color: #91b9b1;
  box-shadow: 0 18px 38px rgb(31 55 75 / 10%);
  outline: none;
  transform: translateY(-2px);
}

.flow-card-heading {
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.status {
  padding: 5px 8px;
  color: #6b7680;
  font-size: 10px;
  font-weight: 800;
  letter-spacing: 0.08em;
  background: #edf1f4;
  border-radius: 999px;
  text-transform: uppercase;
}

.status.deployed {
  color: #0b655b;
  background: #dff6ee;
}

.arrow {
  color: #8294a4;
  font-size: 22px;
}

h2 {
  margin: 22px 0 8px;
  color: #102133;
  font-size: 19px;
}

.flow-card > p {
  min-height: 44px;
  margin: 0;
  color: #627587;
  font-size: 14px;
  line-height: 1.55;
}

dl {
  display: flex;
  gap: 34px;
  margin: 24px 0 0;
  padding-top: 18px;
  border-top: 1px solid #edf1f4;
}

dt {
  margin-bottom: 4px;
  color: #8594a1;
  font-size: 10px;
  font-weight: 750;
  letter-spacing: 0.08em;
  text-transform: uppercase;
}

dd {
  margin: 0;
  color: #34495b;
  font-size: 12px;
  font-weight: 650;
}

@media (max-width: 640px) {
  .flow-library {
    width: min(100% - 28px, 1180px);
    padding: 38px 0;
  }

  .page-heading {
    align-items: start;
  }

  .page-heading button {
    display: none;
  }
}
</style>
