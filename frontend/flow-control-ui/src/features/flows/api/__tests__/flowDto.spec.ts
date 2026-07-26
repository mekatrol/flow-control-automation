import { describe, expect, it } from 'vitest';
import legacyFlows from '@contracts/flows/legacy.json';

import { sampleFlows } from '@/features/flows/__tests__/fixtures/sampleFlows';
import {
  FlowDtoValidationError,
  parseFlowDto,
  parseFlowDtoJson
} from '@/features/flows/api/flowDto';

const validFlow = (): unknown => structuredClone(sampleFlows[0]);

describe('flow DTO validation', () => {

  /**
   * Purpose: Protects the behavioral contract that loads legacy contract fixtures without changing their graph semantics.
   * Description: Exercises loads legacy contract fixtures without changing their graph semantics from its arranged starting state and
   * verifies the observable results required by the scenario.
   */
  it('loads legacy contract fixtures without changing their graph semantics', () => {
    const parsed = legacyFlows.map((flow) => parseFlowDto(flow));

    // Expected outcome: `parsed` matches the required structure.
    // Acceptance criteria: `parsed` must equal `legacyFlows`, because this condition proves that
    // loads legacy contract fixtures without changing their graph semantics.
    expect(parsed).toEqual(legacyFlows);
  });

  /**
   * Purpose: Protects the behavioral contract that accepts a valid graph payload.
   * Description: Exercises accepts a valid graph payload from its arranged starting state and
   * verifies the observable results required by the scenario.
   */
  it('accepts a valid graph payload', () => {

    // Expected outcome: `parseFlowDto(validFlow())` matches the required structure.
    // Acceptance criteria: `parseFlowDto(validFlow())` must equal `sampleFlows[0]`, because this condition proves that
    // accepts a valid graph payload.
    expect(parseFlowDto(validFlow())).toEqual(sampleFlows[0]);
  });

  /**
   * Purpose: Protects the behavioral contract that migrates the legacy invert node kind to not.
   * Description: Exercises migrates the legacy invert node kind to not from its arranged starting state and
   * verifies the observable results required by the scenario.
   */
  it('migrates the legacy invert node kind to not', () => {
    const payload = validFlow() as (typeof sampleFlows)[number];
    (payload.nodes[0] as unknown as { kind: string }).kind = 'invert';

    // Expected outcome: `parseFlowDto(payload` has the required value.
    // Acceptance criteria: `parseFlowDto(payload` must be `'not'`, because this condition proves that
    // migrates the legacy invert node kind to not.
    expect(parseFlowDto(payload).nodes[0]?.kind).toBe('not');
  });

  /**
   * Purpose: Protects the behavioral contract that rejects a connection whose node does not exist.
   * Description: Exercises rejects a connection whose node does not exist from its arranged starting state and
   * verifies the observable results required by the scenario.
   */
  it('rejects a connection whose node does not exist', () => {
    const payload = validFlow() as (typeof sampleFlows)[number];
    payload.connections[0]!.end.nodeId = 'missing';

    // Expected outcome: The invalid operation is rejected.
    // Acceptance criteria: the operation must throw the asserted error, because this condition proves that
    // rejects a connection whose node does not exist.
    expect(() => parseFlowDto(payload)).toThrow(/unknown node “missing”/);
  });

  /**
   * Purpose: Protects the behavioral contract that rejects duplicate node, connector, and connection IDs.
   * Description: Exercises rejects duplicate node, connector, and connection IDs from its arranged starting state and
   * verifies the observable results required by the scenario.
   */
  it('rejects duplicate node, connector, and connection IDs', () => {
    const duplicateNode = validFlow() as (typeof sampleFlows)[number];
    duplicateNode.nodes[1]!.id = duplicateNode.nodes[0]!.id;

    // Expected outcome: The invalid operation is rejected.
    // Acceptance criteria: the operation must throw the asserted error, because this condition proves that
    // rejects duplicate node, connector, and connection IDs.
    expect(() => parseFlowDto(duplicateNode)).toThrow(/duplicate id/);

    const duplicateConnector = validFlow() as (typeof sampleFlows)[number];
    duplicateConnector.nodes[0]!.connectors[1]!.id = duplicateConnector.nodes[0]!.connectors[0]!.id;

    // Expected outcome: The invalid operation is rejected.
    // Acceptance criteria: the operation must throw the asserted error, because this condition proves that
    // rejects duplicate node, connector, and connection IDs.
    expect(() => parseFlowDto(duplicateConnector)).toThrow(/duplicate id/);

    const duplicateConnection = validFlow() as (typeof sampleFlows)[number];
    duplicateConnection.connections[1]!.id = duplicateConnection.connections[0]!.id;

    // Expected outcome: The invalid operation is rejected.
    // Acceptance criteria: the operation must throw the asserted error, because this condition proves that
    // rejects duplicate node, connector, and connection IDs.
    expect(() => parseFlowDto(duplicateConnection)).toThrow(/duplicate id/);
  });

  /**
   * Purpose: Protects the behavioral contract that rejects invalid connector directions and incompatible data types.
   * Description: Exercises rejects invalid connector directions and incompatible data types from its arranged starting state and
   * verifies the observable results required by the scenario.
   */
  it('rejects invalid connector directions and incompatible data types', () => {
    const wrongDirection = validFlow() as (typeof sampleFlows)[number];
    wrongDirection.nodes[0]!.connectors[1]!.direction = 'input';

    // Expected outcome: The invalid operation is rejected.
    // Acceptance criteria: the operation must throw the asserted error, because this condition proves that
    // rejects invalid connector directions and incompatible data types.
    expect(() => parseFlowDto(wrongDirection)).toThrow(/must reference an output connector/);

    const wrongType = validFlow() as (typeof sampleFlows)[number];
    wrongType.nodes[1]!.connectors[0]!.dataType = 'string';

    // Expected outcome: The invalid operation is rejected.
    // Acceptance criteria: the operation must throw the asserted error, because this condition proves that
    // rejects invalid connector directions and incompatible data types.
    expect(() => parseFlowDto(wrongType)).toThrow(/are incompatible/);
  });

  /**
   * Purpose: Protects the behavioral contract that safely rejects malformed JSON.
   * Description: Exercises safely rejects malformed JSON from its arranged starting state and
   * verifies the observable results required by the scenario.
   */
  it('safely rejects malformed JSON', () => {

    // Expected outcome: The invalid operation is rejected.
    // Acceptance criteria: the operation must throw the asserted error, because this condition proves that
    // safely rejects malformed JSON.
    expect(() => parseFlowDtoJson('{"nodes":')).toThrow(FlowDtoValidationError);

    // Expected outcome: The invalid operation is rejected.
    // Acceptance criteria: the operation must throw the asserted error, because this condition proves that
    // safely rejects malformed JSON.
    expect(() => parseFlowDtoJson('{"nodes":')).toThrow('flow: malformed JSON');
  });
});
