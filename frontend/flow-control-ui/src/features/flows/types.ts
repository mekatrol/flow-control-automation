export type FlowStatus = 'draft' | 'deployed';

// These are the function blocks supported by the legacy flow engine. The enum
// is also the persisted wire value, so adding a block does not require a second
// translation table between the toolbox, graph, and API payload.
export enum FlowNodeFunctionType {
  And = 'and',
  Average = 'average',
  Calculator = 'calculator',
  Calendar = 'calendar',
  Clamp = 'clamp',
  Comparator = 'comparator',
  Delay = 'delay',
  If = 'if',
  Line = 'line',
  Max = 'max',
  Min = 'min',
  Nand = 'nand',
  Nor = 'nor',
  Not = 'not',
  Or = 'or',
  Override = 'override',
  Pulse = 'pulse',
  Schedule = 'schedule',
  Selector = 'selector',
  Sequence = 'sequence',
  Split = 'split',
  Timer = 'timer',
  Xnor = 'xnor',
  Xor = 'xor'
}

// Persisted flow JSON contains the enum's string values, not enum members.
// Keep those wire values assignable while deriving the union from the enum so
// the accepted kinds cannot drift from the registry.
export type FlowNodeKind = `${FlowNodeFunctionType}`;

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
