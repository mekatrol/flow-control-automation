import type { VirtualPointValueType } from '@/features/flows/types';
import { AutomationPointValueType, FlowNodeType } from '@/types/serverTypes';
import { executionContextApi } from '@/features/flows/api/executionContextApi';
import type { PointSummary } from '@/features/catalogues/api/catalogueDto';
import type { FlowNode, VirtualPointDeclaration } from '@/features/flows/types';
import { isVirtualPointNode } from '@/features/flows/types';

export type PointValidationState = 'idle' | 'pending' | 'valid' | 'invalid' | 'unavailable';
export interface PointValidationResult {
  state: PointValidationState;
  message?: string;
  point?: PointSummary | VirtualPointDeclaration;
}

export const isPointNode = (node: FlowNode): boolean =>
  isInputPointNode(node) || isOutputPointNode(node) || isVirtualPointNode(node);

export const isInputPointNode = (node: FlowNode): boolean =>
  node.nodeType === FlowNodeType.AnalogInput || node.nodeType === FlowNodeType.DigitalInput;

export const isOutputPointNode = (node: FlowNode): boolean =>
  node.nodeType === FlowNodeType.AnalogOutput || node.nodeType === FlowNodeType.DigitalOutput;

export const pointRequirement = (
  node: FlowNode
): { valueType: VirtualPointValueType; readable: boolean } => ({
  valueType:
    node.nodeType === FlowNodeType.AnalogInput ||
    node.nodeType === FlowNodeType.AnalogOutput ||
    node.nodeType === FlowNodeType.AnalogVirtual
      ? AutomationPointValueType.Analog
      : AutomationPointValueType.Digital,
  readable: isInputPointNode(node) || isVirtualPointNode(node)
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
  signal?: AbortSignal,
  executionContextId?: string,
  executionInstanceId?: string
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
    const point = await executionContextApi.resolvePoint(
      key,
      executionContextId,
      executionInstanceId,
      signal
    );
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
