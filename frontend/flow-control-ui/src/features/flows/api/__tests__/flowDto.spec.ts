import { describe, expect, it } from 'vitest';

import { sampleFlows } from '../../__tests__/fixtures/sampleFlows';
import { FlowDtoValidationError, parseFlowDto, parseFlowDtoJson } from '../flowDto';

const validFlow = (): unknown => structuredClone(sampleFlows[0]);

describe('flow DTO validation', () => {
  it('accepts a valid graph payload', () => {
    expect(parseFlowDto(validFlow())).toEqual(sampleFlows[0]);
  });

  it('rejects a connection whose node does not exist', () => {
    const payload = validFlow() as (typeof sampleFlows)[number];
    payload.connections[0]!.end.nodeId = 'missing';

    expect(() => parseFlowDto(payload)).toThrow(/unknown node “missing”/);
  });

  it('rejects duplicate node, connector, and connection IDs', () => {
    const duplicateNode = validFlow() as (typeof sampleFlows)[number];
    duplicateNode.nodes[1]!.id = duplicateNode.nodes[0]!.id;
    expect(() => parseFlowDto(duplicateNode)).toThrow(/duplicate id/);

    const duplicateConnector = validFlow() as (typeof sampleFlows)[number];
    duplicateConnector.nodes[0]!.connectors[1]!.id = duplicateConnector.nodes[0]!.connectors[0]!.id;
    expect(() => parseFlowDto(duplicateConnector)).toThrow(/duplicate id/);

    const duplicateConnection = validFlow() as (typeof sampleFlows)[number];
    duplicateConnection.connections[1]!.id = duplicateConnection.connections[0]!.id;
    expect(() => parseFlowDto(duplicateConnection)).toThrow(/duplicate id/);
  });

  it('rejects invalid connector directions and incompatible data types', () => {
    const wrongDirection = validFlow() as (typeof sampleFlows)[number];
    wrongDirection.nodes[0]!.connectors[1]!.direction = 'input';
    expect(() => parseFlowDto(wrongDirection)).toThrow(/must reference an output connector/);

    const wrongType = validFlow() as (typeof sampleFlows)[number];
    wrongType.nodes[1]!.connectors[0]!.dataType = 'string';
    expect(() => parseFlowDto(wrongType)).toThrow(/are incompatible/);
  });

  it('safely rejects malformed JSON', () => {
    expect(() => parseFlowDtoJson('{"nodes":')).toThrow(FlowDtoValidationError);
    expect(() => parseFlowDtoJson('{"nodes":')).toThrow('flow: malformed JSON');
  });
});
