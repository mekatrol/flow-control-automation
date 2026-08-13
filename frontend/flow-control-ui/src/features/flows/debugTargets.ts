import type { ControllerTemplateSummary } from '@/features/catalogues/api/catalogueDto';

export interface FlowDebugTarget {
  id: string;
  kind: 'host' | 'server' | 'emulator' | 'controller';
  label: string;
  controllerTemplateId?: string;
  controllerTemplateRevision?: number;
}

const requiredFunctions = ['and', 'not', 'or', 'read-point', 'write-point'];

export const isControllerDebugCompatible = (template: ControllerTemplateSummary): boolean =>
  template.id !== 'default' &&
  template.revision > 0 &&
  template.capabilities.pointTypes.includes('digital') &&
  template.capabilities.pointDirections.includes('input') &&
  template.capabilities.pointDirections.includes('output') &&
  template.capabilities.connectorDataTypes.includes('boolean') &&
  template.capabilities.runtimeFeatures.includes('bound_points') &&
  requiredFunctions.every((kind) => template.capabilities.flowFunctions.includes(kind));

export const getFlowDebugTargets = (
  templates: readonly ControllerTemplateSummary[]
): FlowDebugTarget[] => [
  {
    id: 'server',
    kind: 'server',
    label: 'Server',
    controllerTemplateId: 'default',
    controllerTemplateRevision: 1
  },
  ...templates.filter(isControllerDebugCompatible).map((template) => ({
    id: `emulator:${template.id}`,
    kind: 'emulator' as const,
    label: `Emulator — ${template.name}`,
    controllerTemplateId: template.id,
    controllerTemplateRevision: template.revision
  })),
  ...templates.filter(isControllerDebugCompatible).map((template) => ({
    id: `controller:${template.id}`,
    kind: 'controller' as const,
    label: template.name,
    controllerTemplateId: template.id,
    controllerTemplateRevision: template.revision
  }))
];
