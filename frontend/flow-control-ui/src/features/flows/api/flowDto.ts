import type {
  ConnectorDataType,
  ConnectorDirection,
  ConnectorSide,
  FlowConfigurationValue,
  FlowNodeKind,
  FlowStatus
} from '@/features/flows/types';
import { flowNodeKinds } from '@/features/flows/nodeKinds';

export interface FlowNodeConnectorDto {
  id: string;
  label: string;
  direction: ConnectorDirection;
  dataType: ConnectorDataType;
  side: ConnectorSide;
}

export interface FlowNodeDto {
  id: string;
  kind: FlowNodeKind;
  label: string;
  x: number;
  y: number;
  zOrder: number;
  connectors: FlowNodeConnectorDto[];
  configuration: Record<string, FlowConfigurationValue>;
}

export interface FlowConnectionEndpointDto {
  nodeId: string;
  connectorId: string;
}

export interface FlowConnectionDto {
  id: string;
  start: FlowConnectionEndpointDto;
  end: FlowConnectionEndpointDto;
}

export interface FlowDto {
  id: string;
  name: string;
  description: string;
  status: FlowStatus;
  updatedAt: string;
  nodes: FlowNodeDto[];
  connections: FlowConnectionDto[];
}

// Validation errors include a data path so API failures can identify the exact
// node, connector, or connection that made an otherwise large graph unusable.
export class FlowDtoValidationError extends Error {
  constructor(message: string) {
    super(message);
    this.name = 'FlowDtoValidationError';
  }
}

const nodeKinds = new Set<FlowNodeKind>(flowNodeKinds);
const statuses = new Set<FlowStatus>(['draft', 'deployed']);
const directions = new Set<ConnectorDirection>(['input', 'output']);
const dataTypes = new Set<ConnectorDataType>(['any', 'boolean', 'event', 'number', 'string']);
const sides = new Set<ConnectorSide>(['left', 'right', 'top', 'bottom']);

const fail = (path: string, reason: string): never => {
  throw new FlowDtoValidationError(`${path}: ${reason}`);
};

const asRecord = (value: unknown, path: string): Record<string, unknown> => {
  if (typeof value !== 'object' || value === null || Array.isArray(value)) {
    return fail(path, 'expected an object');
  }
  return value as Record<string, unknown>;
};

const asArray = (value: unknown, path: string): unknown[] => {
  if (!Array.isArray(value)) return fail(path, 'expected an array');
  return value;
};

const asString = (value: unknown, path: string): string => {
  if (typeof value !== 'string' || value.length === 0)
    return fail(path, 'expected a non-empty string');
  return value;
};

const asFiniteNumber = (value: unknown, path: string): number => {
  if (typeof value !== 'number' || !Number.isFinite(value))
    return fail(path, 'expected a finite number');
  return value;
};

const asEnum = <Value extends string>(value: unknown, allowed: Set<Value>, path: string): Value => {
  const text = asString(value, path);
  if (!allowed.has(text as Value)) return fail(path, `unsupported value “${text}”`);
  return text as Value;
};

const asNodeKind = (value: unknown, path: string): FlowNodeKind => {
  // `invert` was the original persisted name for the NOT gate. Normalize it at
  // the API boundary so existing flows open as NOT nodes and are upgraded the
  // next time they are saved.
  if (value === 'invert') return 'not' as FlowNodeKind;
  return asEnum(value, nodeKinds, path);
};

const assertUnique = (ids: string[], path: string): void => {
  const seen = new Set<string>();
  for (const id of ids) {
    if (seen.has(id)) fail(path, `duplicate id “${id}”`);
    seen.add(id);
  }
};

const parseConfiguration = (
  value: unknown,
  path: string
): Record<string, FlowConfigurationValue> => {
  // Configuration is deliberately limited to JSON scalar values. This keeps saved
  // graphs portable and prevents component instances or browser objects from being
  // smuggled into persisted Pinia state.
  const source = asRecord(value, path);
  const configuration: Record<string, FlowConfigurationValue> = {};
  for (const [key, entry] of Object.entries(source)) {
    if (entry !== null && !['boolean', 'number', 'string'].includes(typeof entry)) {
      fail(`${path}.${key}`, 'expected a boolean, number, string, or null');
    }
    if (typeof entry === 'number' && !Number.isFinite(entry)) {
      fail(`${path}.${key}`, 'expected a finite number');
    }
    configuration[key] = entry as FlowConfigurationValue;
  }
  return configuration;
};

