import { describe, expect, it } from 'vitest';

import { sampleFlows } from '@/features/flows/__tests__/fixtures/sampleFlows';
import { parseFlowDto } from '@/features/flows/api/flowDto';
import { flowDomainToDto, flowDtoToDomain } from '@/features/flows/api/flowMapper';

describe('flow DTO mapping', () => {
  /**
   * Purpose: Protects the behavioral contract that maps a validated DTO to editable domain data.
   * Description: Exercises maps a validated DTO to editable domain data from its arranged starting state and
   * verifies the observable results required by the scenario.
   */
  it('maps a validated DTO to editable domain data', () => {
    const dto = parseFlowDto(structuredClone(sampleFlows[0]));

    const domain = flowDtoToDomain(dto);

    // Expected outcome: `domain` matches the required structure.
    // Acceptance criteria: `domain` must equal `sampleFlows[0]`, because this condition proves that
    // maps a validated DTO to editable domain data.
    expect(domain).toEqual(sampleFlows[0]);

    // Expected outcome: `domain` has the required value.
    // Acceptance criteria: `domain` must be `dto`, because this condition proves that
    // maps a validated DTO to editable domain data.
    expect(domain).not.toBe(dto);
  });

  /**
   * Purpose: Protects the behavioral contract that round trips without losing persisted graph data.
   * Description: Exercises round trips without losing persisted graph data from its arranged starting state and
   * verifies the observable results required by the scenario.
   */
  it('round trips without losing persisted graph data', () => {
    const dto = parseFlowDto(structuredClone(sampleFlows[0]));

    // Expected outcome: `flowDomainToDto(flowDtoToDomain(dto))` matches the required structure.
    // Acceptance criteria: `flowDomainToDto(flowDtoToDomain(dto))` must equal `dto`, because this condition proves that
    // round trips without losing persisted graph data.
    expect(flowDomainToDto(flowDtoToDomain(dto))).toEqual(dto);
  });
});
