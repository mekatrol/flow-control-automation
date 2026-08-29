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
  category: 'io' | 'control' | 'timing' | 'maths';
  icon: string;
  defaultSize: { width: number; height: number };
  connectors: FlowNodeConnector[];
  editor: NodeEditorField[];
  defaultConfiguration: Record<string, boolean | number | string | null>;
  executable: boolean;
}

// A shared footprint keeps mixed node kinds aligned. The width accommodates the
// longest built-in label and the extra height gives clustered ports more room.
const defaultNodeSize = (): NodeKindDefinition['defaultSize'] => ({ width: 200, height: 60 });

// Each node kind declares everything the palette, canvas, and inspector need.
// Keeping these concerns together prevents their labels, connectors, and
// configuration defaults from drifting into incompatible versions.
const numberConnectors = (): FlowNodeConnector[] => [
  { id: 'input', label: 'Values', direction: 'input', dataType: 'number', side: 'left' },
  { id: 'output', label: 'Result', direction: 'output', dataType: 'number', side: 'right' }
];
const binaryArithmetic = (kind: FlowNodeFunctionType): NodeKindDefinition =>
  executableDefinition(
    kind,
    [
      { id: 'a', label: 'A', direction: 'input', dataType: 'number', side: 'left' },
      { id: 'b', label: 'B', direction: 'input', dataType: 'number', side: 'left' },
      { id: 'value', label: 'Value', direction: 'output', dataType: 'number', side: 'right' },
      { id: 'error', label: 'Error', direction: 'output', dataType: 'boolean', side: 'right' }
    ],
    [],
    {},
    'maths'
  );
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
  defaultConfiguration: NodeKindDefinition['defaultConfiguration'] = {},
  category: NodeKindDefinition['category'] = 'control'
): NodeKindDefinition => ({
  kind,
  label:
    kind === FlowNodeFunctionType.A2D
      ? 'A2D'
      : kind === FlowNodeFunctionType.D2A
        ? 'D2A'
        : kind.replace(/([A-Z])/g, ' $1').replace(/^./, (letter) => letter.toUpperCase()),
  category,
  icon:
    kind === FlowNodeFunctionType.Memory
      ? 'delay'
      : kind === FlowNodeFunctionType.A2D
        ? 'comparator'
        : kind === FlowNodeFunctionType.D2A
          ? 'analogswitch'
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

export const nodeKindRegistry: Record<FlowNodeKind, NodeKindDefinition> = {
  [FlowNodeFunctionType.Subtract]: binaryArithmetic(FlowNodeFunctionType.Subtract),
  [FlowNodeFunctionType.Multiply]: binaryArithmetic(FlowNodeFunctionType.Multiply),
  [FlowNodeFunctionType.Divide]: binaryArithmetic(FlowNodeFunctionType.Divide),
  [FlowNodeFunctionType.Power]: binaryArithmetic(FlowNodeFunctionType.Power),
  [FlowNodeFunctionType.Negate]: executableDefinition(
    FlowNodeFunctionType.Negate,
    [
      { id: 'in', label: 'Input', direction: 'input', dataType: 'number', side: 'left' },
      { id: 'value', label: 'Value', direction: 'output', dataType: 'number', side: 'right' },
      booleanPort('error', 'Error', 'output', 'right')
    ],
    [],
    {},
    'maths'
  ),
  [FlowNodeFunctionType.A2D]: executableDefinition(
    FlowNodeFunctionType.A2D,
    [
      { id: 'in', label: 'Input', direction: 'input', dataType: 'number', side: 'left' },
      booleanPort('value', 'Value', 'output', 'right')
    ],
    [
      { key: 'activeLowThreshold', label: 'Active low threshold', input: 'number' },
      { key: 'activeHighThreshold', label: 'Active high threshold', input: 'number' }
    ],
    { activeLowThreshold: 0, activeHighThreshold: 100 },
    'control'
  ),
  [FlowNodeFunctionType.Add]: executableDefinition(
    FlowNodeFunctionType.Add,
    [
      { id: 'a', label: 'A', direction: 'input', dataType: 'number', side: 'left' },
      { id: 'b', label: 'B', direction: 'input', dataType: 'number', side: 'left' },
      { id: 'value', label: 'Value', direction: 'output', dataType: 'number', side: 'right' },
      booleanPort('error', 'Error', 'output', 'right')
    ],
    [],
    {},
    'maths'
  ),
  [FlowNodeFunctionType.AnalogInput]: executableDefinition(
    FlowNodeFunctionType.AnalogInput,
    [{ id: 'value', label: 'Value', direction: 'output', dataType: 'number', side: 'right' }],
    [{ key: 'pointId', label: 'Input point ID', input: 'text' }],
    { pointId: 'analog-input-point' },
    'io'
  ),
  [FlowNodeFunctionType.AnalogOutput]: executableDefinition(
    FlowNodeFunctionType.AnalogOutput,
    [{ id: 'in', label: 'Input', direction: 'input', dataType: 'number', side: 'left' }],
    [{ key: 'pointId', label: 'Output point ID', input: 'text' }],
    { pointId: 'analog-output-point' },
    'io'
  ),
  [FlowNodeFunctionType.And]: executableDefinition(FlowNodeFunctionType.And, [
    booleanPort('a', 'A', 'input', 'left'),
    booleanPort('b', 'B', 'input', 'left'),
    booleanPort('value', 'Value', 'output', 'right')
  ]),
  [FlowNodeFunctionType.Average]: executableDefinition(
    FlowNodeFunctionType.Average,
    [
      { id: 'a', label: 'A', direction: 'input', dataType: 'number', side: 'left' },
      { id: 'b', label: 'B', direction: 'input', dataType: 'number', side: 'left' },
      { id: 'output', label: 'Average', direction: 'output', dataType: 'number', side: 'right' },
      booleanPort('error', 'Error', 'output', 'right')
    ],
    [],
    {},
    'maths'
  ),
  [FlowNodeFunctionType.Calculator]: executableDefinition(
    FlowNodeFunctionType.Calculator,
    [
      { id: 'a', label: 'A', direction: 'input', dataType: 'number', side: 'left' },
      { id: 'b', label: 'B', direction: 'input', dataType: 'number', side: 'left' },
      { id: 'c', label: 'C', direction: 'input', dataType: 'number', side: 'left' },
      { id: 'output', label: 'Output', direction: 'output', dataType: 'number', side: 'right' }
    ],
    [{ key: 'formula', label: 'Formula', input: 'text' }],
    { formula: 'a * b + c' },
    'maths'
  ),
  [FlowNodeFunctionType.Calendar]: executableDefinition(
    FlowNodeFunctionType.Calendar,
    [booleanPort('output', 'Active', 'output', 'right')],
    [{ key: 'enabled', label: 'Enabled', input: 'checkbox' }],
    { enabled: true },
    'timing'
  ),
  [FlowNodeFunctionType.Clamp]: executableDefinition(
    FlowNodeFunctionType.Clamp,
    numberConnectors(),
    [
      { key: 'minimum', label: 'Minimum', input: 'number' },
      { key: 'maximum', label: 'Maximum', input: 'number' }
    ],
    { minimum: 0, maximum: 100 },
    'maths'
  ),
  [FlowNodeFunctionType.Comparator]: executableDefinition(
    FlowNodeFunctionType.Comparator,
    [
      { id: 'a', label: 'A', direction: 'input', dataType: 'number', side: 'left' },
      { id: 'b', label: 'B', direction: 'input', dataType: 'number', side: 'left' },
      booleanPort('value', 'Value', 'output', 'right')
    ],
    [
      {
        key: 'operator',
        label: 'Operator',
        input: 'select',
        options: ['lt', 'lte', 'eq', 'gte', 'gt', 'ne']
      }
    ],
    { operator: 'gt' }
  ),
  [FlowNodeFunctionType.Counter]: executableDefinition(FlowNodeFunctionType.Counter, [
    booleanPort('count', 'Count', 'input', 'left'),
    booleanPort('reset', 'Reset', 'input', 'left'),
    { id: 'value', label: 'Count', direction: 'output', dataType: 'number', side: 'right' }
  ]),
  [FlowNodeFunctionType.Clock]: executableDefinition(
    FlowNodeFunctionType.Clock,
    [
      booleanPort('enable', 'Enable', 'input', 'left'),
      booleanPort('output', 'Clock', 'output', 'right')
    ],
    [
      { key: 'frequencyHz', label: 'Frequency (Hz)', input: 'number' },
      { key: 'dutyCycle', label: 'Duty cycle (%)', input: 'number' }
    ],
    { frequencyHz: 1, dutyCycle: 50 },
    'timing'
  ),
  [FlowNodeFunctionType.Delay]: executableDefinition(
    FlowNodeFunctionType.Delay,
    [
      booleanPort('input', 'Input', 'input', 'left'),
      booleanPort('output', 'Elapsed', 'output', 'right')
    ],
    [{ key: 'durationMs', label: 'Duration (ms)', input: 'number' }],
    { durationMs: 1000 },
    'timing'
  ),
  [FlowNodeFunctionType.DigitalConstant]: executableDefinition(
    FlowNodeFunctionType.DigitalConstant,
    [booleanPort('value', 'Value', 'output', 'right')],
    [{ key: 'value', label: 'Value', input: 'checkbox' }],
    { value: false },
    'io'
  ),
  [FlowNodeFunctionType.DigitalInput]: executableDefinition(
    FlowNodeFunctionType.DigitalInput,
    [booleanPort('value', 'Value', 'output', 'right')],
    [{ key: 'pointId', label: 'Input point ID', input: 'text' }],
    { pointId: 'input-point' },
    'io'
  ),
  [FlowNodeFunctionType.DigitalOutput]: executableDefinition(
    FlowNodeFunctionType.DigitalOutput,
    [booleanPort('in', 'Input', 'input', 'left')],
    [{ key: 'pointId', label: 'Output point ID', input: 'text' }],
    { pointId: 'output-point' },
    'io'
  ),
  [FlowNodeFunctionType.DigitalSwitch]: executableDefinition(FlowNodeFunctionType.DigitalSwitch, [
    booleanPort('condition', 'Condition', 'input', 'left'),
    booleanPort('whenTrue', 'True', 'input', 'left'),
    booleanPort('whenFalse', 'False', 'input', 'left'),
    booleanPort('value', 'Value', 'output', 'right')
  ]),
  [FlowNodeFunctionType.D2A]: executableDefinition(
    FlowNodeFunctionType.D2A,
    [
      booleanPort('in', 'Input', 'input', 'left'),
      { id: 'value', label: 'Value', direction: 'output', dataType: 'number', side: 'right' }
    ],
    [
      { key: 'lowValue', label: 'Low analog value', input: 'number' },
      { key: 'highValue', label: 'High analog value', input: 'number' }
    ],
    { lowValue: 0, highValue: 100 },
    'control'
  ),
  [FlowNodeFunctionType.Line]: executableDefinition(
    FlowNodeFunctionType.Line,
    numberConnectors(),
    [
      { key: 'gain', label: 'Gain', input: 'number' },
      { key: 'offset', label: 'Offset', input: 'number' }
    ],
    { gain: 1, offset: 0 },
    'maths'
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
  [FlowNodeFunctionType.Max]: executableDefinition(
    FlowNodeFunctionType.Max,
    [
      { id: 'a', label: 'A', direction: 'input', dataType: 'number', side: 'left' },
      { id: 'b', label: 'B', direction: 'input', dataType: 'number', side: 'left' },
      { id: 'value', label: 'Value', direction: 'output', dataType: 'number', side: 'right' }
    ],
    [],
    {},
    'maths'
  ),
  [FlowNodeFunctionType.Min]: executableDefinition(
    FlowNodeFunctionType.Min,
    [
      { id: 'a', label: 'A', direction: 'input', dataType: 'number', side: 'left' },
      { id: 'b', label: 'B', direction: 'input', dataType: 'number', side: 'left' },
      { id: 'value', label: 'Value', direction: 'output', dataType: 'number', side: 'right' }
    ],
    [],
    {},
    'maths'
  ),
  [FlowNodeFunctionType.Memory]: executableDefinition(
    FlowNodeFunctionType.Memory,
    [
      { id: 'in', label: 'Input', direction: 'input', dataType: 'number', side: 'left' },
      {
        id: 'value',
        label: 'Previous value',
        direction: 'output',
        dataType: 'number',
        side: 'right'
      }
    ],
    [{ key: 'value', label: 'Initial value', input: 'number' }],
    { value: 0 }
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
    { value: 0 },
    'io'
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
    { durationMs: 1000 },
    'timing'
  ),
  [FlowNodeFunctionType.Override]: executableDefinition(
    FlowNodeFunctionType.Override,
    [
      booleanPort('input', 'Automatic', 'input', 'left'),
      booleanPort('output', 'Effective', 'output', 'right')
    ],
    [],
    {},
    'control'
  ),
  [FlowNodeFunctionType.Pulse]: executableDefinition(
    FlowNodeFunctionType.Pulse,
    [
      booleanPort('input', 'Trigger', 'input', 'left'),
      booleanPort('output', 'Pulse', 'output', 'right')
    ],
    [{ key: 'durationMs', label: 'Duration (ms)', input: 'number' }],
    { durationMs: 1000 },
    'timing'
  ),
  [FlowNodeFunctionType.QualityGood]: executableDefinition(FlowNodeFunctionType.QualityGood, [
    { id: 'in', label: 'Input', direction: 'input', dataType: 'number', side: 'left' },
    booleanPort('value', 'Good', 'output', 'right')
  ]),
  [FlowNodeFunctionType.RisingEdge]: executableDefinition(FlowNodeFunctionType.RisingEdge, [
    booleanPort('in', 'Input', 'input', 'left'),
    booleanPort('value', 'Event', 'output', 'right')
  ]),
  [FlowNodeFunctionType.Schedule]: executableDefinition(
    FlowNodeFunctionType.Schedule,
    [booleanPort('output', 'Active', 'output', 'right')],
    [{ key: 'enabled', label: 'Enabled', input: 'checkbox' }],
    { enabled: true },
    'timing'
  ),
  [FlowNodeFunctionType.AnalogSwitch]: executableDefinition(
    FlowNodeFunctionType.AnalogSwitch,
    [
      booleanPort('condition', 'Condition', 'input', 'left'),
      { id: 'a', label: 'A', direction: 'input', dataType: 'number', side: 'left' },
      { id: 'b', label: 'B', direction: 'input', dataType: 'number', side: 'left' },
      { id: 'value', label: 'Value', direction: 'output', dataType: 'number', side: 'right' }
    ],
    [],
    {},
    'control'
  ),
  [FlowNodeFunctionType.Sequence]: executableDefinition(
    FlowNodeFunctionType.Sequence,
    [
      booleanPort('a', 'A', 'input', 'left'),
      booleanPort('b', 'B', 'input', 'left'),
      booleanPort('value', 'Value', 'output', 'right')
    ],
    [],
    {},
    'control'
  ),
  [FlowNodeFunctionType.Split]: executableDefinition(
    FlowNodeFunctionType.Split,
    [
      { id: 'input', label: 'Source', direction: 'input', dataType: 'number', side: 'left' },
      { id: 'output', label: 'Route', direction: 'output', dataType: 'number', side: 'right' }
    ],
    [],
    {},
    'control'
  ),
  [FlowNodeFunctionType.Timer]: executableDefinition(
    FlowNodeFunctionType.Timer,
    [
      booleanPort('input', 'Input', 'input', 'left'),
      booleanPort('output', 'Elapsed', 'output', 'right')
    ],
    [{ key: 'durationMs', label: 'Duration (ms)', input: 'number' }],
    { durationMs: 1000 },
    'timing'
  ),
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

export const flowNodeKinds = Object.keys(nodeKindRegistry) as FlowNodeKind[];

// Palette availability is intentionally separate from the canonical registry.
// Hidden kinds must remain registered so existing flows can still be loaded,
// rendered, edited, and deleted while users are prevented from adding new ones.
const defaultHiddenFlowNodeKinds = '';

// Playwright also imports this registry directly in Node while discovering its
// function-node cases, where Vite does not provide import.meta.env.
const viteEnvironment = import.meta.env;
const configuredHiddenFlowNodeKinds =
  viteEnvironment?.VITE_HIDDEN_FLOW_NODE_KINDS ??
  (viteEnvironment?.MODE === 'test' ? '' : defaultHiddenFlowNodeKinds);

const hiddenFlowNodeKinds = new Set(
  configuredHiddenFlowNodeKinds
    .split(',')
    .map((kind) => kind.trim())
    .filter(Boolean)
);

const unknownHiddenFlowNodeKinds = [...hiddenFlowNodeKinds].filter(
  (kind) => !flowNodeKinds.includes(kind as FlowNodeKind)
);

if (unknownHiddenFlowNodeKinds.length) {
  throw new Error(
    `VITE_HIDDEN_FLOW_NODE_KINDS contains unknown node kinds: ${unknownHiddenFlowNodeKinds.join(', ')}`
  );
}

export const paletteNodeKinds = flowNodeKinds.filter((kind) => !hiddenFlowNodeKinds.has(kind));

export const getNodeKind = (kind: FlowNodeKind): NodeKindDefinition => nodeKindRegistry[kind];

// Vite may serve the application below a Home Assistant add-on path. Building
// icon URLs from BASE_URL keeps the migrated assets working in that deployment.
export const getNodeIconUrl = (icon: string): string =>
  `${import.meta.env.BASE_URL}icons/flow-nodes/${icon}.svg`;
