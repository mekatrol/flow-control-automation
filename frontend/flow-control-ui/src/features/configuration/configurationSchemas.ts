import type { JSONSchema } from '@/components/yaml/MonacoYaml';

const identifier = { type: 'string', pattern: '^[a-z0-9]+(?:-[a-z0-9]+)*$' } as const;

export const pointSchema: JSONSchema = {
  $id: 'app://schemas/point-v1.json',
  type: 'object',
  additionalProperties: false,
  required: ['schemaVersion', 'groups', 'points'],
  properties: {
    schemaVersion: { const: 1 },
    groups: { type: 'array', maxItems: 0 },
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
          'implementation',
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
          groupId: identifier,
          implementation: { enum: ['virtual', 'bound'] },
          direction: { enum: ['input', 'output', 'input_output', 'value'] },
          valueType: { enum: ['analog', 'digital', 'multi_state', 'integer', 'text'] },
          units: { type: 'string' },
          readable: { type: 'boolean' },
          commandable: { type: 'boolean' },
          persistence: { enum: ['volatile', 'retained'] },
          sourceId: identifier,
          mapping: { type: 'object' },
          limits: { type: 'object' },
          stateLabels: {},
          relinquishDefault: {},
          safeDisablePolicy: { type: 'object' }
        }
      }
    }
  }
};

export const pointGroupSchema: JSONSchema = {
  $id: 'app://schemas/point-group-v1.json',
  type: 'object',
  additionalProperties: false,
  required: ['schemaVersion', 'groups', 'points'],
  properties: {
    schemaVersion: { const: 1 },
    groups: {
      type: 'array',
      minItems: 1,
      maxItems: 1,
      items: {
        type: 'object',
        required: ['id', 'name'],
        properties: {
          id: identifier,
          name: { type: 'string', minLength: 1 },
          description: { type: 'string' },
          sourceId: identifier,
          mappingDefaults: { type: 'object' }
        }
      }
    },
    points: { type: 'array', maxItems: 0 }
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
      ]
    },
    limits: { type: 'object' }
  }
};
