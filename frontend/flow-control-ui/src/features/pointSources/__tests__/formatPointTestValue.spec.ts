import { describe, expect, it } from 'vitest';
import { formatPointTestValue } from '@/features/pointSources/formatPointTestValue';

describe('formatPointTestValue', () => {
  it('displays zero with the point units', () => {
    expect(formatPointTestValue(0, 'percent')).toBe('0 percent');
  });

  it('does not add units when none are defined', () => {
    expect(formatPointTestValue(21.5)).toBe('21.5');
  });

  it('keeps unavailable values free of units', () => {
    expect(formatPointTestValue(null, 'percent')).toBe('Unavailable');
  });
});
