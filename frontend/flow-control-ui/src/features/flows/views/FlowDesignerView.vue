<script setup lang="ts">
import { computed, watchEffect } from 'vue';

import FlowDesignerCanvas from '../components/FlowDesignerCanvas.vue';
import { useFlowsStore } from '../stores/flows';

const props = defineProps<{
  flowId: string;
}>();

const flowStore = useFlowsStore();
const flow = computed(() => flowStore.findFlow(props.flowId));

watchEffect(() => {
  flowStore.selectFlow(props.flowId);
});
</script>

<template>
  <section class="designer-page">
    <template v-if="flow">
      <div class="designer-heading">
        <div>
          <RouterLink :to="{ name: 'flows' }">← All flows</RouterLink>
          <div class="title-row">
            <h1>{{ flow.name }}</h1>
            <span :class="flow.status">{{ flow.status }}</span>
          </div>
          <p>{{ flow.description }}</p>
        </div>
        <button type="button" disabled>Deploy flow</button>
      </div>

      <FlowDesignerCanvas :flow="flow" />
    </template>

    <div v-else class="not-found">
      <p class="eyebrow">Flow not found</p>
      <h1>There is no flow named “{{ flowId }}”.</h1>
      <RouterLink :to="{ name: 'flows' }">Return to flows</RouterLink>
    </div>
  </section>
</template>

<style scoped>
.designer-page {
  width: min(1280px, calc(100% - 40px));
  margin: 0 auto;
  padding: 34px 0 60px;
}

.designer-heading {
  display: flex;
  gap: 28px;
  align-items: end;
  justify-content: space-between;
  margin-bottom: 24px;
}

.designer-heading a,
.not-found a {
  color: #0b7568;
  font-size: 13px;
  font-weight: 700;
  text-decoration: none;
}

.title-row {
  display: flex;
  gap: 12px;
  align-items: center;
  margin-top: 9px;
}

h1 {
  margin: 0;
  color: #102133;
  font-size: clamp(28px, 4vw, 38px);
  letter-spacing: -0.035em;
}

.title-row span {
  padding: 5px 8px;
  color: #687784;
  font-size: 10px;
  font-weight: 800;
  letter-spacing: 0.08em;
  background: #e5eaee;
  border-radius: 999px;
  text-transform: uppercase;
}

.title-row span.deployed {
  color: #0b655b;
  background: #dff6ee;
}

.designer-heading p {
  margin: 7px 0 0;
  color: #627587;
  font-size: 14px;
}

button {
  padding: 11px 16px;
  color: #76909d;
  font-weight: 700;
  background: #e5ecef;
  border: 0;
  border-radius: 9px;
}

.not-found {
  padding: 80px 0;
}

.eyebrow {
  margin: 0 0 10px;
  color: #a64f38;
  font-size: 11px;
  font-weight: 800;
  letter-spacing: 0.13em;
  text-transform: uppercase;
}

.not-found h1 {
  margin-bottom: 24px;
}

@media (max-width: 760px) {
  .designer-page {
    width: calc(100% - 28px);
    overflow-x: hidden;
  }

  .designer-heading button {
    display: none;
  }
}
</style>
