import { catalogueApi } from '@/features/catalogues/api/catalogueApi';
import type { PointSummary } from '@/features/catalogues/api/catalogueDto';
import type { FlowNode, VirtualPointDeclaration } from '@/features/flows/types';

export type PointValidationState = 'idle' | 'pending' | 'valid' | 'invalid' | 'unavailable';
export interface PointValidationResult {
  state: PointValidationState;
  message?: string;
  point?: PointSummary | VirtualPointDeclaration;
}

export const isPointNode = (node: FlowNode): boolean =>
  ['analogInput', 'analogOutput', 'digitalInput', 'digitalOutput'].includes(node.kind);

export const pointRequirement = (
  node: FlowNode
): { valueType: 'analog' | 'digital'; readable: boolean } => ({
  valueType: node.kind.startsWith('analog') ? 'analog' : 'digital',
  readable: node.kind.endsWith('Input')
});

export const pointCompatibilityError = (
  node: FlowNode,
  point:
    | Pick<PointSummary, 'valueType' | 'readable' | 'commandable' | 'enabled'>
    | (VirtualPointDeclaration & { enabled?: boolean })
): string | undefined => {
  const requirement = pointRequirement(node);
  if ('enabled' in point && point.enabled === false) return 'This point is disabled.';
  if (point.valueType !== requirement.valueType)
    return `This point is ${point.valueType}, but the node requires ${requirement.valueType}.`;
  if (requirement.readable && !point.readable) return 'This input node requires a readable point.';
  if (!requirement.readable && !point.commandable)
    return 'This output node requires a commandable point.';
};

export const validatePointReference = async (
  node: FlowNode,
  declarations: VirtualPointDeclaration[],
  signal?: AbortSignal
): Promise<PointValidationResult> => {
  const key = String(node.configuration.pointId ?? '').trim();
  if (!key) return { state: 'invalid', message: 'Point ID is required.' };
  if (!/^[a-zA-Z0-9](?:[a-zA-Z0-9._-]{0,126}[a-zA-Z0-9])?$/.test(key))
    return { state: 'invalid', message: 'Point ID contains unsupported characters.' };
  const declared = declarations.find((point) => point.key === key);
  if (declared) {
    const message = pointCompatibilityError(node, declared);
    return message
      ? { state: 'invalid', message, point: declared }
      : { state: 'valid', point: declared };
  }
  try {
    const page = await catalogueApi.points({ filter: key, page: 1, pageSize: 50 }, signal);
    const point = page.items.find((candidate) => candidate.id === key);
    if (!point) return { state: 'invalid', message: `Point “${key}” does not exist.` };
    const message = pointCompatibilityError(node, point);
    return message ? { state: 'invalid', message, point } : { state: 'valid', point };
  } catch (error) {
    if (signal?.aborted) throw error;
    return {
      state: 'unavailable',
      message:
        error instanceof Error
          ? `Point validation unavailable: ${error.message}`
          : 'Point validation unavailable.'
    };
  }
};
