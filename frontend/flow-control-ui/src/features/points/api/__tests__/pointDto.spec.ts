import { describe, expect, it } from 'vitest';
import { parsePage, parsePoint } from '@/features/points/api/pointDto';

const point = {
  id: 'room-temperature',
  name: 'Room temperature',
  description: 'Measured temperature',
  enabled: true,
  sourceKind: 'httpJson',
  sourceName: 'Building controller',
  direction: 'input',
  valueType: 'analog',
  units: 'deg_c',
  readable: true,
  commandable: false,
  persistence: 'volatile',
  sourceId: 'building-controller',
  mapping: 'temperatures/room',
  revision: 2,
  updatedAt: '2026-07-25T00:00:00Z'
};

describe('point DTO parsing', () => {
  /**
   * Purpose: Protects the behavioral contract that maps point, template and page contracts.
   * Description: Exercises point, template and page contracts from its arranged starting state and
   * verifies the observable results required by the scenario.
   */
  it('maps point, template and page contracts', () => {
    // Expected outcome: `parsePoint(point)` contains the required object fields.
    // Acceptance criteria: `parsePoint(point)` must include the point identifier and value type.
    expect(parsePoint(point)).toMatchObject({
      id: 'room-temperature',
      valueType: 'analog'
    });
    expect(
      parsePoint({ ...point, direction: 'inputOutput', valueType: 'multiState' })
    ).toMatchObject({ direction: 'inputOutput', valueType: 'multiState' });

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
    [{ ...point, sourceKind: 'unknown' }, /point.sourceKind/],
    [{ ...point, direction: 'sideways' }, /point.direction/],
    [{ ...point, valueType: 'float' }, /point.valueType/],
    [{ ...point, revision: 1.5 }, /point.revision/]
  ])('rejects malformed payloads', (payload, expected) => {
    // Expected outcome: The invalid operation is rejected.
    // Acceptance criteria: the operation must throw the asserted error, because this condition proves that
    // maps point, group, template and page contracts.
    expect(() => parsePoint(payload)).toThrow(expected);
  });
});
