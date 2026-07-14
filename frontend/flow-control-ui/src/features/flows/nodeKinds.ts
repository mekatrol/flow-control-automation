import { FlowNodeFunctionType, type FlowNodeConnector, type FlowNodeKind } from './types';

export interface NodeEditorField {
  key: string;
  label: string;
  input: 'checkbox' | 'number' | 'select';
  options?: string[];
}

export interface NodeKindDefinition {
  kind: FlowNodeKind;
  label: string;
  category: 'logic' | 'maths' | 'routing' | 'timing';
  icon: string;
  color: string;
  defaultSize: { width: number; height: number };
  connectors: FlowNodeConnector[];
  editor: NodeEditorField[];
  defaultConfiguration: Record<string, boolean | number | string | null>;
}

// Each node kind declares everything the palette, canvas, and inspector need.
// Keeping these concerns together prevents their labels, connectors, and
// configuration defaults from drifting into incompatible versions.
const numberConnectors = (): FlowNodeConnector[] => [
  { id: 'input', label: 'Values', direction: 'input', dataType: 'number', side: 'left' },
  { id: 'output', label: 'Result', direction: 'output', dataType: 'number', side: 'right' }
];
const anyConnectors = (): FlowNodeConnector[] => [
  { id: 'input', label: 'Input', direction: 'input', dataType: 'any', side: 'left' },
  { id: 'output', label: 'Output', direction: 'output', dataType: 'any', side: 'right' }
];
const definition = (
  kind: FlowNodeFunctionType,
  category: NodeKindDefinition['category'],
  icon: string,
  color: string,
  connectors = anyConnectors()
): NodeKindDefinition => ({
  kind,
  label: kind.charAt(0).toUpperCase() + kind.slice(1),
  category,
  icon,
  color,
  defaultSize: { width: 150, height: 40 },
  connectors,
  editor: [{ key: 'enabled', label: 'Enabled', input: 'checkbox' }],
  defaultConfiguration: { enabled: true }
});

