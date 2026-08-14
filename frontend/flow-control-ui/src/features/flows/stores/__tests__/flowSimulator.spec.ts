// @vitest-environment jsdom

import { createPinia, setActivePinia } from 'pinia';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import type { ExecutableFlowSource } from '@/features/flows/api/flowDebugApi';
import type { SimulatorSession } from '@/features/flows/api/flowSimulatorApi';
import { useFlowSimulatorStore } from '@/features/flows/stores/flowSimulator';

const source = (): ExecutableFlowSource => ({
  schemaVersion: 1,
  id: 'flow-a',
  revision: 3,
  controllerTemplateId: 'server',
  controllerTemplateRevision: 1,
  execution: { mode: 'manual', intervalMs: 0, inputQualityPolicy: 'require_good' },
  nodes: [],
  connections: []
});
const session = (state: SimulatorSession['lifecycleState'] = 'ready'): SimulatorSession => ({
  sessionId: 'session-a',
  flowId: 'flow-a',
  sourceRevision: 3,
  sourceDigest: 'digest-a',
  lifecycleState: state,
  leaseRemainingMilliseconds: 900000,
  breakpoints: [],
  capabilities: {
    stepTick: true,
    stepNode: true,
    stepInstruction: true,
    continue: true,
    pause: true,
    runTo: true,
    maximumBreakpoints: 32,
    maximumInspectableSlots: 256
  }
});

describe('flow simulator store', () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    vi.restoreAllMocks();
  });

  it('starts, steps, marks edits stale, and stops the volatile session', async () => {
    const fetch = vi.spyOn(globalThis, 'fetch');
    fetch
      .mockResolvedValueOnce(new Response(JSON.stringify(session()), { status: 201 }))
      .mockResolvedValueOnce(new Response(JSON.stringify(session('paused')), { status: 200 }))
      .mockResolvedValueOnce(new Response(null, { status: 204 }));
    const store = useFlowSimulatorStore();

    await store.start(source());
    await store.stepTick();
    store.markStale();
    await store.stop();

    expect(store.lifecycle).toBe('stopped');
    expect(fetch.mock.calls.map(([url]) => url)).toEqual([
      '/api/flows/flow-a/simulator-sessions',
      '/api/flows/flow-a/simulator-sessions/session-a/step',
      '/api/flows/flow-a/simulator-sessions/session-a'
    ]);
  });

  it('ignores a cancelled older start when a newer draft starts', async () => {
    let finishFirst: ((response: Response) => void) | undefined;
    vi.spyOn(globalThis, 'fetch')
      .mockImplementationOnce(
        () =>
          new Promise((resolve) => {
            finishFirst = resolve;
          })
      )
      .mockResolvedValueOnce(
        new Response(JSON.stringify({ ...session(), sourceDigest: 'new' }), { status: 201 })
      );
    const store = useFlowSimulatorStore();

    const first = store.start(source());
    const second = store.start(source());
    await second;
    finishFirst?.(
      new Response(JSON.stringify({ ...session(), sourceDigest: 'old' }), { status: 201 })
    );
    await first;

    expect(store.session?.sourceDigest).toBe('new');
    expect(store.lifecycle).toBe('ready');
  });

  it('presents structured service failures and enters faulted state', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(
      new Response(
        JSON.stringify({ code: 'compile_invalid_source', message: 'Draft is invalid.' }),
        { status: 422 }
      )
    );
    const store = useFlowSimulatorStore();

    await store.start(source());

    expect(store.lifecycle).toBe('faulted');
    expect(store.error).toBe('Draft is invalid.');
  });
});
