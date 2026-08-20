// @vitest-environment jsdom

import { createPinia, setActivePinia } from 'pinia';
import { beforeEach, describe, expect, it } from 'vitest';

import { useWait } from '@/composables/useWait';

describe('useWait', () => {
  beforeEach(() => setActivePinia(createPinia()));

  it('keeps waiting until every concurrent operation has ended', () => {
    const { waitCount, isWaiting, wait, endWait } = useWait();

    wait();
    wait();
    expect(waitCount.value).toBe(2);
    expect(isWaiting.value).toBe(true);

    endWait();
    expect(waitCount.value).toBe(1);
    expect(isWaiting.value).toBe(true);

    endWait();
    expect(waitCount.value).toBe(0);
    expect(isWaiting.value).toBe(false);
  });

  it('does not allow unmatched endWait calls to make the count negative', () => {
    const { waitCount, isWaiting, endWait } = useWait();

    endWait();

    expect(waitCount.value).toBe(0);
    expect(isWaiting.value).toBe(false);
  });
});