export const nodeKindRegistry: Record<FlowNodeKind, NodeKindDefinition> = {
  [FlowNodeFunctionType.And]: definition(FlowNodeFunctionType.And, 'logic', 'and', '#7f8cff'),
  [FlowNodeFunctionType.Average]: definition(
    FlowNodeFunctionType.Average,
    'maths',
    'average',
    '#ef8354',
    numberConnectors()
  ),
  [FlowNodeFunctionType.Calculator]: {
    kind: FlowNodeFunctionType.Calculator,
    label: 'Calculator',
    category: 'maths',
    icon: 'calculator',
    color: '#ef8354',
    defaultSize: { width: 150, height: 40 },
    connectors: [
      { id: 'input', label: 'Values', direction: 'input', dataType: 'number', side: 'left' },
      { id: 'output', label: 'Result', direction: 'output', dataType: 'number', side: 'right' }
    ],
    editor: [
      { key: 'operation', label: 'Operation', input: 'select', options: ['average', 'sum'] }
    ],
    defaultConfiguration: { operation: 'average' }
  },
  [FlowNodeFunctionType.Calendar]: definition(
    FlowNodeFunctionType.Calendar,
    'timing',
    'calendar',
    '#a879d8'
  ),
  [FlowNodeFunctionType.Clamp]: definition(
    FlowNodeFunctionType.Clamp,
    'maths',
    'clamp',
    '#ef8354',
    numberConnectors()
  ),
  [FlowNodeFunctionType.Comparator]: definition(
    FlowNodeFunctionType.Comparator,
    'logic',
    'comparator',
    '#7f8cff',
    numberConnectors()
  ),
  [FlowNodeFunctionType.Delay]: definition(
    FlowNodeFunctionType.Delay,
    'timing',
    'delay',
    '#f5b942'
  ),
  [FlowNodeFunctionType.If]: definition(FlowNodeFunctionType.If, 'logic', 'if', '#7f8cff'),
  [FlowNodeFunctionType.Invert]: definition(
    FlowNodeFunctionType.Invert,
    'logic',
    'invert',
    '#7f8cff'
  ),
  [FlowNodeFunctionType.Line]: definition(
    FlowNodeFunctionType.Line,
    'maths',
    'line',
    '#ef8354',
    numberConnectors()
  ),
  [FlowNodeFunctionType.Max]: definition(
    FlowNodeFunctionType.Max,
    'maths',
    'max',
    '#ef8354',
    numberConnectors()
  ),
  [FlowNodeFunctionType.Min]: definition(
    FlowNodeFunctionType.Min,
    'maths',
    'min',
    '#ef8354',
    numberConnectors()
  ),
  [FlowNodeFunctionType.Or]: definition(FlowNodeFunctionType.Or, 'logic', 'or', '#7f8cff'),
  [FlowNodeFunctionType.Override]: {
    kind: FlowNodeFunctionType.Override,
    label: 'Override',
    category: 'logic',
    icon: 'override',
    color: '#65d6ad',
    defaultSize: { width: 150, height: 40 },
    connectors: [
      { id: 'input', label: 'Automatic', direction: 'input', dataType: 'any', side: 'left' },
      { id: 'output', label: 'Effective', direction: 'output', dataType: 'any', side: 'right' }
    ],
    editor: [{ key: 'enabled', label: 'Override enabled', input: 'checkbox' }],
    defaultConfiguration: { enabled: false }
  },
  [FlowNodeFunctionType.Pulse]: {
    kind: FlowNodeFunctionType.Pulse,
    label: 'Pulse',
    category: 'timing',
    icon: 'pulse',
    color: '#f5b942',
    defaultSize: { width: 150, height: 40 },
    connectors: [
      { id: 'input', label: 'Trigger', direction: 'input', dataType: 'any', side: 'left' },
      { id: 'output', label: 'Pulse', direction: 'output', dataType: 'any', side: 'right' }
    ],
    editor: [{ key: 'durationSeconds', label: 'Duration (seconds)', input: 'number' }],
    defaultConfiguration: { durationSeconds: 30 }
  },
  [FlowNodeFunctionType.Schedule]: definition(
    FlowNodeFunctionType.Schedule,
    'timing',
    'schedule',
    '#f5b942'
  ),
  [FlowNodeFunctionType.Selector]: definition(
    FlowNodeFunctionType.Selector,
    'routing',
    'selector',
    '#64a7ff'
  ),
  [FlowNodeFunctionType.Sequence]: definition(
    FlowNodeFunctionType.Sequence,
    'routing',
    'sequence',
    '#64a7ff'
  ),
  [FlowNodeFunctionType.Split]: {
    kind: FlowNodeFunctionType.Split,
    label: 'Split',
    category: 'routing',
    icon: 'split',
    color: '#64a7ff',
    defaultSize: { width: 150, height: 40 },
    connectors: [
      { id: 'input', label: 'Source', direction: 'input', dataType: 'any', side: 'left' },
      { id: 'output', label: 'Routes', direction: 'output', dataType: 'any', side: 'right' }
    ],
    editor: [{ key: 'outputs', label: 'Output count', input: 'number' }],
    defaultConfiguration: { outputs: 2 }
  },
  [FlowNodeFunctionType.Timer]: definition(
    FlowNodeFunctionType.Timer,
    'timing',
    'timer',
    '#f5b942'
  ),
  [FlowNodeFunctionType.Xnor]: definition(FlowNodeFunctionType.Xnor, 'logic', 'xnor', '#7f8cff'),
  [FlowNodeFunctionType.Xor]: definition(FlowNodeFunctionType.Xor, 'logic', 'xor', '#7f8cff')
};

export const flowNodeKinds = Object.keys(nodeKindRegistry) as FlowNodeKind[];

export const getNodeKind = (kind: FlowNodeKind): NodeKindDefinition => nodeKindRegistry[kind];

// Vite may serve the application below a Home Assistant add-on path. Building
// icon URLs from BASE_URL keeps the migrated assets working in that deployment.
export const getNodeIconUrl = (icon: string): string =>
  `${import.meta.env.BASE_URL}icons/flow-nodes/${icon}.svg`;