const parseConnector = (value: unknown, path: string): FlowNodeConnectorDto => {
  const source = asRecord(value, path);
  return {
    id: asString(source.id, `${path}.id`),
    label: asString(source.label, `${path}.label`),
    direction: asEnum(source.direction, directions, `${path}.direction`),
    dataType: asEnum(source.dataType, dataTypes, `${path}.dataType`),
    side: asEnum(source.side, sides, `${path}.side`)
  };
};

const parseNode = (value: unknown, path: string): FlowNodeDto => {
  const source = asRecord(value, path);
  const connectors = asArray(source.connectors, `${path}.connectors`).map((connector, index) =>
    parseConnector(connector, `${path}.connectors[${index}]`)
  );
  assertUnique(
    connectors.map((connector) => connector.id),
    `${path}.connectors`
  );
  return {
    id: asString(source.id, `${path}.id`),
    kind: asNodeKind(source.kind, `${path}.kind`),
    label: asString(source.label, `${path}.label`),
    x: asFiniteNumber(source.x, `${path}.x`),
    y: asFiniteNumber(source.y, `${path}.y`),
    zOrder: asFiniteNumber(source.zOrder, `${path}.zOrder`),
    connectors,
    configuration: parseConfiguration(source.configuration, `${path}.configuration`)
  };
};

const parseEndpoint = (value: unknown, path: string): FlowConnectionEndpointDto => {
  const source = asRecord(value, path);
  return {
    nodeId: asString(source.nodeId, `${path}.nodeId`),
    connectorId: asString(source.connectorId, `${path}.connectorId`)
  };
};

const parseConnection = (value: unknown, path: string): FlowConnectionDto => {
  const source = asRecord(value, path);
  return {
    id: asString(source.id, `${path}.id`),
    start: parseEndpoint(source.start, `${path}.start`),
    end: parseEndpoint(source.end, `${path}.end`)
  };
};

const findConnector = (
  nodes: FlowNodeDto[],
  endpoint: FlowConnectionEndpointDto,
  path: string
): FlowNodeConnectorDto => {
  const node = nodes.find((candidate) => candidate.id === endpoint.nodeId);
  if (!node) return fail(`${path}.nodeId`, `unknown node “${endpoint.nodeId}”`);
  const connector = node.connectors.find((candidate) => candidate.id === endpoint.connectorId);
  if (!connector) return fail(`${path}.connectorId`, `unknown connector “${endpoint.connectorId}”`);
  return connector;
};

export const parseFlowDto = (value: unknown): FlowDto => {
  // Parse nodes before connections because connection validation needs the complete
  // connector catalogue. This also enforces referential integrity at the API edge
  // instead of letting the SVG renderer silently drop broken links later.
  const source = asRecord(value, 'flow');
  const nodes = asArray(source.nodes, 'flow.nodes').map((node, index) =>
    parseNode(node, `flow.nodes[${index}]`)
  );
  const connections = asArray(source.connections, 'flow.connections').map((connection, index) =>
    parseConnection(connection, `flow.connections[${index}]`)
  );
  assertUnique(
    nodes.map((node) => node.id),
    'flow.nodes'
  );
  assertUnique(
    connections.map((connection) => connection.id),
    'flow.connections'
  );

  // Persisted connections must run from an output to an input. The `any` data type
  // is the explicit wildcard; all other endpoint types must match exactly.
  connections.forEach((connection, index) => {
    const path = `flow.connections[${index}]`;
    const start = findConnector(nodes, connection.start, `${path}.start`);
    const end = findConnector(nodes, connection.end, `${path}.end`);
    if (start.direction !== 'output') fail(`${path}.start`, 'must reference an output connector');
    if (end.direction !== 'input') fail(`${path}.end`, 'must reference an input connector');
    if (start.dataType !== 'any' && end.dataType !== 'any' && start.dataType !== end.dataType) {
      fail(path, `connector types “${start.dataType}” and “${end.dataType}” are incompatible`);
    }
  });

  const updatedAt = asString(source.updatedAt, 'flow.updatedAt');
  if (Number.isNaN(Date.parse(updatedAt))) fail('flow.updatedAt', 'expected an ISO date-time');

  return {
    id: asString(source.id, 'flow.id'),
    name: asString(source.name, 'flow.name'),
    description:
      typeof source.description === 'string'
        ? source.description
        : fail('flow.description', 'expected a string'),
    status: asEnum(source.status, statuses, 'flow.status'),
    updatedAt,
    nodes,
    connections
  };
};

export const parseFlowDtoJson = (json: string): FlowDto => {
  // Keep malformed JSON distinct from a well-formed graph with invalid fields so
  // error messages remain useful at import and API boundaries.
  try {
    return parseFlowDto(JSON.parse(json));
  } catch (error) {
    if (error instanceof FlowDtoValidationError) throw error;
    throw new FlowDtoValidationError('flow: malformed JSON');
  }
};
