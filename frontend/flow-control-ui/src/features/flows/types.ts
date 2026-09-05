import {
  FlowNodeType,
  DataDirectionType,
  AutomationPointValueType,
  VirtualPointPersistenceType,
  type DataType
} from '@/types/serverTypes';

export type FlowStatus = 'draft' | 'deployed';

export { FlowNodeType, DataType } from '@/types/serverTypes';

export type ConnectorDirection = typeof DataDirectionType.Input | typeof DataDirectionType.Output;

export type ConnectorSide = 'left' | 'right' | 'top' | 'bottom';

export type FlowConfigurationValue = boolean | number | string | null;
export type VirtualPointValueType =
  | typeof AutomationPointValueType.Analog
  | typeof AutomationPointValueType.Digital;

export interface VirtualPointDeclaration {
  key: string;
  valueType: VirtualPointValueType;
  units?: string;
  readable: boolean;
  commandable: boolean;
  persistence: VirtualPointPersistenceType;
  relinquishDefault?: boolean | number | null;
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
  dataType: DataType;
  /** Node edge on which the connector is rendered. */
  side: ConnectorSide;
}

/** Represents one persisted node in editable graph and render order. */
export interface FlowNode {
  /** Stable non-empty identifier unique within the flow. */
  id: string;
  /** Supported registry type that determines behavior, connectors, and configuration schema. */
  nodeType: FlowNodeType;
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
  /** Type-specific persisted values validated by the matching node registry definition. */
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
  revision?: number;
  /** Revision captured by the last successful deployment, when one exists. */
  deployedRevision?: number;
}

export const isVirtualPointNode = (node: FlowNode): boolean =>
  node.nodeType === FlowNodeType.AnalogVirtual || node.nodeType === FlowNodeType.DigitalVirtual;

export const unconnectedVirtualPoint = (flow: FlowDefinition): FlowNode | undefined =>
  flow.nodes.find(
    (node) =>
      isVirtualPointNode(node) &&
      !flow.connections.some(
        (connection) => connection.start.nodeId === node.id || connection.end.nodeId === node.id
      )
  );

export const virtualPointDeclarationFromNode = (
  node: FlowNode
): VirtualPointDeclaration | undefined => {
  if (!isVirtualPointNode(node)) return undefined;
  const units = String(node.configuration.units ?? '').trim();
  const relinquishDefault = node.configuration.relinquishDefault;
  return {
    key: String(node.configuration.pointId ?? '').trim(),
    valueType:
      node.nodeType === FlowNodeType.AnalogVirtual
        ? AutomationPointValueType.Analog
        : AutomationPointValueType.Digital,
    ...(units ? { units } : {}),
    readable: true,
    commandable: true,
    persistence:
      node.configuration.persistence === VirtualPointPersistenceType.Retained
        ? VirtualPointPersistenceType.Retained
        : VirtualPointPersistenceType.Volatile,
    ...(relinquishDefault !== null && relinquishDefault !== ''
      ? { relinquishDefault: relinquishDefault as boolean | number }
      : {})
  };
};

export const virtualPointDeclarationsFromNodes = (nodes: FlowNode[]): VirtualPointDeclaration[] =>
  nodes.flatMap((node) => {
    const declaration = virtualPointDeclarationFromNode(node);
    return declaration ? [declaration] : [];
  });
