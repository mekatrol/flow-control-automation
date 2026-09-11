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

  it('wraps an action with waiting and optional lifecycle hooks', async () => {
    const { isWaiting, withSpinner } = useWait();
    const events: string[] = [];

    const result = await withSpinner(
      () => {
        events.push(`pre:${isWaiting.value}`);
      },
      async () => {
        events.push(`call:${isWaiting.value}`);
        return 42;
      },
      (value) => {
        events.push(`post:${isWaiting.value}:${value}`);
      }
    );

    expect(result).toBe(42);
    expect(events).toEqual(['pre:true', 'call:true', 'post:true:42']);
    expect(isWaiting.value).toBe(false);
  });

  it('ends waiting and rethrows when an action fails', async () => {
    const { isWaiting, withSpinner } = useWait();
    const failure = new Error('failed');

    await expect(withSpinner(null, () => Promise.reject(failure), null)).rejects.toBe(failure);
    expect(isWaiting.value).toBe(false);
  });

  it('ends waiting when a post hook fails', async () => {
    const { isWaiting, withSpinner } = useWait();
    const failure = new Error('post failed');

    await expect(
      withSpinner(
        null,
        () => undefined,
        () => {
          throw failure;
        }
      )
    ).rejects.toBe(failure);
    expect(isWaiting.value).toBe(false);
  });

  it('does not run the post hook when the action fails', async () => {
    const { withSpinner } = useWait();
    let postCalled = false;

    await expect(
      withSpinner(
        null,
        () => Promise.reject(new Error('failed')),
        () => {
          postCalled = true;
        }
      )
    ).rejects.toThrow('failed');
    expect(postCalled).toBe(false);
  });
});
