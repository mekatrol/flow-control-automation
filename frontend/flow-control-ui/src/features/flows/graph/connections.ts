import type {
  FlowConnection,
  FlowConnectionEndpoint,
  FlowDefinition,
  FlowNodeConnector
} from '../types';

export interface ConnectionValidation {
  valid: boolean;
  message?: string;
}

const connectorAt = (
  flow: FlowDefinition,
  endpoint: FlowConnectionEndpoint
): FlowNodeConnector | undefined =>
  flow.nodes
    .find((node) => node.id === endpoint.nodeId)
    ?.connectors.find((connector) => connector.id === endpoint.connectorId);

export const connectorsAreCompatible = (
  start: FlowNodeConnector,
  end: FlowNodeConnector
): boolean =>
  // Connections always carry data from an output to an input. The `any` type is
  // an explicit wildcard so generic routing nodes can accept a concrete type.
  start.direction === 'output' &&
  end.direction === 'input' &&
  (start.dataType === 'any' || end.dataType === 'any' || start.dataType === end.dataType);

export const validateConnection = (
  flow: FlowDefinition,
  start: FlowConnectionEndpoint,
  end: FlowConnectionEndpoint
): ConnectionValidation => {
  const startConnector = connectorAt(flow, start);
  const endConnector = connectorAt(flow, end);
  if (!startConnector || !endConnector)
    return { valid: false, message: 'A connector no longer exists.' };
  if (start.nodeId === end.nodeId)
    return { valid: false, message: 'A node cannot connect to itself.' };
  if (!connectorsAreCompatible(startConnector, endConnector)) {
    return { valid: false, message: 'Choose a compatible input connector.' };
  }
  const duplicate = flow.connections.some(
    (connection) =>
      connection.start.nodeId === start.nodeId &&
      connection.start.connectorId === start.connectorId &&
      connection.end.nodeId === end.nodeId &&
      connection.end.connectorId === end.connectorId
  );
  if (duplicate) return { valid: false, message: 'That connection already exists.' };
  return { valid: true };
};

export const addConnection = (
  flow: FlowDefinition,
  start: FlowConnectionEndpoint,
  end: FlowConnectionEndpoint,
  id = `connection-${crypto.randomUUID()}`
): { connection?: FlowConnection; error?: string } => {
  const validation = validateConnection(flow, start, end);
  if (!validation.valid) return { error: validation.message };
  // Copy the endpoints so later changes to the transient drag state cannot
  // rewrite a connection that has already been accepted into the graph.
  return { connection: { id, start: { ...start }, end: { ...end } } };
};
