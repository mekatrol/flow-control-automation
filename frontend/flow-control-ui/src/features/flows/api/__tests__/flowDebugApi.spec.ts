import { describe, expect, it } from 'vitest';
import { parseDebugSnapshot } from '@/features/flows/api/flowDebugApi';

// The fixture deliberately preserves its inferred nested shape for mutation tests.
// eslint-disable-next-line @typescript-eslint/explicit-function-return-type
const snapshot = () => ({
  debugSessionId: '42',
  flowId: 'flow-a',
  revision: 7,
  lifecycleState: 'paused',
  mode: 'manual',
  tickNumber: 3,
  sampledAtMs: 100,
  completedAtMs: 101,
  executionDurationUs: 40,
  inputValidity: ['good'],
  nodes: [
    {
      nodeId: 'not-1',
      state: 'ready',
      quality: 'good',
      typedValue: { dataType: 'boolean', value: true, number: null, quality: 'good' }
    }
  ],
  proposedOutputs: [
    { pointId: 'relay-1', state: 'proposed', quality: 'good', proposedValue: false }
  ],
  overrunCount: 0,
  evaluationFailureCount: 0,
  lastReasonCode: 0,
  lastReason: 'none',
  lastReasonPath: ''
});

describe('flow debug API contract', () => {
  it('parses typed node values and proposed outputs', () => {
    const parsed = parseDebugSnapshot(snapshot());
    expect(parsed.nodes[0]?.typedValue).toEqual({ type: 'boolean', value: true, quality: 'good' });
    expect(parsed.proposedOutputs[0]?.proposedValue).toBe(false);
    expect(parsed.tickNumber).toBe(3);
  });

  it('rejects malformed and unsafe snapshots', () => {
    expect(() => parseDebugSnapshot({ ...snapshot(), revision: -1 })).toThrow(/revision/);
    expect(() => parseDebugSnapshot({ ...snapshot(), nodes: [{ nodeId: 'x' }] })).toThrow(
      /nodes\[0\]\.state/
    );
    expect(() => parseDebugSnapshot({ ...snapshot(), lifecycleState: 'running-away' })).toThrow(
      /lifecycleState is invalid/
    );
  });
});
