import { beforeEach, describe, expect, it, vi } from 'vitest';

import { waitForFetch } from '@/api/waitForFetch';
import { fetchConfigurationGuidance } from '@/features/configuration/api/configurationGuidanceApi';

vi.mock('@/api/waitForFetch', () => ({ waitForFetch: vi.fn<typeof waitForFetch>() }));

describe('configuration guidance API', () => {
  beforeEach(() => vi.mocked(waitForFetch).mockReset());

  it('loads guidance without activating the application-wide wait overlay', async () => {
    vi.mocked(waitForFetch).mockResolvedValue(new Response('# Point guidance'));

    await expect(fetchConfigurationGuidance('point', 'schemaVersion: 1')).resolves.toBe(
      '# Point guidance'
    );

    expect(waitForFetch).toHaveBeenCalledWith(
      '/api/configuration-guidance/point',
      {
        method: 'POST',
        headers: { 'Content-Type': 'application/yaml', Accept: 'text/markdown' },
        body: 'schemaVersion: 1'
      },
      { trackWait: false }
    );
  });
});
