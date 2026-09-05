import { describe, expect, it } from 'vitest';

import type { ControllerTemplateSummary } from '@/features/catalogues/api/catalogueDto';
import { getFlowDebugTargets, isControllerDebugCompatible } from '@/features/flows/debugTargets';

const template = (
  overrides: Partial<ControllerTemplateSummary> = {}
): ControllerTemplateSummary => ({
  schemaVersion: 1,
  id: 'kc868-a16',
  name: 'KC868-A16',
  readOnly: false,
  capabilities: {
    pointTypes: ['digital'],
    pointDirections: ['input', 'output'],
    pointFeatures: ['read', 'command'],
    connectorDataTypes: ['boolean'],
    flowFunctions: ['and', 'not', 'or', 'readPoint', 'writePoint'],
    executionModes: ['interval'],
    runtimeFeatures: ['physicalPoints']
  },
  limits: {},
  revision: 3,
  ...overrides
});

describe('flow debug targets', () => {
  it('offers server, emulator, and compatible configured controllers', () => {
    const targets = getFlowDebugTargets([
      template(),
      template({ id: 'default', name: 'Default host' }),
      template({
        id: 'analog-only',
        name: 'Analog only',
        capabilities: { ...template().capabilities, pointTypes: ['analog'] }
      })
    ]);

    expect(targets).toEqual([
      {
        id: 'server',
        kind: 'server',
        label: 'Server',
        controllerTemplateId: 'default',
        controllerTemplateRevision: 1
      },
      {
        id: 'emulator:kc868-a16',
        kind: 'emulator',
        label: 'Emulator — KC868-A16',
        controllerTemplateId: 'kc868-a16',
        controllerTemplateRevision: 3
      },
      {
        id: 'controller:kc868-a16',
        kind: 'controller',
        label: 'KC868-A16',
        controllerTemplateId: 'kc868-a16',
        controllerTemplateRevision: 3
      }
    ]);
  });

  it('requires every schema-one hardware capability', () => {
    expect(isControllerDebugCompatible(template())).toBe(true);
    expect(
      isControllerDebugCompatible(
        template({
          capabilities: {
            ...template().capabilities,
            flowFunctions: ['and', 'not', 'or', 'readPoint']
          }
        })
      )
    ).toBe(false);
  });
});
