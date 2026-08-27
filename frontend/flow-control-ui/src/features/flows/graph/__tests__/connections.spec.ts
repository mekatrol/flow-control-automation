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
  /**
   * Purpose: Protects the behavioral contract that implements the direction and data-type compatibility matrix.
   * Description: Exercises implements the direction and data-type compatibility matrix from its arranged starting state and
   * verifies the observable results required by the scenario.
   */
  it('implements the direction and data-type compatibility matrix', () => {
    // Expected outcome: `connectorsAreCompatible(connector('output', 'number'), connector('input', 'number'))` has the required value.
    // Acceptance criteria: `connectorsAreCompatible(connector('output', 'number'), connector('input', 'number'))` must be `true`, because this condition proves that
    // implements the direction and data-type compatibility matrix.
    expect(
      connectorsAreCompatible(connector('output', 'number'), connector('input', 'number'))
    ).toBe(true);

    // Expected outcome: `connectorsAreCompatible(connector('output', 'any'), connector('input', 'string'))` has the required value.
    // Acceptance criteria: `connectorsAreCompatible(connector('output', 'any'), connector('input', 'string'))` must be `true`, because this condition proves that
    // implements the direction and data-type compatibility matrix.
    expect(connectorsAreCompatible(connector('output', 'any'), connector('input', 'string'))).toBe(
      true
    );

    // Expected outcome: `connectorsAreCompatible(connector('output', 'string'), connector('input', 'any'))` has the required value.
    // Acceptance criteria: `connectorsAreCompatible(connector('output', 'string'), connector('input', 'any'))` must be `true`, because this condition proves that
    // implements the direction and data-type compatibility matrix.
    expect(connectorsAreCompatible(connector('output', 'string'), connector('input', 'any'))).toBe(
      true
    );

    // Expected outcome: `connectorsAreCompatible(connector('input', 'number'), connector('input', 'number'))` has the required value.
    // Acceptance criteria: `connectorsAreCompatible(connector('input', 'number'), connector('input', 'number'))` must be `false`, because this condition proves that
    // implements the direction and data-type compatibility matrix.
    expect(
      connectorsAreCompatible(connector('input', 'number'), connector('input', 'number'))
    ).toBe(false);

    // Expected outcome: `connectorsAreCompatible(connector('output', 'number'), connector('output', 'number'))` has the required value.
    // Acceptance criteria: `connectorsAreCompatible(connector('output', 'number'), connector('output', 'number'))` must be `false`, because this condition proves that
    // implements the direction and data-type compatibility matrix.
    expect(
      connectorsAreCompatible(connector('output', 'number'), connector('output', 'number'))
    ).toBe(false);

    // Expected outcome: `connectorsAreCompatible(connector('output', 'number'), connector('input', 'string'))` has the required value.
    // Acceptance criteria: `connectorsAreCompatible(connector('output', 'number'), connector('input', 'string'))` must be `false`, because this condition proves that
    // implements the direction and data-type compatibility matrix.
    expect(
      connectorsAreCompatible(connector('output', 'number'), connector('input', 'string'))
    ).toBe(false);
  });

  /**
   * Purpose: Protects the behavioral contract that accepts a valid connection and creates plain endpoint data.
   * Description: Exercises accepts a valid connection and creates plain endpoint data from its arranged starting state and
   * verifies the observable results required by the scenario.
   */
  it('accepts a valid connection and creates plain endpoint data', () => {
    const flow = structuredClone(sampleFlows[0]!);
    const result = addConnection(
      flow,
      { nodeId: 'temperature-average', connectorId: 'output' },
      { nodeId: 'manual-override', connectorId: 'input' },
      'new-connection'
    );

    // Expected outcome: `result` matches the required structure.
    // Acceptance criteria: `result` must equal `{ connection: { id: 'new-connection', start: { nodeId: 'temperature-average', connectorId: 'output' }, end: { nodeId: 'm`, because this condition proves that
    // accepts a valid connection and creates plain endpoint data.
    expect(result).toEqual({
      connection: {
        id: 'new-connection',
        start: { nodeId: 'temperature-average', connectorId: 'output' },
        end: { nodeId: 'manual-override', connectorId: 'input' }
      }
    });
  });

  it('rejects a second input driver but permits output fan-out', () => {
    const flow = structuredClone(sampleFlows[0]!);

    expect(
      validateConnection(
        flow,
        { nodeId: 'zone-split', connectorId: 'output' },
        { nodeId: 'comfort-pulse', connectorId: 'input' }
      ).message
    ).toMatch(/already has a connection/);

    expect(
      validateConnection(
        flow,
        { nodeId: 'temperature-average', connectorId: 'output' },
        { nodeId: 'zone-split', connectorId: 'input' }
      )
    ).toEqual({ valid: true });
  });

  /**
   * Purpose: Protects the behavioral contract that rejects duplicate, self, missing, wrong-direction, and incompatible links.
   * Description: Exercises rejects duplicate, self, missing, wrong-direction, and incompatible links from its arranged starting state and
   * verifies the observable results required by the scenario.
   */
  it('rejects duplicate, self, missing, wrong-direction, and incompatible links', () => {
    const flow = structuredClone(sampleFlows[0]!);

    // Expected outcome: `validateConnection( flow, { nodeId: 'temperature-average', connectorId: 'output' }, { nodeId: 'comfo` follows the required pattern.
    // Acceptance criteria: `validateConnection( flow, { nodeId: 'temperature-average', connectorId: 'output' }, { nodeId: 'comfo` must match `/already exists/`, because this condition proves that
    // rejects duplicate, self, missing, wrong-direction, and incompatible links.
    expect(
      validateConnection(
        flow,
        { nodeId: 'temperature-average', connectorId: 'output' },
        { nodeId: 'comfort-pulse', connectorId: 'input' }
      ).message
    ).toMatch(/already exists/);

    // Expected outcome: `validateConnection( flow, { nodeId: 'temperature-average', connectorId: 'output' }, { nodeId: 'tempe` follows the required pattern.
    // Acceptance criteria: `validateConnection( flow, { nodeId: 'temperature-average', connectorId: 'output' }, { nodeId: 'tempe` must match `/itself/`, because this condition proves that
    // rejects duplicate, self, missing, wrong-direction, and incompatible links.
    expect(
      validateConnection(
        flow,
        { nodeId: 'temperature-average', connectorId: 'output' },
        { nodeId: 'temperature-average', connectorId: 'input' }
      ).message
    ).toMatch(/itself/);

    // Expected outcome: `validateConnection( flow, { nodeId: 'missing', connectorId: 'output' }, { nodeId: 'comfort-pulse', c` follows the required pattern.
    // Acceptance criteria: `validateConnection( flow, { nodeId: 'missing', connectorId: 'output' }, { nodeId: 'comfort-pulse', c` must match `/no longer exists/`, because this condition proves that
    // rejects duplicate, self, missing, wrong-direction, and incompatible links.
    expect(
      validateConnection(
        flow,
        { nodeId: 'missing', connectorId: 'output' },
        { nodeId: 'comfort-pulse', connectorId: 'input' }
      ).message
    ).toMatch(/no longer exists/);

    // Expected outcome: `validateConnection( flow, { nodeId: 'temperature-average', connectorId: 'input' }, { nodeId: 'comfor` follows the required pattern.
    // Acceptance criteria: `validateConnection( flow, { nodeId: 'temperature-average', connectorId: 'input' }, { nodeId: 'comfor` must match `/compatible input/`, because this condition proves that
    // rejects duplicate, self, missing, wrong-direction, and incompatible links.
    expect(
      validateConnection(
        flow,
        { nodeId: 'temperature-average', connectorId: 'input' },
        { nodeId: 'comfort-pulse', connectorId: 'input' }
      ).message
    ).toMatch(/compatible input/);

    flow.nodes[1]!.connectors[0]!.dataType = 'string';

    // Expected outcome: `validateConnection( flow, { nodeId: 'temperature-average', connectorId: 'output' }, { nodeId: 'comfo` follows the required pattern.
    // Acceptance criteria: `validateConnection( flow, { nodeId: 'temperature-average', connectorId: 'output' }, { nodeId: 'comfo` must match `/compatible input/`, because this condition proves that
    // rejects duplicate, self, missing, wrong-direction, and incompatible links.
    expect(
      validateConnection(
        flow,
        { nodeId: 'temperature-average', connectorId: 'output' },
        { nodeId: 'comfort-pulse', connectorId: 'input' }
      ).message
    ).toMatch(/compatible input/);
  });
});
