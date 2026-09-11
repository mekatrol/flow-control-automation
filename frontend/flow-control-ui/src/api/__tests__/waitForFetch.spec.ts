// @vitest-environment jsdom

import { createPinia, setActivePinia } from 'pinia';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { waitForFetch } from '@/api/waitForFetch';
import { useSpinnerStore } from '@/stores/spinner';

describe('waitForFetch', () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    vi.restoreAllMocks();
  });

  it('tracks concurrent HTTP requests until the final request completes', async () => {
    const resolvers: Array<(response: Response) => void> = [];
    vi.stubGlobal(
      'fetch',
      vi.fn(
        () =>
          new Promise<Response>((resolve) => {
            resolvers.push(resolve);
          })
      )
    );
    const store = useSpinnerStore();

    const first = waitForFetch('/first');
    const second = waitForFetch('/second');
    expect(store.spinnerWaitingCount).toBe(2);

    resolvers[0]!(new Response());
    await first;
    expect(store.spinnerWaitingCount).toBe(1);
    expect(store.isSpinnerVisible).toBe(true);

    resolvers[1]!(new Response());
    await second;
    expect(store.spinnerWaitingCount).toBe(0);
    expect(store.isSpinnerVisible).toBe(false);
  });

  it('ends the wait when fetch rejects', async () => {
    vi.stubGlobal('fetch', vi.fn().mockRejectedValue(new Error('Network unavailable')));
    const store = useSpinnerStore();

    await expect(waitForFetch('/flows')).rejects.toThrow('Network unavailable');

    expect(store.spinnerWaitingCount).toBe(0);
    expect(store.isSpinnerVisible).toBe(false);
  });

  it('allows background polling without changing the global wait state', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response()));
    const store = useSpinnerStore();

    await waitForFetch('/poll', undefined, { trackWait: false });

    expect(store.spinnerWaitingCount).toBe(0);
    expect(store.isSpinnerVisible).toBe(false);
  });
});
