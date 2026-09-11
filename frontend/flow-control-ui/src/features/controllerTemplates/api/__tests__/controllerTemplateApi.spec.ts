import { afterEach, describe, expect, it, vi } from 'vitest';
import {
  ControllerTemplateApiError,
  controllerTemplateApi
} from '@/features/controllerTemplates/api/controllerTemplateApi';

afterEach(() => vi.unstubAllGlobals());

describe('controller template API', () => {
  it('maps a JSON failure with its status', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn<typeof globalThis.fetch>().mockResolvedValueOnce(
        new Response(JSON.stringify({ message: 'Not supported' }), {
          status: 404,
          headers: { 'Content-Type': 'application/json' }
        })
      )
    );

    await expect(controllerTemplateApi.list()).rejects.toMatchObject({
      message: 'Not supported',
      status: 404
    } satisfies Partial<ControllerTemplateApiError>);
  });
});
