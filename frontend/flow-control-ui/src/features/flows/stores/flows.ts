import { computed, ref } from 'vue';
import { defineStore } from 'pinia';

import { sampleFlows } from '../sampleFlows';
import type { FlowDefinition } from '../types';

export const useFlowsStore = defineStore('flows', () => {
  const flows = ref<FlowDefinition[]>(structuredClone(sampleFlows));
  const activeFlowId = ref<string>();

  const activeFlow = computed(() => flows.value.find((flow) => flow.id === activeFlowId.value));

  const findFlow = (flowId: string): FlowDefinition | undefined =>
    flows.value.find((flow) => flow.id === flowId);

  const selectFlow = (flowId: string): void => {
    activeFlowId.value = findFlow(flowId)?.id;
  };

  return { flows, activeFlowId, activeFlow, findFlow, selectFlow };
});
