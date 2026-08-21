import { describe, expect, it } from 'vitest';
import {
  parseControllerTemplate,
  parsePage,
  parsePoint,
  parsePointGroup
} from '@/features/catalogues/api/catalogueDto';

const point = {
  id: 'room-temperature',
  name: 'Room temperature',
  description: 'Measured temperature',
  enabled: true,
  groupId: 'room',
  implementation: 'bound',
  direction: 'input',
  valueType: 'analog',
  units: 'deg_c',
  readable: true,
  commandable: false,
  persistence: 'volatile',
  sourceId: null,
  revision: 2,
  updatedAt: '2026-07-25T00:00:00Z'
};

const template = {
  schemaVersion: 1,
  id: 'default',
  name: 'Default',
  readOnly: true,
  capabilities: {
    pointTypes: ['analog'],
    pointDirections: ['input'],
    pointFeatures: ['read'],
    connectorDataTypes: ['number'],
    flowFunctions: ['readPoint'],
    executionModes: ['event'],
    runtimeFeatures: ['boundPoints']
  },
  limits: {
    maxFlows: null,
    maxNodesPerFlow: 10,
    maxConnectionsPerFlow: null,
    minimumIntervalMilliseconds: null
  },
  revision: 0
};

describe('catalogue DTO parsing', () => {
  /**
   * Purpose: Protects the behavioral contract that maps point, group, template and page contracts.
   * Description: Exercises maps point, group, template and page contracts from its arranged starting state and
   * verifies the observable results required by the scenario.
   */
  it('maps point, group, template and page contracts', () => {
    // Expected outcome: `parsePoint(point)` contains the required object fields.
    // Acceptance criteria: `parsePoint(point)` must match the object `{ id: 'room-temperature', groupId: 'room', valueType: 'analog' }`, because this condition proves that
    // maps point, group, template and page contracts.
    expect(parsePoint(point)).toMatchObject({
      id: 'room-temperature',
      groupId: 'room',
      valueType: 'analog'
    });
    expect(
      parsePoint({ ...point, direction: 'inputOutput', valueType: 'multiState' })
    ).toMatchObject({ direction: 'inputOutput', valueType: 'multiState' });

    // Expected outcome: `parsePointGroup({ id: 'room', name: 'Room', sourceId: null, revision: 1 })` matches the required structure.
    // Acceptance criteria: `parsePointGroup({ id: 'room', name: 'Room', sourceId: null, revision: 1 })` must equal `{ id: 'room', name: 'Room', description: undefined, sourceId: undefined, revision: 1, updatedAt: undefined }`, because this condition proves that
    // maps point, group, template and page contracts.
    expect(parsePointGroup({ id: 'room', name: 'Room', sourceId: null, revision: 1 })).toEqual({
      id: 'room',
      name: 'Room',
      description: undefined,
      sourceId: undefined,
      revision: 1,
      updatedAt: undefined
    });

    // Expected outcome: `parseControllerTemplate(template` has the required value.
    // Acceptance criteria: `parseControllerTemplate(template` must be `10`, because this condition proves that
    // maps point, group, template and page contracts.
    expect(parseControllerTemplate(template).limits.maxNodesPerFlow).toBe(10);

    // Expected outcome: `parsePage({ items: [point], totalItems: 1, page: 1, pageSize: 10, pageCount: 1 }, parsePoint` contains the required number of entries.
    // Acceptance criteria: `parsePage({ items: [point], totalItems: 1, page: 1, pageSize: 10, pageCount: 1 }, parsePoint` must contain exactly 1 entries, because this condition proves that
    // maps point, group, template and page contracts.
    expect(
      parsePage({ items: [point], totalItems: 1, page: 1, pageSize: 10, pageCount: 1 }, parsePoint)
        .items
    ).toHaveLength(1);
  });

  /**
   * Purpose: Protects the behavioral contract that rejects malformed payloads.
   * Description: Exercises rejects malformed payloads from its arranged starting state and
   * verifies the observable results required by the scenario.
   */
  it.each([
    [{ ...point, enabled: 'yes' }, /point.enabled/],
    [{ ...point, implementation: 'physical' }, /point.implementation/],
    [{ ...point, direction: 'sideways' }, /point.direction/],
    [{ ...point, valueType: 'float' }, /point.valueType/],
    [{ ...point, revision: 1.5 }, /point.revision/],
    [{ ...template, schemaVersion: 0 }, /schemaVersion/],
    [{ ...template, capabilities: { ...template.capabilities, pointTypes: [1] } }, /pointTypes/],
    [{ ...template, limits: { ...template.limits, maxFlows: 0 } }, /maxFlows/]
  ])('rejects malformed payloads', (payload, expected) => {
    const parser = 'schemaVersion' in payload ? parseControllerTemplate : parsePoint;

    // Expected outcome: The invalid operation is rejected.
    // Acceptance criteria: the operation must throw the asserted error, because this condition proves that
    // maps point, group, template and page contracts.
    expect(() => parser(payload)).toThrow(expected);
  });
});
