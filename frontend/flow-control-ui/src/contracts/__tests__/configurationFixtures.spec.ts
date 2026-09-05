import { parseDocument } from 'yaml';
import { describe, expect, it } from 'vitest';

import constrainedJson from '@contracts/controllers/constrained.v1.normalized.json';
import constrainedYaml from '@contracts/controllers/constrained.v1.yaml?raw';
import defaultJson from '@contracts/controllers/default.v1.normalized.json';
import defaultYaml from '@contracts/controllers/default.v1.yaml?raw';
import aliasYaml from '@contracts/controllers/invalid/alias.yaml?raw';
import syntaxYaml from '@contracts/controllers/invalid/syntax.yaml?raw';
import unsupportedSchemaYaml from '@contracts/controllers/invalid/unsupported-schema.yaml?raw';
import unknownControllerFieldYaml from '@contracts/controllers/invalid/unknown-field.yaml?raw';
import unknownSourceFieldYaml from '@contracts/point-sources/invalid/unknown-field.yaml?raw';
import sourcesJson from '@contracts/point-sources/v1.normalized.json';
import sourcesYaml from '@contracts/point-sources/v1.yaml?raw';
import unknownPointFieldYaml from '@contracts/points/invalid/unknown-field.yaml?raw';
import pointsJson from '@contracts/points/v1.normalized.json';
import pointsYaml from '@contracts/points/v1.yaml?raw';

type Configuration = Record<string, unknown>;

const pointFields = new Set([
  'id',
  'name',
  'description',
  'enabled',
  'groupId',
  'pointSourceType',
  'direction',
  'valueType',
  'units',
  'stateLabels',
  'readable',
  'commandable',
  'persistence',
  'relinquishDefault',
  'sourceId',
  'mapping',
  'limits',
  'safeDisablePolicy'
]);
const sourceFields = new Set([
  'id',
  'name',
  'description',
  'enabled',
  'kind',
  'connection',
  'credentialRef',
  'tls',
  'timeouts'
]);
const controllerFields = new Set([
  'schemaVersion',
  'id',
  'name',
  'description',
  'readOnly',
  'revision',
  'capabilities',
  'limits'
]);

const parseStrictFixture = (
  source: string,
  kind: 'points' | 'sources' | 'controller'
): Configuration => {
  const document = parseDocument(source, { uniqueKeys: true });
  if (document.errors.length > 0) throw document.errors[0];
  const value = document.toJS({ maxAliasCount: 0 }) as Configuration;
  if (value.schemaVersion !== 1) throw new Error('schemaVersion must be 1');
  if (kind === 'points') {
    for (const point of value.points as Configuration[]) {
      const unknown = Object.keys(point).find((field) => !pointFields.has(field));
      if (unknown) throw new Error(`unknown field “${unknown}”`);
    }
  }
  if (kind === 'sources') {
    for (const pointSource of value.sources as Configuration[]) {
      const unknown = Object.keys(pointSource).find((field) => !sourceFields.has(field));
      if (unknown) throw new Error(`unknown field “${unknown}”`);
    }
  }
  if (kind === 'controller') {
    const unknown = Object.keys(value).find((field) => !controllerFields.has(field));
    if (unknown) throw new Error(`unknown field “${unknown}”`);
  }
  return value;
};

const withoutBackendMetadata = (value: unknown): unknown => {
  if (Array.isArray(value)) return value.map(withoutBackendMetadata);
  if (typeof value !== 'object' || value === null) return value;
  const preserveRevision = Object.hasOwn(value, 'capabilities');
  return Object.fromEntries(
    Object.entries(value)
      .filter(
        ([key]) =>
          !['createdAt', 'updatedAt'].includes(key) && (key !== 'revision' || preserveRevision)
      )
      .map(([key, child]) => [key, withoutBackendMetadata(child)])
  );
};

const asStoredTemplate = (configuration: Configuration): Configuration => {
  const { schemaVersion, ...template } = configuration;
  return { schemaVersion, templates: [template] };
};

describe('version 1 configuration fixtures', () => {
  /**
   * Purpose: Protects the behavioral contract that %s controller YAML agrees with typed normalized capabilities.
   * Description: Exercises %s controller YAML agrees with typed normalized capabilities from its arranged starting state and
   * verifies the observable results required by the scenario.
   */
  it.each([
    ['points', pointsYaml, pointsJson, 'points'],
    ['point sources', sourcesYaml, sourcesJson, 'sources']
  ] as const)('%s YAML agrees with normalized JSON', (_name, yaml, json, kind) => {
    // Expected outcome: `parseStrictFixture(yaml, kind)` matches the required structure.
    // Acceptance criteria: `parseStrictFixture(yaml, kind)` must equal `withoutBackendMetadata(json`, because this condition proves that
    // the arranged test scenario.
    expect(parseStrictFixture(yaml, kind)).toEqual(withoutBackendMetadata(json));
  });

  /**
   * Purpose: Protects the behavioral contract that %s controller YAML agrees with typed normalized capabilities.
   * Description: Exercises %s controller YAML agrees with typed normalized capabilities from its arranged starting state and
   * verifies the observable results required by the scenario.
   */
  it.each([
    ['default', defaultYaml, defaultJson],
    ['constrained', constrainedYaml, constrainedJson]
  ])('%s controller YAML agrees with typed normalized capabilities', (_name, yaml, json) => {
    // Expected outcome: `asStoredTemplate(parseStrictFixture(yaml, 'controller'))` matches the required structure.
    // Acceptance criteria: `asStoredTemplate(parseStrictFixture(yaml, 'controller'))` must equal `withoutBackendMetadata(json`, because this condition proves that
    // the arranged test scenario.
    expect(asStoredTemplate(parseStrictFixture(yaml, 'controller'))).toEqual(
      withoutBackendMetadata(json)
    );
  });

  /**
   * Purpose: Protects the behavioral contract that the declared test scenario.
   * Description: Exercises the declared test scenario from its arranged starting state and
   * verifies the observable results required by the scenario.
   */
  it.each([
    ['unknown point field', unknownPointFieldYaml, 'points', /unknown field/],
    ['unknown source field', unknownSourceFieldYaml, 'sources', /unknown field/],
    ['unknown controller field', unknownControllerFieldYaml, 'controller', /unknown field/],
    ['unsupported schema', unsupportedSchemaYaml, 'controller', /schemaVersion/],
    ['YAML alias', aliasYaml, 'controller', /[Aa]lias/],
    ['invalid YAML syntax', syntaxYaml, 'controller', /[Ff]low sequence|[Ff]low collection/]
  ] as const)('rejects %s', (_name, yaml, kind, diagnostic) => {
    // Expected outcome: The invalid operation is rejected.
    // Acceptance criteria: the operation must throw the asserted error, because this condition proves that
    // the arranged test scenario.
    expect(() => parseStrictFixture(yaml, kind)).toThrow(diagnostic);
  });
});
