import {
  FlowFunctionType,
  DataDirectionType,
  ConnectorDataType,
  ControllerRuntimeFeatureType
} from '@/types/serverTypes';
import { AutomationPointValueType } from '@/types/serverTypes';
import type { ControllerTemplateSummary } from '@/features/controllerTemplates/api/controllerTemplateDto';

export enum FlowDebugTargetKind {
  Host = 'host',
  Server = 'server',
  Emulator = 'emulator',
  Controller = 'controller'
}

export interface FlowDebugTarget {
  id: string;
  kind: FlowDebugTargetKind;
  label: string;
  controllerTemplateId?: string;
  controllerTemplateRevision?: number;
}

const requiredFunctions = [
  FlowFunctionType.And,
  FlowFunctionType.Not,
  FlowFunctionType.Or,
  FlowFunctionType.ReadPoint,
  FlowFunctionType.WritePoint
];

export const isControllerDebugCompatible = (template: ControllerTemplateSummary): boolean =>
  template.id !== 'default' &&
  template.revision > 0 &&
  template.capabilities.pointTypes.includes(AutomationPointValueType.Digital) &&
  template.capabilities.pointDirections.includes(DataDirectionType.Input) &&
  template.capabilities.pointDirections.includes(DataDirectionType.Output) &&
  template.capabilities.connectorDataTypes.includes(ConnectorDataType.Boolean) &&
  template.capabilities.runtimeFeatures.includes(ControllerRuntimeFeatureType.PhysicalPoints) &&
  requiredFunctions.every((kind) => template.capabilities.flowFunctions.includes(kind));

export const getFlowDebugTargets = (
  templates: readonly ControllerTemplateSummary[]
): FlowDebugTarget[] => [
  {
    id: 'server',
    kind: FlowDebugTargetKind.Server,
    label: 'Server',
    controllerTemplateId: 'default',
    controllerTemplateRevision: 1
  },
  ...templates.filter(isControllerDebugCompatible).map((template) => ({
    id: `emulator:${template.id}`,
    kind: FlowDebugTargetKind.Emulator,
    label: `Emulator — ${template.name}`,
    controllerTemplateId: template.id,
    controllerTemplateRevision: template.revision
  })),
  ...templates.filter(isControllerDebugCompatible).map((template) => ({
    id: `controller:${template.id}`,
    kind: FlowDebugTargetKind.Controller,
    label: template.name,
    controllerTemplateId: template.id,
    controllerTemplateRevision: template.revision
  }))
];
