import type { JSONSchema } from '@/components/yaml/MonacoYaml';

const identifier: JSONSchema = { type: 'string', pattern: '^[a-z][a-z0-9]*(?:-[a-z0-9]+)*$' };
const connection: JSONSchema = {
  type: 'object',
  additionalProperties: false,
  properties: {
    baseUrl: { type: 'string', pattern: '^https?://' },
    subscribeEvents: { type: 'boolean' },
    brokerUrl: { type: 'string', pattern: '^mqtts?://' },
    clientIdPrefix: { type: 'string' },
    testTopic: { type: 'string' },
    qos: { type: 'integer', minimum: 0, maximum: 2 },
    cleanStart: { type: 'boolean' },
    keepAliveSeconds: { type: 'integer', minimum: 1 },
    defaultPollMilliseconds: { type: 'integer', minimum: 100 },
    followRedirects: { type: 'boolean' },
    maximumResponseBytes: { type: 'integer', minimum: 1, maximum: 10485760 },
    allowPrivateNetwork: { type: 'boolean' }
  }
};
const operation: JSONSchema = {
  type: 'object',
  additionalProperties: false,
  properties: {
    path: { type: 'string' },
    method: { type: 'string' },
    format: { type: 'string' },
    contentType: { type: 'string' },
    template: { type: 'string' },
    topic: { type: 'string' },
    qos: { type: 'integer', minimum: 0, maximum: 2 },
    retain: { type: 'boolean' },
    entityId: { type: 'string' },
    property: { type: 'string' },
    service: { type: 'string' },
    serviceData: { type: 'object' },
    pollMilliseconds: { type: 'integer', minimum: 100 }
  }
};

export const pointSourceSchema: JSONSchema = {
  $id: 'app://schemas/point-source-v1.json',
  type: 'object',
  additionalProperties: false,
  required: [
    'schemaVersion',
    'id',
    'name',
    'enabled',
    'kind',
    'connection',
    'tls',
    'timeouts',
    'mappings',
    'points'
  ],
  properties: {
    schemaVersion: { const: 1 },
    id: identifier,
    name: { type: 'string', minLength: 1 },
    description: { type: 'string' },
    enabled: { type: 'boolean' },
    kind: { enum: ['virtual', 'physical', 'homeAssistant', 'mqtt', 'httpJson'] },
    connection,
    credentialRef: { type: 'string', pattern: '^(secret://|env:).+' },
    tls: {
      type: 'object',
      additionalProperties: false,
      required: ['verifyServerCertificate'],
      properties: { verifyServerCertificate: { const: true } }
    },
    timeouts: {
      type: 'object',
      additionalProperties: false,
      required: ['connectMilliseconds'],
      properties: {
        connectMilliseconds: { type: 'integer', minimum: 100, maximum: 30000 },
        requestMilliseconds: { type: 'integer', minimum: 100, maximum: 60000 }
      }
    },
    mappings: {
      type: 'array',
      items: {
        type: 'object',
        additionalProperties: false,
        required: ['id', 'aliases'],
        properties: {
          id: identifier,
          aliases: {
            type: 'array',
            minItems: 1,
            uniqueItems: true,
            items: { type: 'string', pattern: '^[A-Za-z][A-Za-z0-9_-]*$' }
          },
          read: operation,
          command: operation,
          physical: {
            type: 'object',
            additionalProperties: false,
            required: ['controllerId', 'channel'],
            properties: {
              controllerId: identifier,
              channel: { type: 'string', minLength: 1 },
              address: { type: 'string' },
              electricalType: { type: 'string' }
            }
          },
          virtual: {
            type: 'object',
            additionalProperties: false,
            properties: { persistence: { enum: ['volatile', 'retained'] } }
          }
        }
      }
    },
    points: {
      type: 'array',
      items: {
        type: 'object',
        additionalProperties: false,
        required: [
          'id',
          'name',
          'enabled',
          'direction',
          'valueType',
          'readable',
          'commandable',
          'persistence',
          'mapping'
        ],
        properties: {
          id: identifier,
          name: { type: 'string', minLength: 1 },
          description: { type: 'string' },
          enabled: { type: 'boolean' },
          direction: { enum: ['input', 'output', 'inputOutput', 'bidirectional', 'value'] },
          valueType: { enum: ['analog', 'digital', 'multiState', 'integer', 'text'] },
          units: { type: 'string' },
          stateLabels: {},
          readable: { type: 'boolean' },
          commandable: { type: 'boolean' },
          persistence: { enum: ['volatile', 'retained'] },
          relinquishDefault: {},
          mapping: {
            type: 'string',
            pattern: '^[a-z][a-z0-9]*(?:-[a-z0-9]+)*/[A-Za-z][A-Za-z0-9_-]*$'
          },
          limits: { type: 'object' },
          safeDisablePolicy: { type: 'object' }
        }
      }
    }
  }
};
