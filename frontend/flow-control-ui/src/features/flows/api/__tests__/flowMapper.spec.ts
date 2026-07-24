import { describe, expect, it } from 'vitest';

import { sampleFlows } from '@/features/flows/__tests__/fixtures/sampleFlows';
import { parseFlowDto } from '@/features/flows/api/flowDto';
import { flowDomainToDto, flowDtoToDomain } from '@/features/flows/api/flowMapper';

describe('flow DTO mapping', () => {
  it('maps a validated DTO to editable domain data', () => {
    const dto = parseFlowDto(structuredClone(sampleFlows[0]));

    const domain = flowDtoToDomain(dto);

    expect(domain).toEqual(sampleFlows[0]);
    expect(domain).not.toBe(dto);
  });

  it('round trips without losing persisted graph data', () => {
    const dto = parseFlowDto(structuredClone(sampleFlows[0]));

    expect(flowDomainToDto(flowDtoToDomain(dto))).toEqual(dto);
  });
});
