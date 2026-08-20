export type PointImplementation = 'virtual' | 'bound';
export type PointDirection = 'input' | 'output' | 'inputOutput' | 'value';
export type PointValueType = 'analog' | 'digital' | 'multiState' | 'integer' | 'text';

export interface PointSummary {
  id: string;
  name: string;
  description?: string;
  enabled: boolean;
  groupId?: string;
  implementation: PointImplementation;
  direction: PointDirection;
  valueType: PointValueType;
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
    pointTypes: string[];
    pointDirections: string[];
    pointFeatures: string[];
    connectorDataTypes: string[];
    flowFunctions: string[];
    executionModes: string[];
    runtimeFeatures: string[];
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

const stringArray = (value: unknown, path: string): string[] => {
  if (!Array.isArray(value)) throw new Error(`${path} must be an array`);
  return value.map((item, index) => string(item, `${path}[${index}]`));
};

const optionalPositiveInteger = (value: unknown, path: string): number | undefined => {
  if (value === undefined || value === null) return undefined;
  const parsed = integer(value, path);
  if (parsed === 0) throw new Error(`${path} must be positive`);
  return parsed;
};

export const parsePoint = (value: unknown, path = 'point'): PointSummary => {
  const item = object(value, path);
  enumeration(item.persistence, ['volatile', 'retained'], `${path}.persistence`);
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
    implementation: enumeration(
      item.implementation,
      ['virtual', 'bound'],
      `${path}.implementation`
    ),
    direction: enumeration(
      item.direction,
      ['input', 'output', 'inputOutput', 'value'],
      `${path}.direction`
    ),
    valueType: enumeration(
      item.valueType,
      ['analog', 'digital', 'multiState', 'integer', 'text'],
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
      pointTypes: stringArray(capabilities.pointTypes, `${path}.capabilities.pointTypes`),
      pointDirections: stringArray(
        capabilities.pointDirections,
        `${path}.capabilities.pointDirections`
      ),
      pointFeatures: stringArray(capabilities.pointFeatures, `${path}.capabilities.pointFeatures`),
      connectorDataTypes: stringArray(
        capabilities.connectorDataTypes,
        `${path}.capabilities.connectorDataTypes`
      ),
      flowFunctions: stringArray(capabilities.flowFunctions, `${path}.capabilities.flowFunctions`),
      executionModes: stringArray(
        capabilities.executionModes,
        `${path}.capabilities.executionModes`
      ),
      runtimeFeatures: stringArray(
        capabilities.runtimeFeatures,
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
