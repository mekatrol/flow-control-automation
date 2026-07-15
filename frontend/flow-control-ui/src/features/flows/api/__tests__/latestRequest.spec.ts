import { describe, expect, it } from 'vitest';

import { createLatestRequestGuard } from '@/features/flows/api/latestRequest';

describe('latest request guard', () => {
  it('rejects stale route responses and invalidates work on unmount', () => {
    const guard = createLatestRequestGuard();
    const climateRequest = guard.begin();
    const gardenRequest = guard.begin();

    expect(guard.isCurrent(climateRequest)).toBe(false);
    expect(guard.isCurrent(gardenRequest)).toBe(true);

    guard.invalidate();
    expect(guard.isCurrent(gardenRequest)).toBe(false);
  });
});
