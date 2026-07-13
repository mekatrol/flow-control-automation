import { computed, ref } from 'vue';
import { defineStore } from 'pinia';

import type { FlowRuntimeSnapshot } from '../api/flowRuntimeApi';

export const useFlowRuntimeStore = defineStore('flow-runtime', () => {
  // Runtime snapshots deliberately live outside the editable flow store. Deploy
  // progress, telemetry, and failures must never make a graph dirty or get saved.
  const snapshots = ref<Record<string, FlowRuntimeSnapshot>>({});
  const deploymentPending = ref<Record<string, boolean>>({});
  const deploymentErrors = ref<Record<string, string | undefined>>({});
  const connected = ref<Record<string, boolean>>({});

  const snapshotFor = (flowId: string) => snapshots.value[flowId];
  const isDeploying = (flowId: string): boolean => deploymentPending.value[flowId] === true;
  const deploymentError = (flowId: string) => deploymentErrors.value[flowId];
  const isConnected = (flowId: string): boolean => connected.value[flowId] === true;

  const beginDeployment = (flowId: string): void => {
    deploymentPending.value[flowId] = true;
    deploymentErrors.value[flowId] = undefined;
  };
  const applySnapshot = (snapshot: FlowRuntimeSnapshot): void => {
    snapshots.value[snapshot.flowId] = structuredClone(snapshot);
    connected.value[snapshot.flowId] = true;
  };
  const completeDeployment = (snapshot: FlowRuntimeSnapshot): void => {
    applySnapshot(snapshot);
    deploymentPending.value[snapshot.flowId] = false;
    deploymentErrors.value[snapshot.flowId] = undefined;
  };
  const failDeployment = (flowId: string, message: string): void => {
    deploymentPending.value[flowId] = false;
    deploymentErrors.value[flowId] = message;
  };
  const disconnect = (flowId: string): void => {
    connected.value[flowId] = false;
    // Values represent live observations. Clearing the snapshot prevents an old
    // value from looking current after the runtime endpoint becomes unreachable.
    delete snapshots.value[flowId];
  };

  return {
    snapshots,
    deploymentPending,
    deploymentErrors,
    connected,
    snapshotFor,
    isDeploying,
    deploymentError,
    isConnected,
    beginDeployment,
    applySnapshot,
    completeDeployment,
    failDeployment,
    disconnect,
    activeRuntimeCount: computed(() => Object.values(snapshots.value).length)
  };
});
