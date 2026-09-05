// JSON wire enums shared with backend/Server/Server.Common/Types.
// Values follow FlowControlJson camelCase serialization and explicit member names.
// Const objects preserve literal JSON assignability while exposing named enum constants.

export const AutomationPointValueType = {
  Analog: 'analog',
  Digital: 'digital',
  MultiState: 'multiState',
  Integer: 'integer',
  Text: 'text'
} as const;
export type AutomationPointValueType =
  (typeof AutomationPointValueType)[keyof typeof AutomationPointValueType];

export const ConnectorDataType = {
  Any: 'any',
  Boolean: 'boolean',
  Event: 'event',
  Number: 'number',
  String: 'string'
} as const;
export type ConnectorDataType = (typeof ConnectorDataType)[keyof typeof ConnectorDataType];

export const ControllerPointFeatureType = {
  Read: 'read',
  Command: 'command',
  Retain: 'retain',
  Override: 'override',
  Relinquish: 'relinquish',
  Quality: 'quality',
  Alarms: 'alarms',
  Trends: 'trends'
} as const;
export type ControllerPointFeatureType =
  (typeof ControllerPointFeatureType)[keyof typeof ControllerPointFeatureType];

export const ControllerRuntimeFeatureType = {
  VirtualPoints: 'virtualPoints',
  PhysicalPoints: 'physicalPoints',
  CommandArbitration: 'commandArbitration',
  QualityPropagation: 'qualityPropagation'
} as const;
export type ControllerRuntimeFeatureType =
  (typeof ControllerRuntimeFeatureType)[keyof typeof ControllerRuntimeFeatureType];

export const DataDirectionType = {
  Input: 'input',
  Output: 'output',
  InputOutput: 'inputOutput',
  Value: 'value'
} as const;
export type DataDirectionType = (typeof DataDirectionType)[keyof typeof DataDirectionType];

export const DataQualityType = {
  Good: 'good',
  Bad: 'bad',
  Uncertain: 'uncertain',
  Unavailable: 'unavailable'
} as const;
export type DataQualityType = (typeof DataQualityType)[keyof typeof DataQualityType];

export const DataType = {
  Any: 'any',
  Boolean: 'boolean',
  Number: 'number',
  String: 'string',
  Event: 'event'
} as const;
export type DataType = (typeof DataType)[keyof typeof DataType];

export const ExecutionContextDeploymentStatusType = {
  Draft: 'draft',
  Active: 'active',
  Disabled: 'disabled',
  Failed: 'failed'
} as const;
export type ExecutionContextDeploymentStatusType =
  (typeof ExecutionContextDeploymentStatusType)[keyof typeof ExecutionContextDeploymentStatusType];

export const ExecutionInstanceType = {
  Server: 'server',
  Controller: 'controller'
} as const;
export type ExecutionInstanceType =
  (typeof ExecutionInstanceType)[keyof typeof ExecutionInstanceType];

export const ExecutionModeType = {
  Event: 'event',
  Interval: 'interval'
} as const;
export type ExecutionModeType = (typeof ExecutionModeType)[keyof typeof ExecutionModeType];

export const FlowExecutionModeType = {
  Manual: 'manual'
} as const;
export type FlowExecutionModeType =
  (typeof FlowExecutionModeType)[keyof typeof FlowExecutionModeType];

export const FlowFunctionType = {
  And: 'and',
  Average: 'average',
  Calculator: 'calculator',
  Calendar: 'calendar',
  Clamp: 'clamp',
  Comparator: 'comparator',
  Delay: 'delay',
  DigitalSwitch: 'digitalSwitch',
  LevelShifter: 'levelShifter',
  Line: 'line',
  Max: 'max',
  Min: 'min',
  Nand: 'nand',
  Nor: 'nor',
  Not: 'not',
  Or: 'or',
  Override: 'override',
  PointChanged: 'pointChanged',
  Pulse: 'pulse',
  ReadPoint: 'readPoint',
  ReleasePointCommand: 'releasePointCommand',
  Schedule: 'schedule',
  AnalogSwitch: 'analogSwitch',
  Sequence: 'sequence',
  Split: 'split',
  Timer: 'timer',
  WritePoint: 'writePoint',
  Xnor: 'xnor',
  Xor: 'xor',
  A2D: 'a2d',
  D2A: 'd2a',
  Subtract: 'subtract',
  Multiply: 'multiply',
  Divide: 'divide',
  Power: 'power',
  Negate: 'negate'
} as const;
export type FlowFunctionType = (typeof FlowFunctionType)[keyof typeof FlowFunctionType];

export const FlowNodeType = {
  Unknown: 'unknown',
  DigitalInput: 'digitalInput',
  DigitalOutput: 'digitalOutput',
  DigitalConstant: 'digitalConstant',
  AnalogInput: 'analogInput',
  AnalogOutput: 'analogOutput',
  Not: 'not',
  And: 'and',
  Nand: 'nand',
  Or: 'or',
  Nor: 'nor',
  Xor: 'xor',
  Xnor: 'xnor',
  Memory: 'memory',
  QualityGood: 'qualityGood',
  AnalogConstant: 'analogConstant',
  Add: 'add',
  Comparator: 'comparator',
  LevelShifter: 'levelShifter',
  OnDelay: 'onDelay',
  RisingEdge: 'risingEdge',
  Average: 'average',
  Calculator: 'calculator',
  Clamp: 'clamp',
  Min: 'min',
  Max: 'max',
  Line: 'line',
  DigitalSwitch: 'digitalSwitch',
  AnalogSwitch: 'analogSwitch',
  Split: 'split',
  Sequence: 'sequence',
  Override: 'override',
  Delay: 'delay',
  Timer: 'timer',
  Pulse: 'pulse',
  Schedule: 'schedule',
  Calendar: 'calendar',
  A2D: 'a2d',
  D2A: 'd2a',
  Subtract: 'subtract',
  Multiply: 'multiply',
  Divide: 'divide',
  Power: 'power',
  Negate: 'negate',
  Counter: 'counter',
  Clock: 'clock',
  AnalogVirtual: 'analogVirtual',
  DigitalVirtual: 'digitalVirtual'
} as const;
export type FlowNodeType = (typeof FlowNodeType)[keyof typeof FlowNodeType];

export const InputQualityPolicyType = {
  RequireGood: 'requireGood',
  Propagate: 'propagate'
} as const;
export type InputQualityPolicyType =
  (typeof InputQualityPolicyType)[keyof typeof InputQualityPolicyType];

export const VirtualPointPersistenceType = {
  Volatile: 'volatile',
  Retained: 'retained'
} as const;
export type VirtualPointPersistenceType =
  (typeof VirtualPointPersistenceType)[keyof typeof VirtualPointPersistenceType];

export const PointSourceType = {
  Physical: 'physical',
  Virtual: 'virtual',
  Remote: 'remote'
} as const;
export type PointSourceType = (typeof PointSourceType)[keyof typeof PointSourceType];

export const isEnumValue = <T extends string>(
  enumeration: Readonly<Record<string, T>>,
  value: unknown
): value is T => typeof value === 'string' && Object.values(enumeration).includes(value as T);
