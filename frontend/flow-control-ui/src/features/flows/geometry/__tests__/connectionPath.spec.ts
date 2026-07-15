import { describe, expect, it } from 'vitest';

import { connectionPath } from '@/features/flows/geometry/connectionPath';

describe('connection path', () => {
  it('draws forward and reverse horizontal splines', () => {
    expect(connectionPath({ x: 0, y: 10 }, { x: 200, y: 30 })).toBe(
      'M 0 10 C 100 10, 100 30, 200 30'
    );
    expect(connectionPath({ x: 200, y: 10 }, { x: 0, y: 30 })).toBe(
      'M 200 10 C 288.3883476483185 10, -88.38834764831844 30, 0 30'
    );
  });

  it('routes from the side of vertically arranged connectors', () => {
    expect(connectionPath({ x: 10, y: 0 }, { x: 30, y: 200 }, 'bottom', 'top')).toBe(
      'M 10 0 C 10 100, 30 100, 30 200'
    );
  });

  it('supports connectors on different sides', () => {
    expect(connectionPath({ x: 0, y: 100 }, { x: 200, y: 0 }, 'left', 'bottom')).toBe(
      'M 0 100 C -88.38834764831844 100, 200 50, 200 0'
    );
  });

  it('returns no path for a missing endpoint', () => {
    expect(connectionPath({ x: 0, y: 0 }, undefined)).toBe('');
    expect(connectionPath(undefined, { x: 0, y: 0 })).toBe('');
  });
});
