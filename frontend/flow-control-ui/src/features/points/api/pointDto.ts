import { VirtualPointPersistenceType } from '@/types/serverTypes';
import { PointSourceType, DataDirectionType, AutomationPointValueType } from '@/types/serverTypes';
export { PointSourceType, DataDirectionType, AutomationPointValueType } from '@/types/serverTypes';

export interface PointSummary {
  id: string;
  name: string;
  description?: string;
  enabled: boolean;
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
