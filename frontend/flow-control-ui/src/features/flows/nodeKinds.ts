import type { FlowNodeConnector, FlowNodeKind } from './types';

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
export const nodeKindRegistry: Record<FlowNodeKind, NodeKindDefinition> = {
  calculator: {
    kind: 'calculator',
    label: 'Calculator',
    category: 'maths',
    icon: '∑',
    color: '#ef8354',
    defaultSize: { width: 210, height: 64 },
    connectors: [
      { id: 'input', label: 'Values', direction: 'input', dataType: 'number', side: 'left' },
      { id: 'output', label: 'Result', direction: 'output', dataType: 'number', side: 'right' }
    ],
    editor: [
      { key: 'operation', label: 'Operation', input: 'select', options: ['average', 'sum'] }
    ],
    defaultConfiguration: { operation: 'average' }
  },
  override: {
    kind: 'override',
    label: 'Override',
    category: 'logic',
    icon: '↯',
    color: '#65d6ad',
    defaultSize: { width: 210, height: 64 },
    connectors: [
      { id: 'input', label: 'Automatic', direction: 'input', dataType: 'any', side: 'left' },
      { id: 'output', label: 'Effective', direction: 'output', dataType: 'any', side: 'right' }
    ],
    editor: [{ key: 'enabled', label: 'Override enabled', input: 'checkbox' }],
    defaultConfiguration: { enabled: false }
  },
  pulse: {
    kind: 'pulse',
    label: 'Pulse',
    category: 'timing',
    icon: '⌁',
    color: '#f5b942',
    defaultSize: { width: 210, height: 64 },
    connectors: [
      { id: 'input', label: 'Trigger', direction: 'input', dataType: 'any', side: 'left' },
      { id: 'output', label: 'Pulse', direction: 'output', dataType: 'any', side: 'right' }
    ],
    editor: [{ key: 'durationSeconds', label: 'Duration (seconds)', input: 'number' }],
    defaultConfiguration: { durationSeconds: 30 }
  },
  split: {
    kind: 'split',
    label: 'Split',
    category: 'routing',
    icon: '⑂',
    color: '#64a7ff',
    defaultSize: { width: 210, height: 64 },
    connectors: [
      { id: 'input', label: 'Source', direction: 'input', dataType: 'any', side: 'left' },
      { id: 'output', label: 'Routes', direction: 'output', dataType: 'any', side: 'right' }
    ],
    editor: [{ key: 'outputs', label: 'Output count', input: 'number' }],
    defaultConfiguration: { outputs: 2 }
  }
};

export const flowNodeKinds = Object.keys(nodeKindRegistry) as FlowNodeKind[];

export const getNodeKind = (kind: FlowNodeKind): NodeKindDefinition => nodeKindRegistry[kind];
