import type { JSONSchema } from '@/components/yaml/MonacoYaml';

/* oxlint-disable unicorn/no-thenable -- `then` is a required JSON Schema conditional keyword. */

const connectionProperties: Record<string, JSONSchema> = {
  baseUrl: { type: 'string', pattern: '^https://', description: 'HTTPS base URL for the service.' },
  subscribeEvents: { type: 'boolean' },
  brokerUrl: {
    type: 'string',
    pattern: '^mqtts?://',
    description: 'MQTT broker URL, including mqtt:// or mqtts:// and its port.'
  },
  clientIdPrefix: { type: 'string', minLength: 1 },
  testTopic: {
    type: 'string',
    minLength: 1,
    pattern: '^[^+#\\u0000]+$',
    description: 'Exact read-only topic used to verify subscription access.'
  },
  allowPrivateNetwork: {
    type: 'boolean',
    description: 'Explicitly permit private LAN destinations. Loopback remains blocked.'
  },
  qos: { type: 'integer', enum: [0, 1, 2] },
  cleanStart: { type: 'boolean' },
  keepAliveSeconds: { type: 'integer', minimum: 1 },
  allowedReadMethods: {
    type: 'array',
    minItems: 1,
    uniqueItems: true,
    items: { enum: ['GET', 'HEAD'] }
  },
  defaultPollMilliseconds: { type: 'integer', minimum: 100 },
  followRedirects: { type: 'boolean' },
  maximumResponseBytes: { type: 'integer', minimum: 1, maximum: 10485760 }
};

export const pointSourceSchema: JSONSchema = {
  $id: 'app://schemas/point-source-v1.json',
  type: 'object',
  additionalProperties: false,
  required: ['schemaVersion', 'sources'],
  properties: {
    schemaVersion: { const: 1, description: 'Configuration schema version.' },
    sources: {
      type: 'array',
      minItems: 1,
      maxItems: 1,
      items: {
        type: 'object',
        additionalProperties: false,
        required: ['id', 'name', 'enabled', 'kind', 'connection', 'tls', 'timeouts'],
        properties: {
          id: { type: 'string', pattern: '^[a-z0-9]+(?:-[a-z0-9]+)*$' },
          name: { type: 'string', minLength: 1 },
          description: { type: 'string' },
          enabled: { type: 'boolean' },
          kind: { enum: ['home_assistant', 'mqtt', 'http_json'] },
          connection: {
            type: 'object',
            additionalProperties: false,
            properties: connectionProperties
          },
          credentialRef: {
            type: 'string',
            pattern: '^(secret://|env:).+',
            description: 'Write-only credential reference; never place a secret directly in YAML.'
          },
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
          }
        },
        allOf: [
          {
            if: { properties: { kind: { const: 'mqtt' } }, required: ['kind'] },
            then: {
              properties: {
                connection: {
                  required: ['brokerUrl', 'qos']
                }
              }
            }
          },
          {
            if: {
              properties: { kind: { enum: ['home_assistant', 'http_json'] } },
              required: ['kind']
            },
            then: {
              properties: {
                connection: {
                  required: ['baseUrl']
                }
              }
            }
          },
          {
            if: { properties: { kind: { const: 'http_json' } }, required: ['kind'] },
            then: {
              properties: {
                connection: {
                  required: ['allowedReadMethods', 'maximumResponseBytes']
                }
              }
            }
          }
        ]
      }
    }
  }
};
