import { parseDocument } from 'yaml';
import { describe, expect, it } from 'vitest';

import haJson from '@contracts/point-sources/valid/home-assistant.v1.normalized.json';
import haYaml from '@contracts/point-sources/valid/home-assistant.v1.yaml?raw';
import httpJson from '@contracts/point-sources/valid/http-json.v1.normalized.json';
import httpYaml from '@contracts/point-sources/valid/http-json.v1.yaml?raw';
import mqttJson from '@contracts/point-sources/valid/mqtt.v1.normalized.json';
import mqttYaml from '@contracts/point-sources/valid/mqtt.v1.yaml?raw';
import physicalJson from '@contracts/point-sources/valid/physical.v1.normalized.json';
import physicalYaml from '@contracts/point-sources/valid/physical.v1.yaml?raw';
import virtualJson from '@contracts/point-sources/valid/virtual.v1.normalized.json';
import virtualYaml from '@contracts/point-sources/valid/virtual.v1.yaml?raw';
import inlineMapping from '@contracts/point-sources/invalid/inline-mapping.yaml?raw';
import oldSources from '@contracts/point-sources/invalid/old-sources-wrapper.yaml?raw';
import pointSourceType from '@contracts/point-sources/invalid/point-source-type.yaml?raw';
import sourceId from '@contracts/point-sources/invalid/source-id.yaml?raw';
import standalonePoints from '@contracts/point-sources/invalid/standalone-points.yaml?raw';

const rootFields = new Set([
  'schemaVersion',
  'id',
  'name',
  'description',
  'enabled',
  'kind',
  'connection',
  'credentialRef',
  'tls',
  'timeouts',
  'mappings',
  'points'
]);
const pointFields = new Set([
  'id',
  'name',
  'description',
  'enabled',
  'direction',
  'valueType',
  'units',
  'stateLabels',
  'readable',
  'commandable',
  'persistence',
  'relinquishDefault',
  'mapping',
  'limits',
  'safeDisablePolicy'
]);

const parseStrict = (text: string): Record<string, unknown> => {
  const document = parseDocument(text, { uniqueKeys: true });
  if (document.errors.length > 0) throw document.errors[0];
  const value = document.toJS({ maxAliasCount: 0 }) as Record<string, unknown>;
  if (value.schemaVersion !== 1) throw new Error('schemaVersion must be 1');
  const unknownRoot = Object.keys(value).find((field) => !rootFields.has(field));
  if (unknownRoot) throw new Error(`unknown field ${unknownRoot}`);
  if (!Array.isArray(value.mappings) || !Array.isArray(value.points))
    throw new Error('aggregate collections are required');
  for (const point of value.points as Record<string, unknown>[]) {
    const unknown = Object.keys(point).find((field) => !pointFields.has(field));
    if (unknown) throw new Error(`unknown field ${unknown}`);
    if (typeof point.mapping !== 'string' || point.mapping.split('/').length !== 2)
      throw new Error('mapping path is invalid');
  }
  return value;
};

describe('point-source version 1 fixtures', () => {
  it.each([
    ['virtual', virtualYaml, virtualJson],
    ['physical', physicalYaml, physicalJson],
    ['Home Assistant', haYaml, haJson],
    ['MQTT', mqttYaml, mqttJson],
    ['HTTP/JSON', httpYaml, httpJson]
  ])('%s YAML agrees with normalized JSON', (_name, yaml, json) => {
    expect(parseStrict(yaml as string)).toEqual(json);
  });

  it.each([oldSources, standalonePoints, pointSourceType, sourceId, inlineMapping])(
    'rejects a removed shape',
    (yaml) => {
      expect(() => parseStrict(yaml)).toThrow(/.+/);
    }
  );
});
