import { describe, expect, it } from 'vitest';

import { sampleFlows } from '@/features/flows/__tests__/fixtures/sampleFlows';
import type { FlowNodeConnector } from '@/features/flows/types';
import {
  addConnection,
  connectorsAreCompatible,
  validateConnection
} from '@/features/flows/graph/connections';

const connector = (
  direction: FlowNodeConnector['direction'],
  dataType: FlowNodeConnector['dataType']
): FlowNodeConnector => ({
  id: `${direction}-${dataType}`,
  label: 'Test',
  direction,
  dataType,
  side: 'left'
});

describe('connection graph operations', () => {
  it('implements the direction and data-type compatibility matrix', () => {
    expect(
      connectorsAreCompatible(connector('output', 'number'), connector('input', 'number'))
    ).toBe(true);
    expect(connectorsAreCompatible(connector('output', 'any'), connector('input', 'string'))).toBe(
      true
    );
    expect(connectorsAreCompatible(connector('output', 'string'), connector('input', 'any'))).toBe(
      true
    );
    expect(
      connectorsAreCompatible(connector('input', 'number'), connector('input', 'number'))
    ).toBe(false);
    expect(
      connectorsAreCompatible(connector('output', 'number'), connector('output', 'number'))
    ).toBe(false);
    expect(
      connectorsAreCompatible(connector('output', 'number'), connector('input', 'string'))
    ).toBe(false);
  });

  it('accepts a valid connection and creates plain endpoint data', () => {
    const flow = structuredClone(sampleFlows[0]!);
    const result = addConnection(
      flow,
      { nodeId: 'temperature-average', connectorId: 'output' },
      { nodeId: 'manual-override', connectorId: 'input' },
      'new-connection'
    );

    expect(result).toEqual({
      connection: {
        id: 'new-connection',
        start: { nodeId: 'temperature-average', connectorId: 'output' },
        end: { nodeId: 'manual-override', connectorId: 'input' }
      }
    });
  });

  it('rejects duplicate, self, missing, wrong-direction, and incompatible links', () => {
    const flow = structuredClone(sampleFlows[0]!);
    expect(
      validateConnection(
        flow,
        { nodeId: 'temperature-average', connectorId: 'output' },
        { nodeId: 'comfort-pulse', connectorId: 'input' }
      ).message
    ).toMatch(/already exists/);
    expect(
      validateConnection(
        flow,
        { nodeId: 'temperature-average', connectorId: 'output' },
        { nodeId: 'temperature-average', connectorId: 'input' }
      ).message
    ).toMatch(/itself/);
    expect(
      validateConnection(
        flow,
        { nodeId: 'missing', connectorId: 'output' },
        { nodeId: 'comfort-pulse', connectorId: 'input' }
      ).message
    ).toMatch(/no longer exists/);
    expect(
      validateConnection(
        flow,
        { nodeId: 'temperature-average', connectorId: 'input' },
        { nodeId: 'comfort-pulse', connectorId: 'input' }
      ).message
    ).toMatch(/compatible input/);

    flow.nodes[1]!.connectors[0]!.dataType = 'string';
    expect(
      validateConnection(
        flow,
        { nodeId: 'temperature-average', connectorId: 'output' },
        { nodeId: 'comfort-pulse', connectorId: 'input' }
      ).message
    ).toMatch(/compatible input/);
  });
});
