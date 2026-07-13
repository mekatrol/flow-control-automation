import { describe, expect, it } from 'vitest';

import { connectionPath } from '../connectionPath';

describe('connection path', () => {
  it('draws forward and reverse horizontal splines', () => {
    expect(connectionPath({ x: 0, y: 10 }, { x: 200, y: 30 })).toBe(
      'M 0 10 C 90 10, 110 30, 200 30'
    );
    expect(connectionPath({ x: 200, y: 10 }, { x: 0, y: 30 })).toBe(
      'M 200 10 C 110 10, 90 30, 0 30'
    );
  });

  it('draws a vertical spline', () => {
    expect(connectionPath({ x: 10, y: 0 }, { x: 30, y: 200 })).toBe(
      'M 10 0 C 10 90, 30 110, 30 200'
    );
  });

  it('returns no path for a missing endpoint', () => {
    expect(connectionPath({ x: 0, y: 0 }, undefined)).toBe('');
    expect(connectionPath(undefined, { x: 0, y: 0 })).toBe('');
  });
});
