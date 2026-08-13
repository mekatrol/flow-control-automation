import { FlowNodeFunctionType, type FlowNodeConnector, type FlowNodeKind } from './types';

export interface NodeEditorField {
  key: string;
  label: string;
  input: 'checkbox' | 'number' | 'select' | 'text';
  options?: string[];
}

export interface NodeKindDefinition {
  kind: FlowNodeKind;
  label: string;
  category: 'logic' | 'maths' | 'override' | 'routing' | 'timing';
  icon: string;
  defaultSize: { width: number; height: number };
  connectors: FlowNodeConnector[];
  editor: NodeEditorField[];
  defaultConfiguration: Record<string, boolean | number | string | null>;
  executable: boolean;
}

const defaultNodeSize = (): NodeKindDefinition['defaultSize'] => ({ width: 170, height: 40 });

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
const booleanPort = (
  id: string,
  label: string,
  direction: FlowNodeConnector['direction'],
  side: FlowNodeConnector['side']
): FlowNodeConnector => ({ id, label, direction, dataType: 'boolean', side });
const executableDefinition = (
  kind: FlowNodeFunctionType,
  connectors: FlowNodeConnector[],
  editor: NodeEditorField[] = [],
  defaultConfiguration: NodeKindDefinition['defaultConfiguration'] = {}
): NodeKindDefinition => ({
  kind,
  label: kind.replace(/([A-Z])/g, ' $1').replace(/^./, (letter) => letter.toUpperCase()),
  category: 'logic',
  icon:
    kind === FlowNodeFunctionType.Memory
      ? 'delay'
      : kind === FlowNodeFunctionType.DigitalInput
        ? 'trigger'
        : kind === FlowNodeFunctionType.DigitalOutput
          ? 'override'
          : kind === FlowNodeFunctionType.DigitalConstant
            ? 'line'
            : kind.toLowerCase(),
  defaultSize: defaultNodeSize(),
  connectors,
  editor,
  defaultConfiguration,
  executable: true
});
const definition = (
  kind: FlowNodeFunctionType,
  category: NodeKindDefinition['category'],
  icon: string,
  connectors = anyConnectors()
): NodeKindDefinition => ({
  kind,
  label: kind.charAt(0).toUpperCase() + kind.slice(1),
  category,
  icon,
  defaultSize: defaultNodeSize(),
  connectors,
  editor: [{ key: 'enabled', label: 'Enabled', input: 'checkbox' }],
  defaultConfiguration: { enabled: true },
  executable: false
});

