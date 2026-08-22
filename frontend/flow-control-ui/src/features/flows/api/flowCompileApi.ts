import { waitForFetch } from '@/api/waitForFetch';
import type { ExecutableFlowSource } from './flowDebugApi';

export interface FlowCompileDiagnostic {
  code: string;
  displayCode: string;
  path: string;
  title: string;
  message: string;
}

export interface FlowCompileResult {
  success: boolean;
  flowRevision?: number;
  artifactSha256?: string;
  instructionCount?: number;
  slotCount?: number;
  pointCount?: number;
  diagnostics: FlowCompileDiagnostic[];
}

const parseDiagnostic = (value: unknown): FlowCompileDiagnostic | undefined => {
  if (typeof value !== 'object' || value === null) return undefined;
  const item = value as Record<string, unknown>;
  if (typeof item.message !== 'string' || !item.message.trim()) return undefined;
  return {
    code: typeof item.code === 'string' ? item.code : 'CompileError',
    displayCode: typeof item.displayCode === 'string' ? item.displayCode : 'FLOW',
    path: typeof item.path === 'string' ? item.path : '',
    title: typeof item.title === 'string' ? item.title : 'Compilation error',
    message: item.message.trim()
  };
};

const parse = (value: unknown): FlowCompileResult => {
  if (typeof value !== 'object' || value === null)
    throw new TypeError('Compile result is invalid.');
  const item = value as Record<string, unknown>;
  if (typeof item.success !== 'boolean')
    throw new TypeError('Compile result has no success state.');
  const diagnostics = Array.isArray(item.diagnostics)
    ? item.diagnostics.flatMap((entry) => {
        const diagnostic = parseDiagnostic(entry);
        return diagnostic ? [diagnostic] : [];
      })
    : [];
  return {
    success: item.success,
    ...(typeof item.flowRevision === 'number' ? { flowRevision: item.flowRevision } : {}),
    ...(typeof item.artifactSha256 === 'string' ? { artifactSha256: item.artifactSha256 } : {}),
    ...(typeof item.instructionCount === 'number'
      ? { instructionCount: item.instructionCount }
      : {}),
    ...(typeof item.slotCount === 'number' ? { slotCount: item.slotCount } : {}),
    ...(typeof item.pointCount === 'number' ? { pointCount: item.pointCount } : {}),
    diagnostics
  };
};

export const flowCompileApi = {
  async compile(source: ExecutableFlowSource, signal?: AbortSignal): Promise<FlowCompileResult> {
    const response = await waitForFetch(`/api/flows/${encodeURIComponent(source.id)}/compile`, {
      method: 'POST',
      headers: { 'content-type': 'application/json' },
      body: JSON.stringify(source),
      signal
    });
    const result = parse(await response.json());
    if (!response.ok && result.diagnostics.length === 0)
      throw new Error(`Compile request failed with status ${response.status}.`);
    return result;
  }
};
