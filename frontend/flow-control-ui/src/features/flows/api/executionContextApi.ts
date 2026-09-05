import {
  AutomationPointValueType,
  PointSourceType,
  DataDirectionType,
  isEnumValue
} from '@/types/serverTypes';
import { waitForFetch } from '@/api/waitForFetch';
import type { VirtualPointDeclaration } from '@/features/flows/types';
import type { PointSummary } from '@/features/catalogues/api/catalogueDto';

export interface ExecutionContextSummary {
  id: string;
  name: string;
  revision: number;
  programs: { flowId: string; flowRevision: number }[];
  pointContracts: VirtualPointDeclaration[];
}

export class ExecutionConfigurationApiError extends Error {
  constructor(
    message: string,
    readonly status: number,
    readonly code: string,
    readonly details?: unknown
  ) {
    super(message);
  }
}

const requireOk = async (response: Response, fallback: string): Promise<Response> => {
  if (response.ok) return response;
  let message = `${fallback} (${response.status}).`;
  let code = 'request_failed';
  let details: unknown;
  try {
    const body = (await response.json()) as Record<string, unknown>;
    if (typeof body.message === 'string') message = body.message;
    if (typeof body.code === 'string') code = body.code;
    details = body.details;
  } catch {
    // Preserve the stable fallback for non-JSON proxy and transport responses.
  }
  throw new ExecutionConfigurationApiError(message, response.status, code, details);
};

const parseContext = (value: unknown): ExecutionContextSummary => {
  if (!value || typeof value !== 'object') throw new Error('Execution context is malformed.');
  const item = value as Record<string, unknown>;
  if (typeof item.id !== 'string' || typeof item.name !== 'string')
    throw new Error('Execution context identity is malformed.');
  return {
    id: item.id,
    name: item.name,
    revision: typeof item.revision === 'number' ? item.revision : 0,
    programs: Array.isArray(item.programs)
      ? (item.programs as { flowId: string; flowRevision: number }[])
      : [],
    pointContracts: Array.isArray(item.pointContracts)
      ? (item.pointContracts as VirtualPointDeclaration[])
      : []
  };
};

export const executionContextApi = {
  async list(signal?: AbortSignal): Promise<ExecutionContextSummary[]> {
    const response = await waitForFetch('/api/execution-contexts', { signal });
    await requireOk(response, 'Unable to load execution contexts');
    const body: unknown = await response.json();
    if (!Array.isArray(body)) throw new Error('Execution context catalogue is malformed.');
    return body.map(parseContext);
  },
  async resolvePoint(
    pointKey: string,
    executionContextId?: string,
    executionInstanceId?: string,
    signal?: AbortSignal
  ): Promise<PointSummary | undefined> {
    const query = new URLSearchParams();
    if (executionContextId) query.set('executionContextId', executionContextId);
    if (executionInstanceId) query.set('executionInstanceId', executionInstanceId);
    const suffix = query.size ? `?${query}` : '';
    const response = await waitForFetch(
      `/api/point-resolution/${encodeURIComponent(pointKey)}${suffix}`,
      { signal }
    );
    await requireOk(response, 'Unable to resolve point');
    const payload: unknown = await response.json();
    if (!payload || typeof payload !== 'object' || Array.isArray(payload))
      throw new Error('Point resolution is malformed.');
    const body = payload as Record<string, unknown>;
    if (body.exists === false) return undefined;
    if (
      body.exists !== true ||
      typeof body.pointKey !== 'string' ||
      !isEnumValue(PointSourceType, body.pointSourceType) ||
      !isEnumValue(AutomationPointValueType, body.valueType)
    )
      throw new Error('Point resolution is malformed.');
    return {
      id: body.pointKey,
      name: body.pointKey,
      enabled: body.enabled === true,
      pointSourceType: body.pointSourceType,
      direction:
        body.pointSourceType === PointSourceType.Virtual
          ? DataDirectionType.Value
          : DataDirectionType.InputOutput,
      valueType: body.valueType,
      units: typeof body.units === 'string' ? body.units : undefined,
      readable: body.readable === true,
      commandable: body.commandable === true,
      revision: typeof body.revision === 'number' ? body.revision : 0
    };
  }
};
