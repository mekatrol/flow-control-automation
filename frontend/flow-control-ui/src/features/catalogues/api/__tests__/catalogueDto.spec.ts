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
    flowFunctions: ['read-point'],
    executionModes: ['event'],
    runtimeFeatures: ['bound_points']
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
  it('maps point, group, template and page contracts', () => {
    expect(parsePoint(point)).toMatchObject({
      id: 'room-temperature',
      groupId: 'room',
      valueType: 'analog'
    });
    expect(parsePointGroup({ id: 'room', name: 'Room', sourceId: null, revision: 1 })).toEqual({
      id: 'room',
      name: 'Room',
      description: undefined,
      sourceId: undefined,
      revision: 1,
      updatedAt: undefined
    });
    expect(parseControllerTemplate(template).limits.maxNodesPerFlow).toBe(10);
    expect(
      parsePage({ items: [point], totalItems: 1, page: 1, pageSize: 10, pageCount: 1 }, parsePoint)
        .items
    ).toHaveLength(1);
  });

  it.each([
    [{ ...point, enabled: 'yes' }, /point.enabled/],
    [{ ...point, implementation: 'physical' }, /point.implementation/],
    [{ ...point, direction: 'sideways' }, /point.direction/],
    [{ ...point, valueType: 'float' }, /point.valueType/],
    [{ ...point, revision: 1.5 }, /point.revision/],
    [{ ...template, schemaVersion: 2 }, /schemaVersion/],
    [{ ...template, capabilities: { ...template.capabilities, pointTypes: [1] } }, /pointTypes/],
    [{ ...template, limits: { ...template.limits, maxFlows: 0 } }, /maxFlows/]
  ])('rejects malformed payloads', (payload, expected) => {
    const parser = 'schemaVersion' in payload ? parseControllerTemplate : parsePoint;
    expect(() => parser(payload)).toThrow(expected);
  });
});
