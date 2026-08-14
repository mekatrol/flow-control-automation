/* eslint-disable max-len */
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
  category: NodeKindDefinition['category'] = 'logic'
): NodeKindDefinition => ({
  kind,
  label: kind.replace(/([A-Z])/g, ' $1').replace(/^./, (letter) => letter.toUpperCase()),
  category,
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

export const nodeKindRegistry: Record<FlowNodeKind, NodeKindDefinition> = {
  [FlowNodeFunctionType.Add]: executableDefinition(
    FlowNodeFunctionType.Add,
    [
      { id: 'a', label: 'A', direction: 'input', dataType: 'number', side: 'left' },
      { id: 'b', label: 'B', direction: 'input', dataType: 'number', side: 'left' },
      { id: 'value', label: 'Value', direction: 'output', dataType: 'number', side: 'right' }
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
    'routing'
  ),
  [FlowNodeFunctionType.AnalogOutput]: executableDefinition(
    FlowNodeFunctionType.AnalogOutput,
    [{ id: 'in', label: 'Input', direction: 'input', dataType: 'number', side: 'left' }],
    [{ key: 'pointId', label: 'Output point ID', input: 'text' }],
    { pointId: 'analog-output-point' },
    'routing'
  ),
  [FlowNodeFunctionType.And]: executableDefinition(FlowNodeFunctionType.And, [
    booleanPort('a', 'A', 'input', 'left'),
    booleanPort('b', 'B', 'input', 'left'),
    booleanPort('value', 'Value', 'output', 'right')
  ]),
  [FlowNodeFunctionType.Average]: executableDefinition(
    FlowNodeFunctionType.Average,
    numberConnectors(), [], {}, 'maths'
  ),
  [FlowNodeFunctionType.Calculator]: executableDefinition(FlowNodeFunctionType.Calculator, numberConnectors(), [], {}, 'maths'),
  [FlowNodeFunctionType.Calendar]: executableDefinition(FlowNodeFunctionType.Calendar, [booleanPort('output', 'Active', 'output', 'right')], [{ key: 'enabled', label: 'Enabled', input: 'checkbox' }], { enabled: true }, 'timing'),
  [FlowNodeFunctionType.Clamp]: executableDefinition(
    FlowNodeFunctionType.Clamp,
    numberConnectors(), [{ key: 'minimum', label: 'Minimum', input: 'number' }, { key: 'maximum', label: 'Maximum', input: 'number' }], { minimum: 0, maximum: 100 }, 'maths'
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
  [FlowNodeFunctionType.Delay]: executableDefinition(FlowNodeFunctionType.Delay, [booleanPort('input', 'Input', 'input', 'left'), booleanPort('output', 'Elapsed', 'output', 'right')], [{ key: 'durationMs', label: 'Duration (ms)', input: 'number' }], { durationMs: 1000 }, 'timing'),
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
  [FlowNodeFunctionType.FlowInput]: executableDefinition(
    FlowNodeFunctionType.FlowInput,
    [
      { id: 'value', label: 'Interface value', direction: 'output', dataType: 'any', side: 'right' }
    ],
    [{ key: 'interfaceId', label: 'Flow input', input: 'text' }],
    { interfaceId: '' },
    'routing'
  ),
  [FlowNodeFunctionType.FlowOutput]: executableDefinition(
    FlowNodeFunctionType.FlowOutput,
    [{ id: 'value', label: 'Interface value', direction: 'input', dataType: 'any', side: 'left' }],
    [{ key: 'interfaceId', label: 'Flow output', input: 'text' }],
    { interfaceId: '' },
    'routing'
  ),
  [FlowNodeFunctionType.If]: executableDefinition(FlowNodeFunctionType.If, [booleanPort('condition','Condition','input','left'), booleanPort('whenTrue','True','input','left'), booleanPort('whenFalse','False','input','left'), booleanPort('value','Value','output','right')]),
  [FlowNodeFunctionType.Line]: executableDefinition(
    FlowNodeFunctionType.Line,
    numberConnectors(), [{ key: 'gain', label: 'Gain', input: 'number' }, { key: 'offset', label: 'Offset', input: 'number' }], { gain: 1, offset: 0 }, 'maths'
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
    [{ id: 'a', label: 'A', direction: 'input', dataType: 'number', side: 'left' }, { id: 'b', label: 'B', direction: 'input', dataType: 'number', side: 'left' }, { id: 'value', label: 'Value', direction: 'output', dataType: 'number', side: 'right' }], [], {}, 'maths'
  ),
  [FlowNodeFunctionType.Min]: executableDefinition(
    FlowNodeFunctionType.Min,
    [{ id: 'a', label: 'A', direction: 'input', dataType: 'number', side: 'left' }, { id: 'b', label: 'B', direction: 'input', dataType: 'number', side: 'left' }, { id: 'value', label: 'Value', direction: 'output', dataType: 'number', side: 'right' }], [], {}, 'maths'
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
  [FlowNodeFunctionType.Override]: executableDefinition(FlowNodeFunctionType.Override, [booleanPort('input','Automatic','input','left'), booleanPort('output','Effective','output','right')], [], {}, 'override'),
  [FlowNodeFunctionType.Pulse]: executableDefinition(FlowNodeFunctionType.Pulse, [booleanPort('input','Trigger','input','left'), booleanPort('output','Pulse','output','right')], [], {}, 'timing'),
  [FlowNodeFunctionType.QualityGood]: executableDefinition(FlowNodeFunctionType.QualityGood, [
    booleanPort('in', 'Input', 'input', 'left'),
    booleanPort('value', 'Good', 'output', 'right')
  ]),
  [FlowNodeFunctionType.RisingEdge]: executableDefinition(FlowNodeFunctionType.RisingEdge, [
    booleanPort('in', 'Input', 'input', 'left'),
    booleanPort('value', 'Event', 'output', 'right')
  ]),
  [FlowNodeFunctionType.Schedule]: executableDefinition(FlowNodeFunctionType.Schedule, [booleanPort('output','Active','output','right')], [{ key: 'enabled', label: 'Enabled', input: 'checkbox' }], { enabled: true }, 'timing'),
  [FlowNodeFunctionType.Selector]: executableDefinition(FlowNodeFunctionType.Selector, [booleanPort('condition','Condition','input','left'), { id:'a',label:'A',direction:'input',dataType:'number',side:'left' }, { id:'b',label:'B',direction:'input',dataType:'number',side:'left' }, { id:'value',label:'Value',direction:'output',dataType:'number',side:'right' }], [], {}, 'routing'),
  [FlowNodeFunctionType.Sequence]: executableDefinition(FlowNodeFunctionType.Sequence, [booleanPort('a','A','input','left'), booleanPort('b','B','input','left'), booleanPort('value','Value','output','right')], [], {}, 'routing'),
  [FlowNodeFunctionType.Split]: executableDefinition(FlowNodeFunctionType.Split, [booleanPort('input','Source','input','left'), booleanPort('output','Route','output','right')], [], {}, 'routing'),
  [FlowNodeFunctionType.Timer]: executableDefinition(FlowNodeFunctionType.Timer, [booleanPort('input','Input','input','left'), booleanPort('output','Elapsed','output','right')], [{ key: 'durationMs', label: 'Duration (ms)', input: 'number' }], { durationMs: 1000 }, 'timing'),
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

export const getNodeKind = (kind: FlowNodeKind): NodeKindDefinition => nodeKindRegistry[kind];

// Vite may serve the application below a Home Assistant add-on path. Building
// icon URLs from BASE_URL keeps the migrated assets working in that deployment.
export const getNodeIconUrl = (icon: string): string =>
  `${import.meta.env.BASE_URL}icons/flow-nodes/${icon}.svg`;
