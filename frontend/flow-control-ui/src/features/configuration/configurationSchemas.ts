import {
  ControllerPointFeatureType,
  AutomationPointValueType,
  ConnectorDataType,
  DataDirectionType,
  FlowFunctionType,
  ExecutionModeType,
  ControllerRuntimeFeatureType
} from '@/types/serverTypes';
import type { JSONSchema } from '@/components/yaml/MonacoYaml';

const identifier = { type: 'string', pattern: '^[a-z0-9]+(?:-[a-z0-9]+)*$' } as const;

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
