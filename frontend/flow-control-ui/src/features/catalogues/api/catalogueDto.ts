import {
  ControllerPointFeatureType,
  ConnectorDataType,
  FlowFunctionType,
  ExecutionModeType,
  ControllerRuntimeFeatureType
} from '@/types/serverTypes';
import { VirtualPointPersistenceType } from '@/types/serverTypes';
import { PointSourceType, DataDirectionType, AutomationPointValueType } from '@/types/serverTypes';
export { PointSourceType, DataDirectionType, AutomationPointValueType } from '@/types/serverTypes';

export interface PointSummary {
  id: string;
  name: string;
  description?: string;
  enabled: boolean;
  groupId?: string;
  pointSourceType: PointSourceType;
  direction: DataDirectionType;
  valueType: AutomationPointValueType;
  units?: string;
  readable: boolean;
  commandable: boolean;
  sourceId?: string;
  revision: number;
  updatedAt?: string;
}

export interface PointGroupSummary {
  id: string;
  name: string;
  description?: string;
  sourceId?: string;
  revision: number;
  updatedAt?: string;
}

export interface ControllerTemplateSummary {
  schemaVersion: 1;
  id: string;
  name: string;
  description?: string;
  readOnly: boolean;
  capabilities: {
    pointTypes: AutomationPointValueType[];
    pointDirections: DataDirectionType[];
    pointFeatures: ControllerPointFeatureType[];
    connectorDataTypes: ConnectorDataType[];
    flowFunctions: FlowFunctionType[];
    executionModes: ExecutionModeType[];
    runtimeFeatures: ControllerRuntimeFeatureType[];
  };
  limits: {
    maxFlows?: number;
    maxNodesPerFlow?: number;
    maxConnectionsPerFlow?: number;
    minimumIntervalMilliseconds?: number;
  };
  revision: number;
  updatedAt?: string;
}

export interface Page<T> {
  items: T[];
  totalItems: number;
  page: number;
  pageSize: number;
  pageCount: number;
}

type JsonObject = Record<string, unknown>;

const object = (value: unknown, path: string): JsonObject => {
  if (typeof value !== 'object' || value === null || Array.isArray(value))
    throw new Error(`${path} must be an object`);
  return value as JsonObject;
};

const string = (value: unknown, path: string): string => {
  if (typeof value !== 'string') throw new Error(`${path} must be a string`);
  return value;
};

const optionalString = (value: unknown, path: string): string | undefined => {
  if (value === undefined || value === null) return undefined;
  return string(value, path);
};

const boolean = (value: unknown, path: string): boolean => {
  if (typeof value !== 'boolean') throw new Error(`${path} must be a boolean`);
  return value;
};

const integer = (value: unknown, path: string): number => {
  if (!Number.isSafeInteger(value) || (value as number) < 0)
    throw new Error(`${path} must be a non-negative safe integer`);
  return value as number;
};

const enumeration = <T extends string>(value: unknown, values: readonly T[], path: string): T => {
  const parsed = string(value, path);
  if (!values.includes(parsed as T)) throw new Error(`${path} is unsupported`);
  return parsed as T;
};

const enumArray = <T extends string>(value: unknown, values: readonly T[], path: string): T[] => {
  if (!Array.isArray(value)) throw new Error(`${path} must be an array`);
  return value.map((item, index) => enumeration(item, values, `${path}[${index}]`));
};

const optionalPositiveInteger = (value: unknown, path: string): number | undefined => {
  if (value === undefined || value === null) return undefined;
  const parsed = integer(value, path);
  if (parsed === 0) throw new Error(`${path} must be positive`);
  return parsed;
};

export const parsePoint = (value: unknown, path = 'point'): PointSummary => {
  const item = object(value, path);
  enumeration(item.persistence, Object.values(VirtualPointPersistenceType), `${path}.persistence`);
  for (const field of ['mapping', 'limits', 'safeDisablePolicy'] as const) {
    if (item[field] !== undefined && item[field] !== null) object(item[field], `${path}.${field}`);
  }
  if (item.stateLabels !== undefined && item.stateLabels !== null) {
    if (typeof item.stateLabels !== 'object')
      throw new Error(`${path}.stateLabels must be an object or array`);
  }
  optionalString(item.createdAt, `${path}.createdAt`);
  return {
    id: string(item.id, `${path}.id`),
    name: string(item.name, `${path}.name`),
    description: optionalString(item.description, `${path}.description`),
    enabled: boolean(item.enabled, `${path}.enabled`),
    groupId: optionalString(item.groupId, `${path}.groupId`),
    pointSourceType: enumeration(
      item.pointSourceType,
      Object.values(PointSourceType),
      `${path}.pointSourceType`
    ),
    direction: enumeration(item.direction, Object.values(DataDirectionType), `${path}.direction`),
    valueType: enumeration(
      item.valueType,
      Object.values(AutomationPointValueType),
      `${path}.valueType`
    ),
    units: optionalString(item.units, `${path}.units`),
    readable: boolean(item.readable, `${path}.readable`),
    commandable: boolean(item.commandable, `${path}.commandable`),
    sourceId: optionalString(item.sourceId, `${path}.sourceId`),
    revision: integer(item.revision, `${path}.revision`),
    updatedAt: optionalString(item.updatedAt, `${path}.updatedAt`)
  };
};

