export type FlowStatus = 'draft' | 'deployed';

export type FlowNodeKind = 'calculator' | 'override' | 'pulse' | 'split';

export interface FlowNode {
  id: string;
  kind: FlowNodeKind;
  label: string;
  x: number;
  y: number;
  color: string;
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
