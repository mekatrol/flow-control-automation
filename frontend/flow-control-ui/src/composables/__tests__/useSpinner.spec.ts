// @vitest-environment jsdom

import { createPinia, setActivePinia } from 'pinia';
import { beforeEach, describe, expect, it } from 'vitest';

import { useSpinner } from '@/composables/useSpinner';

describe('useSpinner', () => {
  beforeEach(() => setActivePinia(createPinia()));

  it('keeps the spinner visible until every concurrent operation has ended', () => {
    const { spinnerWaitingCount, isSpinnerVisible, showSpinner, hideSpinner } = useSpinner();

    showSpinner();
    showSpinner();
    expect(spinnerWaitingCount.value).toBe(2);
    expect(isSpinnerVisible.value).toBe(true);

    hideSpinner();
    expect(spinnerWaitingCount.value).toBe(1);
    expect(isSpinnerVisible.value).toBe(true);

    hideSpinner();
    expect(spinnerWaitingCount.value).toBe(0);
    expect(isSpinnerVisible.value).toBe(false);
  });

  it('does not allow unmatched hideSpinner calls to make the count negative', () => {
    const { spinnerWaitingCount, isSpinnerVisible, hideSpinner } = useSpinner();

    hideSpinner();

    expect(spinnerWaitingCount.value).toBe(0);
    expect(isSpinnerVisible.value).toBe(false);
  });

  it('wraps an action with the spinner and optional lifecycle hooks', async () => {
    const { isSpinnerVisible, withSpinner } = useSpinner();
    const events: string[] = [];

    const result = await withSpinner(
      () => {
        events.push(`pre:${isSpinnerVisible.value}`);
      },
      async () => {
        events.push(`call:${isSpinnerVisible.value}`);
        return 42;
      },
      (value) => {
        events.push(`post:${isSpinnerVisible.value}:${value}`);
      }
    );

    expect(result).toBe(42);
    expect(events).toEqual(['pre:true', 'call:true', 'post:true:42']);
    expect(isSpinnerVisible.value).toBe(false);
  });

  it('hides the spinner and rethrows when an action fails', async () => {
    const { isSpinnerVisible, withSpinner } = useSpinner();
    const failure = new Error('failed');

    await expect(withSpinner(null, () => Promise.reject(failure), null)).rejects.toBe(failure);
    expect(isSpinnerVisible.value).toBe(false);
  });

  it('hides the spinner when a post hook fails', async () => {
    const { isSpinnerVisible, withSpinner } = useSpinner();
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
    expect(isSpinnerVisible.value).toBe(false);
  });

  it('does not run the post hook when the action fails', async () => {
    const { withSpinner } = useSpinner();
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
