export type FlowStatus = 'draft' | 'deployed';

// These are the function blocks supported by the legacy flow engine. The enum
// is also the persisted wire value, so adding a block does not require a second
// translation table between the toolbox, graph, and API payload.
export enum FlowNodeFunctionType {
  Add = 'add',
  AnalogInput = 'analogInput',
  AnalogOutput = 'analogOutput',
  And = 'and',
  Average = 'average',
  Calculator = 'calculator',
  Calendar = 'calendar',
  Clamp = 'clamp',
  Comparator = 'comparator',
  Delay = 'delay',
  DigitalConstant = 'digitalConstant',
  DigitalInput = 'digitalInput',
  DigitalOutput = 'digitalOutput',
  FlowInput = 'flowInput',
  FlowOutput = 'flowOutput',
  If = 'if',
  Line = 'line',
  LevelShifter = 'levelShifter',
  Max = 'max',
  Min = 'min',
  Memory = 'memory',
  Nand = 'nand',
  Nor = 'nor',
  NumericConstant = 'numericConstant',
  Not = 'not',
  Or = 'or',
  Override = 'override',
  OnDelay = 'onDelay',
  Pulse = 'pulse',
  QualityGood = 'qualityGood',
  RisingEdge = 'risingEdge',
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
export type FlowInterfaceDataType = 'boolean' | 'number' | 'string' | 'event';
export type VirtualPointValueType = 'analog' | 'digital';
export type VirtualPointPersistence = 'volatile' | 'retained';

export interface VirtualPointDeclaration {
  key: string;
  valueType: VirtualPointValueType;
  units?: string;
  readable: boolean;
  commandable: boolean;
  persistence: VirtualPointPersistence;
  relinquishDefault?: boolean | number | null;
}

/** Defines one externally supplied value in a reusable flow interface. */
export interface FlowInterfaceInput {
  /** Stable non-empty identifier unique among the interface inputs. */
  id: string;
  /** User-visible non-empty label; it need not be globally unique. */
  name: string;
  /** Wire data type accepted at the flow boundary. */
  dataType: FlowInterfaceDataType;
  /** Engineering-unit symbol for numeric values, or absent for unitless/non-numeric values. */
  units?: string;
  /** Value used when no caller value is supplied; it must match `dataType`, while `null` means no default. */
  defaultValue?: boolean | number | string | null;
  /** Whether execution must reject a call that omits this input. */
  required: boolean;
}

/** Defines one value published by a reusable flow interface. */
export interface FlowInterfaceOutput {
  /** Stable non-empty identifier unique among the interface outputs. */
  id: string;
  /** User-visible non-empty label; it need not be globally unique. */
  name: string;
  /** Wire data type produced at the flow boundary. */
  dataType: FlowInterfaceDataType;
  /** Engineering-unit symbol for numeric values, or absent for unitless/non-numeric values. */
  units?: string;
}

/** Defines the versioned ordered input and output boundary of one flow. */
export interface FlowInterface {
  /** Current interface contract version; only version `1` is supported before release. */
  schemaVersion: 1;
  /** Inputs in stable author-defined display order; IDs must be unique and the array may be empty. */
  inputs: FlowInterfaceInput[];
  /** Outputs in stable author-defined display order; IDs must be unique and the array may be empty. */
  outputs: FlowInterfaceOutput[];
}

// These interfaces describe persisted flow data only. Selection, pointer
// gestures, zoom, and validation messages remain transient browser state so they
// cannot leak into API payloads.
/** Describes one typed connection point exposed by a flow node. */
export interface FlowNodeConnector {
  /** Stable non-empty identifier unique within its node. */
  id: string;
  /** User-visible connector label. */
  label: string;
  /** Whether graph edges leave or enter this connector. */
  direction: ConnectorDirection;
  /** Value contract used for connection compatibility; `any` accepts every supported data type. */
  dataType: ConnectorDataType;
  /** Node edge on which the connector is rendered. */
  side: ConnectorSide;
}

/** Represents one persisted node in editable graph and render order. */
export interface FlowNode {
  /** Stable non-empty identifier unique within the flow. */
  id: string;
  /** Supported registry kind that determines behavior, connectors, and configuration schema. */
  kind: FlowNodeKind;
  /** User-visible non-empty node label. */
  label: string;
  /** Horizontal canvas coordinate in CSS pixels; finite values may be negative on the unbounded workspace. */
  x: number;
  /** Vertical canvas coordinate in CSS pixels; finite values may be negative on the unbounded workspace. */
  y: number;
  /** Integer stacking position; larger values render above smaller values and values are normalized on save. */
  zOrder: number;
  /** Optional containing group ID, or absent when the node is at the flow root. */
  groupId?: string;
  /** Connectors in stable registry/display order with IDs unique within this node. */
  connectors: FlowNodeConnector[];
  /** Kind-specific persisted values validated by the matching node registry definition. */
  configuration: Record<string, FlowConfigurationValue>;
}

/** Identifies one connector endpoint without duplicating connector metadata. */
export interface FlowConnectionEndpoint {
  /** ID of an existing node in the same flow. */
  nodeId: string;
  /** ID of an existing connector on `nodeId`. */
  connectorId: string;
}

/** Represents one directed, type-compatible graph edge. */
export interface FlowConnection {
  /** Stable non-empty identifier unique within the flow. */
  id: string;
  /** Existing output connector from which the value originates. */
  start: FlowConnectionEndpoint;
  /** Existing compatible input connector that receives the value. */
  end: FlowConnectionEndpoint;
}

/** Complete persisted authoring contract for one flow revision. */
export interface FlowDefinition {
  /** Stable canonical flow identifier. */
  id: string;
  /** Non-empty user-visible flow name within backend length limits. */
  name: string;
  /** Optional user-authored prose; an empty string means no description. */
  description: string;
  /** Authoring/deployment lifecycle state represented by the current wire vocabulary. */
  status: FlowStatus;
  /** Whether automatic and manual runtime execution is prohibited. */
  disabled: boolean;
  /** ISO 8601 UTC instant at which this revision was last persisted. */
  updatedAt: string;
  /** Nodes in stable persisted order; node IDs must be unique. */
  nodes: FlowNode[];
  /** Directed edges in stable persisted order; connection IDs must be unique. */
  connections: FlowConnection[];
  /** Versioned callable boundary for composing this flow into other flows. */
  interface: FlowInterface;
  revision?: number;
  virtualPointDeclarations?: VirtualPointDeclaration[];
}
