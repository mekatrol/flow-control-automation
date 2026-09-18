import { describe, expect, it } from 'vitest';

import type { JSONSchema } from '@/components/yaml/MonacoYaml';
import { pointSourceSchema } from '@/features/pointSources/pointSourceSchema';

describe('pointSourceSchema', () => {
  type SchemaWithDefinitions = JSONSchema & { $defs?: Record<string, JSONSchema> };

  const definitions = (pointSourceSchema as SchemaWithDefinitions).$defs;
  const connectionSchema = (): JSONSchema | undefined => definitions?.connection;

  const sourceKinds = (): unknown[] =>
    pointSourceSchema.oneOf?.map((branch) => {
      if (typeof branch !== 'object') return undefined;
      const condition = branch.allOf?.[0];
      if (typeof condition !== 'object') return undefined;
      const kind = condition.properties?.kind;

      return typeof kind === 'object' ? kind.const : undefined;
    }) ?? [];

  it('accepts HTTP and HTTPS base URLs', () => {
    const connection = connectionSchema();
    const baseUrl =
      typeof connection === 'object' && !Array.isArray(connection)
        ? connection.properties?.baseUrl
        : undefined;
    const pattern =
      typeof baseUrl === 'object' && !Array.isArray(baseUrl) ? baseUrl.pattern : undefined;

    expect(pattern).toBeDefined();
    expect(new RegExp(pattern!)).toMatchObject(expect.any(RegExp));
    expect('http://lego-train.lan').toMatch(new RegExp(pattern!));
    expect('https://lego-train.lan').toMatch(new RegExp(pattern!));
    expect('ftp://lego-train.lan').not.toMatch(new RegExp(pattern!));
  });

  it('only advertises connection fields accepted by the backend contract', () => {
    const connection = connectionSchema();
    const properties =
      typeof connection === 'object' && !Array.isArray(connection)
        ? connection.properties
        : undefined;

    expect(Object.keys(properties ?? {}).sort()).toEqual(
      [
        'allowPrivateNetwork',
        'baseUrl',
        'brokerUrl',
        'cleanStart',
        'clientIdPrefix',
        'defaultPollMilliseconds',
        'followRedirects',
        'keepAliveSeconds',
        'maximumResponseBytes',
        'qos',
        'subscribeEvents',
        'testTopic'
      ].sort()
    );
  });

  it('exposes source kinds as discriminated completion branches', () => {
    expect(pointSourceSchema.allOf).toBeUndefined();
    expect(sourceKinds()).toEqual(['virtual', 'physical', 'homeAssistant', 'mqtt', 'http']);
  });

  it('narrows connection and mapping schemas from the selected kind', () => {
    const mqttBranch = pointSourceSchema.oneOf?.find((branch) => {
      if (typeof branch !== 'object') return false;
      const condition = branch.allOf?.[0];
      if (typeof condition !== 'object') return false;
      const kind = condition.properties?.kind;
      return typeof kind === 'object' && kind.const === 'mqtt';
    });
    const rules = typeof mqttBranch === 'object' ? mqttBranch.allOf?.[1] : undefined;
    const properties = typeof rules === 'object' ? rules.properties : undefined;

    expect(properties?.connection).toEqual({ $ref: '#/$defs/mqttConnection' });
    expect(properties?.mappings).toEqual({
      items: { $ref: '#/$defs/mqttMapping' }
    });
  });
});
