import { waitForFetch } from '@/api/waitForFetch';
import type { VirtualPointDeclaration } from '@/features/flows/types';

export interface ExecutionContextSummary {
  id: string;
  name: string;
  revision: number;
  programs: { flowId: string; flowRevision: number }[];
  pointContracts: VirtualPointDeclaration[];
}

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
    if (!response.ok) throw new Error(`Unable to load execution contexts (${response.status}).`);
    const body: unknown = await response.json();
    if (!Array.isArray(body)) throw new Error('Execution context catalogue is malformed.');
    return body.map(parseContext);
  }
};
