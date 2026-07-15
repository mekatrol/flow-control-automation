import { computed, ref } from 'vue';
import { defineStore } from 'pinia';

import { reorderNode as reorderNodeGraph, type ZOrderCommand } from '@/features/flows/graph/zOrder';
import { parseFlowDto, type FlowDto } from '@/features/flows/api/flowDto';
import { flowDomainToDto, flowDtoToDomain } from '@/features/flows/api/flowMapper';
import { addConnection as addGraphConnection } from '@/features/flows/graph/connections';
import type {
  FlowConfigurationValue,
  FlowConnectionEndpoint,
  FlowNode
} from '@/features/flows/types';
import type { FlowDefinition } from '@/features/flows/types';

export const useFlowsStore = defineStore('flows', () => {
  // The store starts empty and only accepts API payloads after schema validation.
  // This prevents sample data from masking an unavailable or malformed backend.
  const flows = ref<FlowDefinition[]>([]);
  // Baselines are server-confirmed copies used for dirty checks and discarding.
  const baselineFlows = ref<Record<string, FlowDefinition>>({});
  const activeFlowId = ref<string>();

  const activeFlow = computed(() => flows.value.find((flow) => flow.id === activeFlowId.value));

  const findFlow = (flowId: string): FlowDefinition | undefined =>
    flows.value.find((flow) => flow.id === flowId);

  const selectFlow = (flowId: string): void => {
    activeFlowId.value = findFlow(flowId)?.id;
  };

  const replaceFlowFromPayload = (payload: unknown): FlowDefinition => {
    const flow = flowDtoToDomain(parseFlowDto(payload));
    const index = flows.value.findIndex((candidate) => candidate.id === flow.id);
    if (index >= 0) flows.value[index] = flow;
    else flows.value.push(flow);
    baselineFlows.value[flow.id] = flowDtoToDomain(flowDomainToDto(flow));
    return flow;
  };

  const replaceAllFlowsFromPayloads = (payloads: unknown): FlowDefinition[] => {
    if (!Array.isArray(payloads)) throw new TypeError('Flow list payload must be an array.');
    // Validate and map the complete response before replacing reactive state. A
    // bad item therefore cannot leave the list half-updated.
    const nextFlows = payloads.map((payload) => flowDtoToDomain(parseFlowDto(payload)));
    const nextBaselines = Object.fromEntries(
      nextFlows.map((flow) => [flow.id, flowDtoToDomain(flowDomainToDto(flow))])
    );
    flows.value = nextFlows;
    baselineFlows.value = nextBaselines;
    activeFlowId.value = nextFlows.some(({ id }) => id === activeFlowId.value)
      ? activeFlowId.value
      : undefined;
    return nextFlows;
  };

  const removeConfirmedFlow = (flowId: string): boolean => {
    if (!findFlow(flowId)) return false;
    flows.value = flows.value.filter(({ id }) => id !== flowId);
    const { [flowId]: _, ...remainingBaselines } = baselineFlows.value;
    baselineFlows.value = remainingBaselines;
    if (activeFlowId.value === flowId) activeFlowId.value = undefined;
    return true;
  };

  const flowPayload = (flowId: string): FlowDto | undefined => {
    const flow = findFlow(flowId);
    return flow ? flowDomainToDto(flow) : undefined;
  };

  const isFlowDirty = (flowId: string): boolean => {
    const flow = findFlow(flowId);
    const baseline = baselineFlows.value[flowId];
    if (!flow) return false;
    if (!baseline) return true;
    // DTO mapping gives both graphs the same plain-data shape and key order, so
    // serialization is a stable whole-graph comparison for this bounded schema.
    return JSON.stringify(flowDomainToDto(flow)) !== JSON.stringify(flowDomainToDto(baseline));
  };

  const resetFlow = (flowId: string): boolean => {
    const baseline = baselineFlows.value[flowId];
    const index = flows.value.findIndex(({ id }) => id === flowId);
    if (!baseline || index < 0) return false;
    flows.value[index] = flowDtoToDomain(flowDomainToDto(baseline));
    return true;
  };

  const moveNode = (flowId: string, nodeId: string, x: number, y: number): boolean => {
    const node = findFlow(flowId)?.nodes.find((candidate) => candidate.id === nodeId);
    if (!node) return false;
    node.x = x;
    node.y = y;
    return true;
  };

  const reorderNode = (flowId: string, nodeId: string, command: ZOrderCommand): boolean => {
    const flow = findFlow(flowId);
    if (!flow) return false;
    const nodes = reorderNodeGraph(flow.nodes, nodeId, command);
    if (nodes === flow.nodes) return false;
    flow.nodes = nodes;
    return true;
  };

  const deleteNode = (flowId: string, nodeId: string): boolean => {
    const flow = findFlow(flowId);
    if (!flow || !flow.nodes.some((node) => node.id === nodeId)) return false;
    flow.nodes = flow.nodes.filter((node) => node.id !== nodeId);
    // A connection cannot survive without both endpoints. Removing these links
    // here keeps every store state valid, even before the next save.
    flow.connections = flow.connections.filter(
      (connection) => connection.start.nodeId !== nodeId && connection.end.nodeId !== nodeId
    );
    return true;
  };

  const connectNodes = (
    flowId: string,
    start: FlowConnectionEndpoint,
    end: FlowConnectionEndpoint,
    id?: string
  ): { success: boolean; error?: string } => {
    const flow = findFlow(flowId);
    if (!flow) return { success: false, error: 'The flow no longer exists.' };
    const result = addGraphConnection(flow, start, end, id);
    if (!result.connection) return { success: false, error: result.error };
    flow.connections.push(result.connection);
    return { success: true };
  };

  const deleteConnection = (flowId: string, connectionId: string): boolean => {
    const flow = findFlow(flowId);
    if (!flow || !flow.connections.some(({ id }) => id === connectionId)) return false;
    flow.connections = flow.connections.filter(({ id }) => id !== connectionId);
    return true;
  };

  const addNode = (flowId: string, node: FlowNode): boolean => {
    const flow = findFlow(flowId);
    if (!flow || flow.nodes.some(({ id }) => id === node.id)) return false;
    // The browser's structured clone copies nested connectors and settings, so
    // caller-owned palette or drag data cannot mutate the stored node later.
    flow.nodes.push(structuredClone(node));
    return true;
  };

  const updateNodeLabel = (flowId: string, nodeId: string, label: string): boolean => {
    const node = findFlow(flowId)?.nodes.find(({ id }) => id === nodeId);
    const trimmed = label.trim();
    if (!node || !trimmed) return false;
    node.label = trimmed;
    return true;
  };

  const updateNodeConfiguration = (
    flowId: string,
    nodeId: string,
    key: string,
    value: FlowConfigurationValue
  ): boolean => {
    const node = findFlow(flowId)?.nodes.find(({ id }) => id === nodeId);
    // Editors may update configured fields but cannot silently expand the saved
    // schema with an unknown key produced by stale UI metadata.
    if (!node || !(key in node.configuration)) return false;
    node.configuration[key] = value;
    return true;
  };

  return {
    flows,
    baselineFlows,
    activeFlowId,
    activeFlow,
    findFlow,
    selectFlow,
    replaceFlowFromPayload,
    replaceAllFlowsFromPayloads,
    removeConfirmedFlow,
    flowPayload,
    isFlowDirty,
    resetFlow,
    moveNode,
    reorderNode,
    deleteNode,
    connectNodes,
    deleteConnection,
    addNode,
    updateNodeLabel,
    updateNodeConfiguration
  };
});