export const parsePointGroup = (value: unknown, path = 'group'): PointGroupSummary => {
  const item = object(value, path);
  if (item.mappingDefaults !== undefined && item.mappingDefaults !== null)
    object(item.mappingDefaults, `${path}.mappingDefaults`);
  optionalString(item.createdAt, `${path}.createdAt`);
  return {
    id: string(item.id, `${path}.id`),
    name: string(item.name, `${path}.name`),
    description: optionalString(item.description, `${path}.description`),
    sourceId: optionalString(item.sourceId, `${path}.sourceId`),
    revision: integer(item.revision, `${path}.revision`),
    updatedAt: optionalString(item.updatedAt, `${path}.updatedAt`)
  };
};

export const parseControllerTemplate = (
  value: unknown,
  path = 'controllerTemplate'
): ControllerTemplateSummary => {
  const item = object(value, path);
  const capabilities = object(item.capabilities, `${path}.capabilities`);
  const limits = object(item.limits, `${path}.limits`);
  optionalString(item.createdAt, `${path}.createdAt`);
  if (item.schemaVersion !== 1) throw new Error(`${path}.schemaVersion must be 1`);
  return {
    schemaVersion: 1,
    id: string(item.id, `${path}.id`),
    name: string(item.name, `${path}.name`),
    description: optionalString(item.description, `${path}.description`),
    readOnly: boolean(item.readOnly, `${path}.readOnly`),
    capabilities: {
      pointTypes: enumArray(
        capabilities.pointTypes,
        Object.values(AutomationPointValueType),
        `${path}.capabilities.pointTypes`
      ),
      pointDirections: enumArray(
        capabilities.pointDirections,
        Object.values(DataDirectionType),
        `${path}.capabilities.pointDirections`
      ),
      pointFeatures: enumArray(
        capabilities.pointFeatures,
        Object.values(ControllerPointFeatureType),
        `${path}.capabilities.pointFeatures`
      ),
      connectorDataTypes: enumArray(
        capabilities.connectorDataTypes,
        Object.values(ConnectorDataType),
        `${path}.capabilities.connectorDataTypes`
      ),
      flowFunctions: enumArray(
        capabilities.flowFunctions,
        Object.values(FlowFunctionType),
        `${path}.capabilities.flowFunctions`
      ),
      executionModes: enumArray(
        capabilities.executionModes,
        Object.values(ExecutionModeType),
        `${path}.capabilities.executionModes`
      ),
      runtimeFeatures: enumArray(
        capabilities.runtimeFeatures,
        Object.values(ControllerRuntimeFeatureType),
        `${path}.capabilities.runtimeFeatures`
      )
    },
    limits: {
      maxFlows: optionalPositiveInteger(limits.maxFlows, `${path}.limits.maxFlows`),
      maxNodesPerFlow: optionalPositiveInteger(
        limits.maxNodesPerFlow,
        `${path}.limits.maxNodesPerFlow`
      ),
      maxConnectionsPerFlow: optionalPositiveInteger(
        limits.maxConnectionsPerFlow,
        `${path}.limits.maxConnectionsPerFlow`
      ),
      minimumIntervalMilliseconds: optionalPositiveInteger(
        limits.minimumIntervalMilliseconds,
        `${path}.limits.minimumIntervalMilliseconds`
      )
    },
    revision: integer(item.revision, `${path}.revision`),
    updatedAt: optionalString(item.updatedAt, `${path}.updatedAt`)
  };
};

export const parsePage = <T>(
  value: unknown,
  parseItem: (item: unknown, path: string) => T
): Page<T> => {
  const page = object(value, 'response');
  if (!Array.isArray(page.items)) throw new Error('response.items must be an array');
  return {
    items: page.items.map((item, index) => parseItem(item, `response.items[${index}]`)),
    totalItems: integer(page.totalItems, 'response.totalItems'),
    page: integer(page.page, 'response.page'),
    pageSize: integer(page.pageSize, 'response.pageSize'),
    pageCount: integer(page.pageCount, 'response.pageCount')
  };
};

export const parseControllerTemplateList = (value: unknown): ControllerTemplateSummary[] => {
  const response = object(value, 'response');
  if (!Array.isArray(response.items)) throw new Error('response.items must be an array');
  return response.items.map((item, index) =>
    parseControllerTemplate(item, `response.items[${index}]`)
  );
};
