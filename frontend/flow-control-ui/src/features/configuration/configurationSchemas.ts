import {
  ControllerPointFeatureType,
  ConnectorDataType,
  FlowFunctionType,
  ExecutionModeType,
  ControllerRuntimeFeatureType
} from '@/types/serverTypes';
import {
  AutomationPointValueType,
  DataDirectionType,
  VirtualPointPersistenceType,
  PointSourceType
} from '@/types/serverTypes';
import type { JSONSchema } from '@/components/yaml/MonacoYaml';

const identifier = { type: 'string', pattern: '^[a-z0-9]+(?:-[a-z0-9]+)*$' } as const;

export const pointSchema: JSONSchema = {
  $id: 'app://schemas/point-v1.json',
  type: 'object',
  additionalProperties: false,
  required: ['schemaVersion', 'points'],
  properties: {
    schemaVersion: { const: 1 },
    points: {
      type: 'array',
      minItems: 1,
      maxItems: 1,
      items: {
        type: 'object',
        required: [
          'id',
          'name',
          'enabled',
          'pointSourceType',
          'direction',
          'valueType',
          'readable',
          'commandable',
          'persistence'
        ],
        properties: {
          id: identifier,
          name: { type: 'string', minLength: 1 },
          description: { type: 'string' },
          enabled: { type: 'boolean' },
          pointSourceType: { enum: Object.values(PointSourceType) },
          direction: { enum: Object.values(DataDirectionType) },
          valueType: { enum: Object.values(AutomationPointValueType) },
          units: { type: 'string' },
          readable: { type: 'boolean' },
          commandable: { type: 'boolean' },
          persistence: { enum: Object.values(VirtualPointPersistenceType) },
          sourceId: identifier,
          mapping: {
            type: 'object',
            properties: {
              path: { type: 'string', pattern: '^/(?!/)' },
              method: { enum: ['GET', 'HEAD', 'POST', 'PUT', 'PATCH'] },
              jsonPointer: { type: 'string', pattern: '^/' },
              valuePointer: { type: 'string', pattern: '^/' },
              contentType: {
                enum: ['application/json', 'application/x-www-form-urlencoded']
              }
            }
          },
          limits: { type: 'object' },
          stateLabels: {},
          relinquishDefault: {},
          safeDisablePolicy: { type: 'object' }
        }
      }
    }
  }
};

export const controllerTemplateSchema: JSONSchema = {
  $id: 'app://schemas/controller-template-v1.json',
  type: 'object',
  additionalProperties: false,
  required: ['schemaVersion', 'id', 'name', 'readOnly', 'capabilities', 'limits'],
  properties: {
    schemaVersion: { const: 1 },
    id: identifier,
    name: { type: 'string', minLength: 1 },
    description: { type: 'string' },
    readOnly: { type: 'boolean' },
    capabilities: {
      type: 'object',
      required: [
        'pointTypes',
        'pointDirections',
        'pointFeatures',
        'connectorDataTypes',
        'flowFunctions',
        'executionModes',
        'runtimeFeatures'
      ],
      properties: {
        pointTypes: { type: 'array', items: { enum: Object.values(AutomationPointValueType) } },
        pointDirections: { type: 'array', items: { enum: Object.values(DataDirectionType) } },
        pointFeatures: {
          type: 'array',
          items: { enum: Object.values(ControllerPointFeatureType) }
        },
        connectorDataTypes: { type: 'array', items: { enum: Object.values(ConnectorDataType) } },
        flowFunctions: { type: 'array', items: { enum: Object.values(FlowFunctionType) } },
        executionModes: { type: 'array', items: { enum: Object.values(ExecutionModeType) } },
        runtimeFeatures: {
          type: 'array',
          items: { enum: Object.values(ControllerRuntimeFeatureType) }
        }
      }
    },
    limits: { type: 'object' }
  }
};
