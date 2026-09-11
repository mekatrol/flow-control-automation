import { afterEach, describe, expect, it, vi } from 'vitest';

import { validatePointReference } from '@/features/flows/flowPointValidation';
import type { FlowNode, VirtualPointDefinition } from '@/features/flows/types';

const node = (nodeType: FlowNode['nodeType'], pointId: string): FlowNode => ({
  id: 'point-node',
  nodeType,
  label: 'Point',
  x: 0,
  y: 0,
  zOrder: 0,
  connectors: [],
  configuration: { pointId }
});
const definition: VirtualPointDefinition = {
  key: 'temperature',
  valueType: 'analog',
  readable: true,
  commandable: false,
  persistence: 'volatile',
  units: '°C'
};

describe('flow point validation', () => {
  afterEach(() => vi.restoreAllMocks());

  it('validates compatible definitions without a points request', async () => {
    const fetchMock = vi.spyOn(globalThis, 'fetch');
    await expect(
      validatePointReference(node('analogInput', 'temperature'), [definition])
    ).resolves.toMatchObject({ state: 'valid', point: definition });
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it('reports a specific capability mismatch', async () => {
    await expect(
      validatePointReference(node('analogOutput', 'temperature'), [definition])
    ).resolves.toMatchObject({
      state: 'invalid',
      message: 'This output node requires a commandable point.'
    });
  });

  it('distinguishes missing points from an unavailable points API', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValueOnce(
      new Response(
        JSON.stringify({
          pointKey: 'missing',
          exists: false
        }),
        { status: 200 }
      )
    );
    await expect(
      validatePointReference(node('digitalInput', 'missing'), [])
    ).resolves.toMatchObject({ state: 'invalid', message: 'Point “missing” does not exist.' });

    vi.spyOn(globalThis, 'fetch').mockRejectedValueOnce(new Error('offline'));
    await expect(
      validatePointReference(node('digitalInput', 'unknown'), [])
    ).resolves.toMatchObject({
      state: 'unavailable',
      message: expect.stringContaining('unavailable')
    });
  });
});
