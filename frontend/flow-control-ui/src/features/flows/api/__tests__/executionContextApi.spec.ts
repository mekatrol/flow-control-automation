import { afterEach, describe, expect, it, vi } from 'vitest';
import { executionContextApi } from '@/features/flows/api/executionContextApi';
import { AutomationPointValueType, DataDirectionType, PointSourceType } from '@/types/serverTypes';

const point = {
  exists: true,
  pointKey: 'room.temperature',
  pointSourceType: 'virtual',
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
    for (const pointSourceType of Object.values(PointSourceType)) {
      for (const valueType of Object.values(AutomationPointValueType)) {
        respond({ ...point, pointSourceType, valueType });
        await expect(executionContextApi.resolvePoint(point.pointKey)).resolves.toMatchObject({
          id: point.pointKey,
          pointSourceType,
          valueType,
          direction:
            pointSourceType === PointSourceType.Virtual
              ? DataDirectionType.Value
              : DataDirectionType.InputOutput
        });
      }
    }
  });

  it.each([
    null,
    [],
    { ...point, pointSourceType: 'unsupported' },
    { ...point, pointSourceType: 0 },
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
