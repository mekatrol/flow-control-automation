// @vitest-environment jsdom

import { createPinia, setActivePinia } from 'pinia';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { waitForFetch } from '@/api/waitForFetch';
import { useWaitStore } from '@/stores/wait';

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
    const store = useWaitStore();

    const first = waitForFetch('/first');
    const second = waitForFetch('/second');
    expect(store.waitCount).toBe(2);

    resolvers[0]!(new Response());
    await first;
    expect(store.waitCount).toBe(1);
    expect(store.isWaiting).toBe(true);

    resolvers[1]!(new Response());
    await second;
    expect(store.waitCount).toBe(0);
    expect(store.isWaiting).toBe(false);
  });

  it('ends the wait when fetch rejects', async () => {
    vi.stubGlobal('fetch', vi.fn().mockRejectedValue(new Error('Network unavailable')));
    const store = useWaitStore();

    await expect(waitForFetch('/flows')).rejects.toThrow('Network unavailable');

    expect(store.waitCount).toBe(0);
    expect(store.isWaiting).toBe(false);
  });

  it('allows background polling without changing the global wait state', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response()));
    const store = useWaitStore();

    await waitForFetch('/poll', undefined, { trackWait: false });

    expect(store.waitCount).toBe(0);
    expect(store.isWaiting).toBe(false);
  });
});
