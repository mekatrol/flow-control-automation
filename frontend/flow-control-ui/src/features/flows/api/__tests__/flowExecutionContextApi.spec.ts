import { afterEach, describe, expect, it, vi } from 'vitest';
import { flowExecutionContextApi } from '@/features/flows/api/flowExecutionContextApi';

describe('flow execution context API', () => {
  afterEach(() => vi.unstubAllGlobals());

  it('includes compiler diagnostics in a failed create message', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn<typeof fetch>().mockResolvedValue(
        new Response(
          JSON.stringify({
            code: 'compilation_failed',
            message: 'The saved flow could not be compiled.',
            details: [
              {
                path: '/nodes/1/configuration/pointId',
                message: 'Input point IDs must be unique.'
              },
              { path: '/nodes/2', message: 'The OR node requires two inputs.' }
            ]
          }),
          { status: 422 }
        )
      )
    );

    await expect(
      flowExecutionContextApi.create('draft-flow', {
        mode: 'debugger',
        expectedRevision: 3,
        targetId: 'server'
      })
    ).rejects.toThrow(
      'The saved flow could not be compiled. Input point IDs must be unique. (/nodes/1/configuration/pointId) The OR node requires two inputs. (/nodes/2)'
    );
  });
});
