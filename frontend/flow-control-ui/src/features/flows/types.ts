export type FlowStatus = 'draft' | 'deployed';

export type FlowNodeKind = 'calculator' | 'override' | 'pulse' | 'split';

export type ConnectorDirection = 'input' | 'output';

export type ConnectorDataType = 'any' | 'boolean' | 'event' | 'number' | 'string';

export type ConnectorSide = 'left' | 'right' | 'top' | 'bottom';

export type FlowConfigurationValue = boolean | number | string | null;

// These interfaces describe persisted flow data only. Selection, pointer
// gestures, zoom, and validation messages remain transient browser state so they
// cannot leak into API payloads.
export interface FlowNodeConnector {
  id: string;
  label: string;
  direction: ConnectorDirection;
  dataType: ConnectorDataType;
  side: ConnectorSide;
}

export interface FlowNode {
  id: string;
  kind: FlowNodeKind;
  label: string;
  x: number;
  y: number;
  zOrder: number;
  color: string;
  connectors: FlowNodeConnector[];
  configuration: Record<string, FlowConfigurationValue>;
}

export interface FlowConnectionEndpoint {
  nodeId: string;
  connectorId: string;
}

export interface FlowConnection {
  id: string;
  start: FlowConnectionEndpoint;
  end: FlowConnectionEndpoint;
}

export interface FlowDefinition {
  id: string;
  name: string;
  description: string;
  status: FlowStatus;
  updatedAt: string;
  nodes: FlowNode[];
  connections: FlowConnection[];
}
