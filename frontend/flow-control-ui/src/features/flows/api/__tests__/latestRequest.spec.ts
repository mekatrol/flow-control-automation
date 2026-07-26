import { describe, expect, it } from 'vitest';

import { createLatestRequestGuard } from '@/features/flows/api/latestRequest';

describe('latest request guard', () => {
  /**
   * Purpose: Protects the behavioral contract that rejects stale route responses and invalidates work on unmount.
   * Description: Exercises rejects stale route responses and invalidates work on unmount from its arranged starting state and
   * verifies the observable results required by the scenario.
   */
  it('rejects stale route responses and invalidates work on unmount', () => {
    const guard = createLatestRequestGuard();
    const climateRequest = guard.begin();
    const gardenRequest = guard.begin();

    // Expected outcome: `guard.isCurrent(climateRequest)` has the required value.
    // Acceptance criteria: `guard.isCurrent(climateRequest)` must be `false`, because this condition proves that
    // rejects stale route responses and invalidates work on unmount.
    expect(guard.isCurrent(climateRequest)).toBe(false);

    // Expected outcome: `guard.isCurrent(gardenRequest)` has the required value.
    // Acceptance criteria: `guard.isCurrent(gardenRequest)` must be `true`, because this condition proves that
    // rejects stale route responses and invalidates work on unmount.
    expect(guard.isCurrent(gardenRequest)).toBe(true);

    guard.invalidate();

    // Expected outcome: `guard.isCurrent(gardenRequest)` has the required value.
    // Acceptance criteria: `guard.isCurrent(gardenRequest)` must be `false`, because this condition proves that
    // rejects stale route responses and invalidates work on unmount.
    expect(guard.isCurrent(gardenRequest)).toBe(false);
  });
});
