import { afterEach, describe, expect, it, vi } from 'vitest';
import { executionContextApi } from '@/features/flows/api/executionContextApi';
import { AutomationPointValueType, DataDirectionType } from '@/types/serverTypes';
const PointSourceKind = {
  Physical: 'physical',
  Virtual: 'virtual',
  HomeAssistant: 'homeAssistant',
  Mqtt: 'mqtt',
  HttpJson: 'httpJson'
} as const;

const point = {
  exists: true,
  pointKey: 'room.temperature',
  sourceKind: 'virtual',
  valueType: 'analog',
  enabled: true,
  readable: true,
  commandable: true,
  revision: 2
};

const respond = (body: unknown): void => {
  vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(JSON.stringify(body))));
};

describe('execution context point resolution', () => {
  afterEach(() => vi.unstubAllGlobals());

  it('accepts every backend point source and value type without changing wire values', async () => {
    for (const sourceKind of Object.values(PointSourceKind)) {
      for (const valueType of Object.values(AutomationPointValueType)) {
        respond({ ...point, sourceKind, valueType });
        await expect(executionContextApi.resolvePoint(point.pointKey)).resolves.toMatchObject({
          id: point.pointKey,
          sourceKind,
          valueType,
          direction:
            sourceKind === PointSourceKind.Virtual
              ? DataDirectionType.Value
              : DataDirectionType.InputOutput
        });
      }
    }
  });

  it.each([
    null,
    [],
    { ...point, sourceKind: 'unsupported' },
    { ...point, sourceKind: 0 },
    { ...point, valueType: 'Analog' },
    { ...point, valueType: ['analog'] },
    { ...point, valueType: 1 }
  ])('rejects malformed responses and non-enum values: %j', async (body) => {
    respond(body);
    await expect(executionContextApi.resolvePoint(point.pointKey)).rejects.toThrow(
      'Point resolution is malformed.'
    );
  });

  it('returns undefined for a missing point', async () => {
    respond({ exists: false });
    await expect(executionContextApi.resolvePoint('missing')).resolves.toBeUndefined();
  });
});
