import { describe, expect, it } from 'vitest';
import { parseControllerTemplate } from '@/features/controllerTemplates/api/controllerTemplateDto';

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
    runtimeFeatures: ['physicalPoints']
  },
  limits: {
    maxFlows: null,
    maxNodesPerFlow: 10,
    maxConnectionsPerFlow: null,
    minimumIntervalMilliseconds: null
  },
  revision: 0
};

describe('controller template DTO parsing', () => {
  it('maps a controller template', () => {
    expect(parseControllerTemplate(template).limits.maxNodesPerFlow).toBe(10);
  });

  it.each([
    [{ ...template, schemaVersion: 0 }, /schemaVersion/],
    [{ ...template, capabilities: { ...template.capabilities, pointTypes: [1] } }, /pointTypes/],
    [{ ...template, limits: { ...template.limits, maxFlows: 0 } }, /maxFlows/]
  ])('rejects malformed payloads', (payload, expected) => {
    expect(() => parseControllerTemplate(payload)).toThrow(expected);
  });
});
