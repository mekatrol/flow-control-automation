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

  it('does not request replacement when creating a debugger context', async () => {
    const fetchMock = vi.fn<typeof fetch>().mockResolvedValue(
      new Response(
        JSON.stringify({
          id: 'context-1',
          flowId: 'flow-1',
          revision: 1,
          mode: 'debugger',
          lifecycle: 'ready',
          capabilities: Object.fromEntries(
            [
              'canRun',
              'canPause',
              'canStop',
              'canRestart',
              'canStepTick',
              'canStepNode',
              'canStepInstruction',
              'canUseBreakpoints',
              'canRunTo',
              'canEditInputs',
              'canAdvanceVirtualTime',
              'canInjectFaults',
              'canResetIo',
              'canEnableLiveOutputs',
              'locksFlowEditing'
            ].map((name) => [name, false])
          ),
          breakpoints: [],
          presentation: {},
          leaseRemainingMilliseconds: 1000
        }),
        { status: 201 }
      )
    );
    vi.stubGlobal('fetch', fetchMock);

    await flowExecutionContextApi.create('flow-1', {
      mode: 'debugger',
      expectedRevision: 1,
      targetId: 'server'
    });

    const body = fetchMock.mock.calls[0]?.[1]?.body;
    expect(typeof body).toBe('string');
    const request = JSON.parse(body as string) as Record<string, unknown>;
    expect(request).not.toHaveProperty('replaceExisting');
  });
});
