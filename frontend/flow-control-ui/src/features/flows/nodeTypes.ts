import { DataDirectionType, DataType, VirtualPointPersistenceType } from '@/types/serverTypes';
import { FlowNodeType, type FlowNodeConnector } from './types';

export interface NodeEditorField {
  key: string;
  label: string;
  input: 'checkbox' | 'number' | 'select' | 'text';
  options?: string[];
}

export interface FlowNodeTypeDefinition {
  nodeType: FlowNodeType;
  label: string;
  category: 'io' | 'control' | 'timing' | 'maths';
  icon: string;
  defaultSize: { width: number; height: number };
  connectors: FlowNodeConnector[];
  editor: NodeEditorField[];
  defaultConfiguration: Record<string, boolean | number | string | null>;
  executable: boolean;
}

// A shared footprint keeps mixed node types aligned. The width accommodates the
// longest built-in label and the extra height gives clustered ports more room.
const defaultNodeSize = (): FlowNodeTypeDefinition['defaultSize'] => ({ width: 200, height: 60 });

// Each node type declares everything the palette, canvas, and inspector need.
// Keeping these concerns together prevents their labels, connectors, and
// configuration defaults from drifting into incompatible versions.
const numberConnectors = (): FlowNodeConnector[] => [
  {
    id: 'input',
    label: 'Values',
    direction: DataDirectionType.Input,
    dataType: DataType.Number,
    side: 'left'
  },
  {
    id: 'output',
    label: 'Result',
    direction: DataDirectionType.Output,
    dataType: DataType.Number,
    side: 'right'
  }
];
const binaryArithmetic = (type: FlowNodeType): FlowNodeTypeDefinition =>
  executableDefinition(
    type,
    [
      {
        id: 'a',
        label: 'A',
        direction: DataDirectionType.Input,
        dataType: DataType.Number,
        side: 'left'
      },
      {
        id: 'b',
        label: 'B',
        direction: DataDirectionType.Input,
        dataType: DataType.Number,
        side: 'left'
      },
      {
        id: 'value',
        label: 'Value',
        direction: DataDirectionType.Output,
        dataType: DataType.Number,
        side: 'right'
      },
      {
        id: 'error',
        label: 'Error',
        direction: DataDirectionType.Output,
        dataType: DataType.Boolean,
        side: 'right'
      }
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
): FlowNodeConnector => ({ id, label, direction, dataType: DataType.Boolean, side });
const executableDefinition = (
  type: FlowNodeType,
  connectors: FlowNodeConnector[],
  editor: NodeEditorField[] = [],
  defaultConfiguration: FlowNodeTypeDefinition['defaultConfiguration'] = {},
  category: FlowNodeTypeDefinition['category'] = 'control'
): FlowNodeTypeDefinition => ({
  nodeType: type,
  label:
    type === FlowNodeType.A2D
      ? 'A2D'
      : type === FlowNodeType.D2A
        ? 'D2A'
        : type.replace(/([A-Z])/g, ' $1').replace(/^./, (letter) => letter.toUpperCase()),
  category,
  icon:
    type === FlowNodeType.Memory
      ? 'delay'
      : type === FlowNodeType.A2D
        ? 'comparator'
        : type === FlowNodeType.D2A
          ? 'analogswitch'
          : type === FlowNodeType.AnalogInput
            ? 'analogoutput'
            : type === FlowNodeType.AnalogOutput
              ? 'analoginput'
              : type === FlowNodeType.DigitalInput
                ? 'trigger'
                : type === FlowNodeType.DigitalOutput
                  ? 'override'
                  : type.toLowerCase(),
  defaultSize: defaultNodeSize(),
  connectors,
  editor,
  defaultConfiguration,
  executable: true
});

// Unknown is a backend sentinel and cannot be authored in the designer.
export const nodeTypeRegistry: Record<
  Exclude<FlowNodeType, typeof FlowNodeType.Unknown>,
  FlowNodeTypeDefinition
> = {
  [FlowNodeType.Subtract]: binaryArithmetic(FlowNodeType.Subtract),
  [FlowNodeType.Multiply]: binaryArithmetic(FlowNodeType.Multiply),
  [FlowNodeType.Divide]: binaryArithmetic(FlowNodeType.Divide),
  [FlowNodeType.Power]: binaryArithmetic(FlowNodeType.Power),
  [FlowNodeType.Negate]: executableDefinition(
    FlowNodeType.Negate,
    [
      {
        id: 'in',
        label: 'Input',
        direction: DataDirectionType.Input,
        dataType: DataType.Number,
        side: 'left'
      },
      {
        id: 'value',
        label: 'Value',
        direction: DataDirectionType.Output,
        dataType: DataType.Number,
        side: 'right'
      },
      booleanPort('error', 'Error', DataDirectionType.Output, 'right')
    ],
    [],
    {},
    'maths'
  ),
  [FlowNodeType.A2D]: executableDefinition(
    FlowNodeType.A2D,
    [
      {
        id: 'in',
        label: 'Input',
        direction: DataDirectionType.Input,
        dataType: DataType.Number,
        side: 'left'
      },
      booleanPort('value', 'Value', DataDirectionType.Output, 'right')
    ],
    [
      { key: 'activeLowThreshold', label: 'Active low threshold', input: 'number' },
      { key: 'activeHighThreshold', label: 'Active high threshold', input: 'number' }
    ],
    { activeLowThreshold: 0, activeHighThreshold: 100 },
    'control'
  ),
  [FlowNodeType.Add]: executableDefinition(
    FlowNodeType.Add,
    [
      {
        id: 'a',
        label: 'A',
        direction: DataDirectionType.Input,
        dataType: DataType.Number,
        side: 'left'
      },
      {
        id: 'b',
        label: 'B',
        direction: DataDirectionType.Input,
        dataType: DataType.Number,
        side: 'left'
      },
      {
        id: 'value',
        label: 'Value',
        direction: DataDirectionType.Output,
        dataType: DataType.Number,
        side: 'right'
      },
      booleanPort('error', 'Error', DataDirectionType.Output, 'right')
    ],
    [],
    {},
    'maths'
  ),
  [FlowNodeType.AnalogInput]: executableDefinition(
    FlowNodeType.AnalogInput,
    [
      {
        id: 'value',
        label: 'Value',
        direction: DataDirectionType.Output,
        dataType: DataType.Number,
        side: 'right'
      }
    ],
    [{ key: 'pointId', label: 'Input point ID', input: 'text' }],
    { pointId: 'analog-input-point' },
    'io'
  ),
  [FlowNodeType.AnalogOutput]: executableDefinition(
    FlowNodeType.AnalogOutput,
    [
      {
        id: 'in',
        label: 'Input',
        direction: DataDirectionType.Input,
        dataType: DataType.Number,
        side: 'left'
      }
    ],
    [{ key: 'pointId', label: 'Output point ID', input: 'text' }],
    { pointId: 'analog-output-point' },
    'io'
  ),
  [FlowNodeType.AnalogVirtual]: executableDefinition(
    FlowNodeType.AnalogVirtual,
    [
      {
        id: 'in',
        label: 'Set',
        direction: DataDirectionType.Input,
        dataType: DataType.Number,
        side: 'left'
      },
      {
        id: 'value',
        label: 'Value',
        direction: DataDirectionType.Output,
        dataType: DataType.Number,
        side: 'right'
      }
    ],
    [
      { key: 'pointId', label: 'Virtual point key', input: 'text' },
      { key: 'units', label: 'Units', input: 'text' },
      {
        key: 'persistence',
        label: 'Persistence',
        input: 'select',
        options: Object.values(VirtualPointPersistenceType)
      },
      { key: 'relinquishDefault', label: 'Optional default', input: 'number' }
    ],
    {
      pointId: 'analog-virtual-point',
      units: '',
      persistence: VirtualPointPersistenceType.Volatile,
      relinquishDefault: null
    },
    'io'
  ),
  [FlowNodeType.And]: executableDefinition(FlowNodeType.And, [
    booleanPort('a', 'A', DataDirectionType.Input, 'left'),
    booleanPort('b', 'B', DataDirectionType.Input, 'left'),
    booleanPort('value', 'Value', DataDirectionType.Output, 'right')
  ]),
  [FlowNodeType.Average]: executableDefinition(
    FlowNodeType.Average,
    [
      {
        id: 'a',
        label: 'A',
        direction: DataDirectionType.Input,
        dataType: DataType.Number,
        side: 'left'
      },
      {
        id: 'b',
        label: 'B',
        direction: DataDirectionType.Input,
        dataType: DataType.Number,
        side: 'left'
      },
      {
        id: 'output',
        label: 'Average',
        direction: DataDirectionType.Output,
        dataType: DataType.Number,
        side: 'right'
      },
      booleanPort('error', 'Error', DataDirectionType.Output, 'right')
    ],
    [],
    {},
    'maths'
  ),
  [FlowNodeType.Calculator]: executableDefinition(
    FlowNodeType.Calculator,
    [
      {
        id: 'a',
        label: 'A',
        direction: DataDirectionType.Input,
        dataType: DataType.Number,
        side: 'left'
      },
      {
        id: 'b',
        label: 'B',
        direction: DataDirectionType.Input,
        dataType: DataType.Number,
        side: 'left'
      },
      {
        id: 'c',
        label: 'C',
        direction: DataDirectionType.Input,
        dataType: DataType.Number,
        side: 'left'
      },
      {
        id: 'output',
        label: 'Output',
        direction: DataDirectionType.Output,
        dataType: DataType.Number,
        side: 'right'
      }
    ],
    [{ key: 'formula', label: 'Formula', input: 'text' }],
    { formula: 'a * b + c' },
    'maths'
  ),
  [FlowNodeType.Calendar]: executableDefinition(
    FlowNodeType.Calendar,
    [booleanPort('output', 'Active', DataDirectionType.Output, 'right')],
    [{ key: 'enabled', label: 'Enabled', input: 'checkbox' }],
    { enabled: true },
    'timing'
  ),
  [FlowNodeType.Clamp]: executableDefinition(
    FlowNodeType.Clamp,
    numberConnectors(),
    [
      { key: 'minimum', label: 'Minimum', input: 'number' },
      { key: 'maximum', label: 'Maximum', input: 'number' }
    ],
    { minimum: 0, maximum: 100 },
    'maths'
  ),
  [FlowNodeType.Comparator]: executableDefinition(
    FlowNodeType.Comparator,
    [
      {
        id: 'a',
        label: 'A',
        direction: DataDirectionType.Input,
        dataType: DataType.Number,
        side: 'left'
      },
      {
        id: 'b',
        label: 'B',
        direction: DataDirectionType.Input,
        dataType: DataType.Number,
        side: 'left'
      },
      booleanPort('value', 'Value', DataDirectionType.Output, 'right')
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
  [FlowNodeType.Counter]: executableDefinition(FlowNodeType.Counter, [
    booleanPort('count', 'Count', DataDirectionType.Input, 'left'),
    booleanPort('reset', 'Reset', DataDirectionType.Input, 'left'),
    {
      id: 'value',
      label: 'Count',
      direction: DataDirectionType.Output,
      dataType: DataType.Number,
      side: 'right'
    }
  ]),
  [FlowNodeType.Clock]: executableDefinition(
    FlowNodeType.Clock,
    [
      booleanPort('enable', 'Enable', DataDirectionType.Input, 'left'),
      booleanPort('output', 'Clock', DataDirectionType.Output, 'right')
    ],
    [
      { key: 'frequencyHz', label: 'Frequency (Hz)', input: 'number' },
      { key: 'dutyCycle', label: 'Duty cycle (%)', input: 'number' }
    ],
    { frequencyHz: 1, dutyCycle: 50 },
    'timing'
  ),
  [FlowNodeType.Delay]: executableDefinition(
    FlowNodeType.Delay,
    [
      booleanPort('input', 'Input', DataDirectionType.Input, 'left'),
      booleanPort('output', 'Elapsed', DataDirectionType.Output, 'right')
    ],
    [{ key: 'durationMs', label: 'Duration (ms)', input: 'number' }],
    { durationMs: 1000 },
    'timing'
  ),
  [FlowNodeType.DigitalConstant]: executableDefinition(
    FlowNodeType.DigitalConstant,
    [booleanPort('value', 'Value', DataDirectionType.Output, 'right')],
    [{ key: 'value', label: 'Value', input: 'checkbox' }],
    { value: false },
    'io'
  ),
  [FlowNodeType.DigitalInput]: executableDefinition(
    FlowNodeType.DigitalInput,
    [booleanPort('value', 'Value', DataDirectionType.Output, 'right')],
    [{ key: 'pointId', label: 'Input point ID', input: 'text' }],
    { pointId: 'input-point' },
    'io'
  ),
  [FlowNodeType.DigitalOutput]: executableDefinition(
    FlowNodeType.DigitalOutput,
    [booleanPort('in', 'Input', DataDirectionType.Input, 'left')],
    [{ key: 'pointId', label: 'Output point ID', input: 'text' }],
    { pointId: 'output-point' },
    'io'
  ),
  [FlowNodeType.DigitalVirtual]: executableDefinition(
    FlowNodeType.DigitalVirtual,
    [
      booleanPort('in', 'Set', DataDirectionType.Input, 'left'),
      booleanPort('value', 'Value', DataDirectionType.Output, 'right')
    ],
    [
      { key: 'pointId', label: 'Virtual point key', input: 'text' },
      {
        key: 'persistence',
        label: 'Persistence',
        input: 'select',
        options: Object.values(VirtualPointPersistenceType)
      },
      { key: 'relinquishDefault', label: 'Default value', input: 'checkbox' }
    ],
    {
      pointId: 'digital-virtual-point',
      persistence: VirtualPointPersistenceType.Volatile,
      relinquishDefault: false
    },
    'io'
  ),
  [FlowNodeType.DigitalSwitch]: executableDefinition(FlowNodeType.DigitalSwitch, [
    booleanPort('condition', 'Condition', DataDirectionType.Input, 'left'),
    booleanPort('whenTrue', 'True', DataDirectionType.Input, 'left'),
    booleanPort('whenFalse', 'False', DataDirectionType.Input, 'left'),
    booleanPort('value', 'Value', DataDirectionType.Output, 'right')
  ]),
  [FlowNodeType.D2A]: executableDefinition(
    FlowNodeType.D2A,
    [
      booleanPort('in', 'Input', DataDirectionType.Input, 'left'),
      {
        id: 'value',
        label: 'Value',
        direction: DataDirectionType.Output,
        dataType: DataType.Number,
        side: 'right'
      }
    ],
    [
      { key: 'lowValue', label: 'Low analog value', input: 'number' },
      { key: 'highValue', label: 'High analog value', input: 'number' }
    ],
    { lowValue: 0, highValue: 100 },
    'control'
  ),
  [FlowNodeType.Line]: executableDefinition(
    FlowNodeType.Line,
    numberConnectors(),
    [
      { key: 'gain', label: 'Gain', input: 'number' },
      { key: 'offset', label: 'Offset', input: 'number' }
    ],
    { gain: 1, offset: 0 },
    'maths'
  ),
  [FlowNodeType.LevelShifter]: executableDefinition(
    FlowNodeType.LevelShifter,
    [
      {
        id: 'in',
        label: 'Input',
        direction: DataDirectionType.Input,
        dataType: DataType.Number,
        side: 'left'
      },
      {
        id: 'value',
        label: 'Value',
        direction: DataDirectionType.Output,
        dataType: DataType.Number,
        side: 'right'
      }
    ],
    [
      { key: 'gain', label: 'Gain', input: 'number' },
      { key: 'offset', label: 'Offset', input: 'number' }
    ],
    { gain: 1, offset: 0 }
  ),
  [FlowNodeType.Max]: executableDefinition(
    FlowNodeType.Max,
    [
      {
        id: 'a',
        label: 'A',
        direction: DataDirectionType.Input,
        dataType: DataType.Number,
        side: 'left'
      },
      {
        id: 'b',
        label: 'B',
        direction: DataDirectionType.Input,
        dataType: DataType.Number,
        side: 'left'
      },
      {
        id: 'value',
        label: 'Value',
        direction: DataDirectionType.Output,
        dataType: DataType.Number,
        side: 'right'
      }
    ],
    [],
    {},
    'maths'
  ),
  [FlowNodeType.Min]: executableDefinition(
    FlowNodeType.Min,
    [
      {
        id: 'a',
        label: 'A',
        direction: DataDirectionType.Input,
        dataType: DataType.Number,
        side: 'left'
      },
      {
        id: 'b',
        label: 'B',
        direction: DataDirectionType.Input,
        dataType: DataType.Number,
        side: 'left'
      },
      {
        id: 'value',
        label: 'Value',
        direction: DataDirectionType.Output,
        dataType: DataType.Number,
        side: 'right'
      }
    ],
    [],
    {},
    'maths'
  ),
  [FlowNodeType.Memory]: executableDefinition(
    FlowNodeType.Memory,
    [
      {
        id: 'in',
        label: 'Input',
        direction: DataDirectionType.Input,
        dataType: DataType.Number,
        side: 'left'
      },
      {
        id: 'value',
        label: 'Previous value',
        direction: DataDirectionType.Output,
        dataType: DataType.Number,
        side: 'right'
      }
    ],
    [{ key: 'value', label: 'Initial value', input: 'number' }],
    { value: 0 }
  ),
  [FlowNodeType.Nand]: executableDefinition(FlowNodeType.Nand, [
    booleanPort('a', 'A', DataDirectionType.Input, 'left'),
    booleanPort('b', 'B', DataDirectionType.Input, 'left'),
    booleanPort('value', 'Value', DataDirectionType.Output, 'right')
  ]),
  [FlowNodeType.Nor]: executableDefinition(FlowNodeType.Nor, [
    booleanPort('a', 'A', DataDirectionType.Input, 'left'),
    booleanPort('b', 'B', DataDirectionType.Input, 'left'),
    booleanPort('value', 'Value', DataDirectionType.Output, 'right')
  ]),
  [FlowNodeType.AnalogConstant]: executableDefinition(
    FlowNodeType.AnalogConstant,
    [
      {
        id: 'value',
        label: 'Value',
        direction: DataDirectionType.Output,
        dataType: DataType.Number,
        side: 'right'
      }
    ],
    [{ key: 'value', label: 'Value', input: 'number' }],
    { value: 0 },
    'io'
  ),
  [FlowNodeType.Not]: executableDefinition(FlowNodeType.Not, [
    booleanPort('in', 'Input', DataDirectionType.Input, 'left'),
    booleanPort('value', 'Value', DataDirectionType.Output, 'right')
  ]),
  [FlowNodeType.Or]: executableDefinition(FlowNodeType.Or, [
    booleanPort('a', 'A', DataDirectionType.Input, 'left'),
    booleanPort('b', 'B', DataDirectionType.Input, 'left'),
    booleanPort('value', 'Value', DataDirectionType.Output, 'right')
  ]),
  [FlowNodeType.OnDelay]: executableDefinition(
    FlowNodeType.OnDelay,
    [
      booleanPort('in', 'Input', DataDirectionType.Input, 'left'),
      booleanPort('value', 'Elapsed', DataDirectionType.Output, 'right')
    ],
    [{ key: 'durationMs', label: 'Duration (ms)', input: 'number' }],
    { durationMs: 1000 },
    'timing'
  ),
  [FlowNodeType.Override]: executableDefinition(
    FlowNodeType.Override,
    [
      booleanPort('input', 'Automatic', DataDirectionType.Input, 'left'),
      booleanPort('output', 'Effective', DataDirectionType.Output, 'right')
    ],
    [],
    {},
    'control'
  ),
  [FlowNodeType.Pulse]: executableDefinition(
    FlowNodeType.Pulse,
    [
      booleanPort('input', 'Trigger', DataDirectionType.Input, 'left'),
      booleanPort('output', 'Pulse', DataDirectionType.Output, 'right')
    ],
    [{ key: 'durationMs', label: 'Duration (ms)', input: 'number' }],
    { durationMs: 1000 },
    'timing'
  ),
  [FlowNodeType.QualityGood]: executableDefinition(FlowNodeType.QualityGood, [
    {
      id: 'in',
      label: 'Input',
      direction: DataDirectionType.Input,
      dataType: DataType.Number,
      side: 'left'
    },
    booleanPort('value', 'Good', DataDirectionType.Output, 'right')
  ]),
  [FlowNodeType.RisingEdge]: executableDefinition(FlowNodeType.RisingEdge, [
    booleanPort('in', 'Input', DataDirectionType.Input, 'left'),
    booleanPort('value', 'Event', DataDirectionType.Output, 'right')
  ]),
  [FlowNodeType.Schedule]: executableDefinition(
    FlowNodeType.Schedule,
    [booleanPort('output', 'Active', DataDirectionType.Output, 'right')],
    [{ key: 'enabled', label: 'Enabled', input: 'checkbox' }],
    { enabled: true },
    'timing'
  ),
  [FlowNodeType.AnalogSwitch]: executableDefinition(
    FlowNodeType.AnalogSwitch,
    [
      booleanPort('condition', 'Condition', DataDirectionType.Input, 'left'),
      {
        id: 'a',
        label: 'A',
        direction: DataDirectionType.Input,
        dataType: DataType.Number,
        side: 'left'
      },
      {
        id: 'b',
        label: 'B',
        direction: DataDirectionType.Input,
        dataType: DataType.Number,
        side: 'left'
      },
      {
        id: 'value',
        label: 'Value',
        direction: DataDirectionType.Output,
        dataType: DataType.Number,
        side: 'right'
      }
    ],
    [],
    {},
    'control'
  ),
  [FlowNodeType.Sequence]: executableDefinition(
    FlowNodeType.Sequence,
    [
      booleanPort('a', 'A', DataDirectionType.Input, 'left'),
      booleanPort('b', 'B', DataDirectionType.Input, 'left'),
      booleanPort('value', 'Value', DataDirectionType.Output, 'right')
    ],
    [],
    {},
    'control'
  ),
  [FlowNodeType.Split]: executableDefinition(
    FlowNodeType.Split,
    [
      {
        id: 'input',
        label: 'Source',
        direction: DataDirectionType.Input,
        dataType: DataType.Number,
        side: 'left'
      },
      {
        id: 'output',
        label: 'Route',
        direction: DataDirectionType.Output,
        dataType: DataType.Number,
        side: 'right'
      }
    ],
    [],
    {},
    'control'
  ),
  [FlowNodeType.Timer]: executableDefinition(
    FlowNodeType.Timer,
    [
      booleanPort('input', 'Input', DataDirectionType.Input, 'left'),
      booleanPort('output', 'Elapsed', DataDirectionType.Output, 'right')
    ],
    [{ key: 'durationMs', label: 'Duration (ms)', input: 'number' }],
    { durationMs: 1000 },
    'timing'
  ),
  [FlowNodeType.Xnor]: executableDefinition(FlowNodeType.Xnor, [
    booleanPort('a', 'A', DataDirectionType.Input, 'left'),
    booleanPort('b', 'B', DataDirectionType.Input, 'left'),
    booleanPort('value', 'Value', DataDirectionType.Output, 'right')
  ]),
  [FlowNodeType.Xor]: executableDefinition(FlowNodeType.Xor, [
    booleanPort('a', 'A', DataDirectionType.Input, 'left'),
    booleanPort('b', 'B', DataDirectionType.Input, 'left'),
    booleanPort('value', 'Value', DataDirectionType.Output, 'right')
  ])
};

export const flowNodeTypes = Object.keys(nodeTypeRegistry) as (keyof typeof nodeTypeRegistry)[];

// Palette availability is intentionally separate from the canonical registry.
// Hidden types must remain registered so existing flows can still be loaded,
// rendered, edited, and deleted while users are prevented from adding new ones.
const defaultHiddenFlowNodeTypes = '';

// Playwright also imports this registry directly in Node while discovering its
// function-node cases, where Vite does not provide import.meta.env.
const viteEnvironment = import.meta.env;
const configuredHiddenFlowNodeTypes =
  viteEnvironment?.VITE_HIDDEN_FLOW_NODE_TYPES ??
  (viteEnvironment?.MODE === 'test' ? '' : defaultHiddenFlowNodeTypes);

const hiddenFlowNodeTypes = new Set(
  configuredHiddenFlowNodeTypes
    .split(',')
    .map((type) => type.trim())
    .filter(Boolean)
);

const unknownHiddenFlowNodeTypes = [...hiddenFlowNodeTypes].filter(
  (type) => !Object.hasOwn(nodeTypeRegistry, type)
);

if (unknownHiddenFlowNodeTypes.length) {
  throw new Error(
    `VITE_HIDDEN_FLOW_NODE_TYPES contains unknown node types: ${unknownHiddenFlowNodeTypes.join(', ')}`
  );
}

export const paletteNodeTypes = flowNodeTypes.filter((type) => !hiddenFlowNodeTypes.has(type));

export const getNodeTypeDefinition = (type: FlowNodeType): FlowNodeTypeDefinition => {
  if (type === FlowNodeType.Unknown) throw new Error('Unknown flow nodes cannot be authored.');
  return nodeTypeRegistry[type];
};

// Vite may serve the application below a Home Assistant add-on path. Building
// icon URLs from BASE_URL keeps the migrated assets working in that deployment.
export const getNodeIconUrl = (icon: string): string =>
  `${import.meta.env.BASE_URL}icons/flow-nodes/${icon}.svg`;