export const nodeKindRegistry: Record<FlowNodeKind, NodeKindDefinition> = {
  [FlowNodeFunctionType.Add]: executableDefinition(FlowNodeFunctionType.Add, [
    { id: 'a', label: 'A', direction: 'input', dataType: 'number', side: 'left' },
    { id: 'b', label: 'B', direction: 'input', dataType: 'number', side: 'left' },
    { id: 'value', label: 'Value', direction: 'output', dataType: 'number', side: 'right' }
  ]),
  [FlowNodeFunctionType.And]: executableDefinition(FlowNodeFunctionType.And, [
    booleanPort('a', 'A', 'input', 'left'),
    booleanPort('b', 'B', 'input', 'left'),
    booleanPort('value', 'Value', 'output', 'right')
  ]),
  [FlowNodeFunctionType.Average]: definition(
    FlowNodeFunctionType.Average,
    'maths',
    'average',
    numberConnectors()
  ),
  [FlowNodeFunctionType.Calculator]: {
    kind: FlowNodeFunctionType.Calculator,
    label: 'Calculator',
    category: 'maths',
    icon: 'calculator',
    defaultSize: defaultNodeSize(),
    connectors: [
      {
        id: 'analogue-input',
        label: 'Analogue input',
        direction: 'input',
        dataType: 'number',
        side: 'left'
      },
      {
        id: 'digital-input',
        label: 'Digital input',
        direction: 'input',
        dataType: 'boolean',
        side: 'left'
      },
      {
        id: 'analogue-output',
        label: 'Analogue output',
        direction: 'output',
        dataType: 'number',
        side: 'right'
      },
      {
        id: 'digital-output',
        label: 'Digital output',
        direction: 'output',
        dataType: 'boolean',
        side: 'right'
      }
    ],
    executable: false,
    editor: [
      { key: 'operation', label: 'Operation', input: 'select', options: ['average', 'sum'] }
    ],
    defaultConfiguration: { operation: 'average' }
  },
  [FlowNodeFunctionType.Calendar]: definition(FlowNodeFunctionType.Calendar, 'timing', 'calendar'),
  [FlowNodeFunctionType.Clamp]: definition(
    FlowNodeFunctionType.Clamp,
    'maths',
    'clamp',
    numberConnectors()
  ),
  [FlowNodeFunctionType.Comparator]: executableDefinition(
    FlowNodeFunctionType.Comparator,
    [
      { id: 'a', label: 'A', direction: 'input', dataType: 'number', side: 'left' },
      { id: 'b', label: 'B', direction: 'input', dataType: 'number', side: 'left' },
      booleanPort('value', 'Value', 'output', 'right')
    ],
    [{ key: 'operator', label: 'Operator', input: 'select', options: ['lt', 'lte', 'eq', 'gte', 'gt', 'ne'] }],
    { operator: 'gt' }
  ),
  [FlowNodeFunctionType.Delay]: definition(FlowNodeFunctionType.Delay, 'timing', 'delay'),
  [FlowNodeFunctionType.DigitalConstant]: executableDefinition(
    FlowNodeFunctionType.DigitalConstant,
    [booleanPort('value', 'Value', 'output', 'right')],
    [{ key: 'value', label: 'Value', input: 'checkbox' }],
    { value: false }
  ),
  [FlowNodeFunctionType.DigitalInput]: executableDefinition(
    FlowNodeFunctionType.DigitalInput,
    [booleanPort('value', 'Value', 'output', 'right')],
    [{ key: 'pointId', label: 'Input point ID', input: 'text' }],
    { pointId: 'input-point' }
  ),
  [FlowNodeFunctionType.DigitalOutput]: executableDefinition(
    FlowNodeFunctionType.DigitalOutput,
    [booleanPort('in', 'Input', 'input', 'left')],
    [{ key: 'pointId', label: 'Output point ID', input: 'text' }],
    { pointId: 'output-point' }
  ),
  [FlowNodeFunctionType.If]: definition(FlowNodeFunctionType.If, 'logic', 'if'),
  [FlowNodeFunctionType.Line]: definition(
    FlowNodeFunctionType.Line,
    'maths',
    'line',
    numberConnectors()
  ),
  [FlowNodeFunctionType.LevelShifter]: executableDefinition(
    FlowNodeFunctionType.LevelShifter,
    [
      { id: 'in', label: 'Input', direction: 'input', dataType: 'number', side: 'left' },
      { id: 'value', label: 'Value', direction: 'output', dataType: 'number', side: 'right' }
    ],
    [
      { key: 'gain', label: 'Gain', input: 'number' },
      { key: 'offset', label: 'Offset', input: 'number' }
    ],
    { gain: 1, offset: 0 }
  ),
  [FlowNodeFunctionType.Max]: definition(
    FlowNodeFunctionType.Max,
    'maths',
    'max',
    numberConnectors()
  ),
  [FlowNodeFunctionType.Min]: definition(
    FlowNodeFunctionType.Min,
    'maths',
    'min',
    numberConnectors()
  ),
  [FlowNodeFunctionType.Memory]: executableDefinition(
    FlowNodeFunctionType.Memory,
    [
      booleanPort('in', 'Input', 'input', 'left'),
      booleanPort('value', 'Previous value', 'output', 'right')
    ],
    [{ key: 'value', label: 'Initial value', input: 'checkbox' }],
    { value: false }
  ),
  [FlowNodeFunctionType.Nand]: executableDefinition(FlowNodeFunctionType.Nand, [
    booleanPort('a', 'A', 'input', 'left'),
    booleanPort('b', 'B', 'input', 'left'),
    booleanPort('value', 'Value', 'output', 'right')
  ]),
  [FlowNodeFunctionType.Nor]: executableDefinition(FlowNodeFunctionType.Nor, [
    booleanPort('a', 'A', 'input', 'left'),
    booleanPort('b', 'B', 'input', 'left'),
    booleanPort('value', 'Value', 'output', 'right')
  ]),
  [FlowNodeFunctionType.NumericConstant]: executableDefinition(
    FlowNodeFunctionType.NumericConstant,
    [{ id: 'value', label: 'Value', direction: 'output', dataType: 'number', side: 'right' }],
    [{ key: 'value', label: 'Value', input: 'number' }],
    { value: 0 }
  ),
  [FlowNodeFunctionType.Not]: executableDefinition(FlowNodeFunctionType.Not, [
    booleanPort('in', 'Input', 'input', 'left'),
    booleanPort('value', 'Value', 'output', 'right')
  ]),
  [FlowNodeFunctionType.Or]: executableDefinition(FlowNodeFunctionType.Or, [
    booleanPort('a', 'A', 'input', 'left'),
    booleanPort('b', 'B', 'input', 'left'),
    booleanPort('value', 'Value', 'output', 'right')
  ]),
  [FlowNodeFunctionType.OnDelay]: executableDefinition(
    FlowNodeFunctionType.OnDelay,
    [
      booleanPort('in', 'Input', 'input', 'left'),
      booleanPort('value', 'Elapsed', 'output', 'right')
    ],
    [{ key: 'durationMs', label: 'Duration (ms)', input: 'number' }],
    { durationMs: 1000 }
  ),
  [FlowNodeFunctionType.Override]: {
    kind: FlowNodeFunctionType.Override,
    label: 'Override',
    category: 'override',
    icon: 'override',
    defaultSize: defaultNodeSize(),
    connectors: [
      { id: 'input', label: 'Automatic', direction: 'input', dataType: 'any', side: 'left' },
      { id: 'output', label: 'Effective', direction: 'output', dataType: 'any', side: 'right' }
    ],
    executable: false,
    editor: [{ key: 'enabled', label: 'Override enabled', input: 'checkbox' }],
    defaultConfiguration: { enabled: false }
  },
  [FlowNodeFunctionType.Pulse]: {
    kind: FlowNodeFunctionType.Pulse,
    label: 'Pulse',
    category: 'timing',
    icon: 'pulse',
    defaultSize: defaultNodeSize(),
    connectors: [
      { id: 'input', label: 'Trigger', direction: 'input', dataType: 'any', side: 'left' },
      { id: 'output', label: 'Pulse', direction: 'output', dataType: 'any', side: 'right' }
    ],
    executable: false,
    editor: [{ key: 'durationSeconds', label: 'Duration (seconds)', input: 'number' }],
    defaultConfiguration: { durationSeconds: 30 }
  },
  [FlowNodeFunctionType.QualityGood]: executableDefinition(FlowNodeFunctionType.QualityGood, [
    booleanPort('in', 'Input', 'input', 'left'),
    booleanPort('value', 'Good', 'output', 'right')
  ]),
  [FlowNodeFunctionType.RisingEdge]: executableDefinition(FlowNodeFunctionType.RisingEdge, [
    booleanPort('in', 'Input', 'input', 'left'),
    booleanPort('value', 'Event', 'output', 'right')
  ]),
  [FlowNodeFunctionType.Schedule]: definition(FlowNodeFunctionType.Schedule, 'timing', 'schedule'),
  [FlowNodeFunctionType.Selector]: definition(FlowNodeFunctionType.Selector, 'routing', 'selector'),
  [FlowNodeFunctionType.Sequence]: definition(FlowNodeFunctionType.Sequence, 'routing', 'sequence'),
  [FlowNodeFunctionType.Split]: {
    kind: FlowNodeFunctionType.Split,
    label: 'Split',
    category: 'routing',
    icon: 'split',
    defaultSize: defaultNodeSize(),
    connectors: [
      { id: 'input', label: 'Source', direction: 'input', dataType: 'any', side: 'left' },
      {
        id: 'analogue-output',
        label: 'Analogue route',
        direction: 'output',
        dataType: 'number',
        side: 'right'
      },
      {
        id: 'digital-output',
        label: 'Digital route',
        direction: 'output',
        dataType: 'boolean',
        side: 'right'
      }
    ],
    executable: false,
    editor: [{ key: 'outputs', label: 'Output count', input: 'number' }],
    defaultConfiguration: { outputs: 2 }
  },
  [FlowNodeFunctionType.Timer]: definition(FlowNodeFunctionType.Timer, 'timing', 'timer'),
  [FlowNodeFunctionType.Xnor]: executableDefinition(FlowNodeFunctionType.Xnor, [
    booleanPort('a', 'A', 'input', 'left'),
    booleanPort('b', 'B', 'input', 'left'),
    booleanPort('value', 'Value', 'output', 'right')
  ]),
  [FlowNodeFunctionType.Xor]: executableDefinition(FlowNodeFunctionType.Xor, [
    booleanPort('a', 'A', 'input', 'left'),
    booleanPort('b', 'B', 'input', 'left'),
    booleanPort('value', 'Value', 'output', 'right')
  ])
};

export const flowNodeKinds = (Object.keys(nodeKindRegistry) as FlowNodeKind[]).filter(
  (kind) => nodeKindRegistry[kind].executable
);

export const getNodeKind = (kind: FlowNodeKind): NodeKindDefinition => nodeKindRegistry[kind];

// Vite may serve the application below a Home Assistant add-on path. Building
// icon URLs from BASE_URL keeps the migrated assets working in that deployment.
export const getNodeIconUrl = (icon: string): string =>
  `${import.meta.env.BASE_URL}icons/flow-nodes/${icon}.svg`;
