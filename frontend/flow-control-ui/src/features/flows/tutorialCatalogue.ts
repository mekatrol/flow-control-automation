import { createDefaultNode } from '@/features/flows/graph/createNode';
import { flowNodeKinds, getNodeKind } from '@/features/flows/nodeKinds';
import type { FlowDefinition, FlowNodeKind } from '@/features/flows/types';

export interface FlowTutorial {
  schemaVersion: 1;
  id: string;
  title: string;
  functionKind: FlowNodeKind;
  category: string;
  objective: string;
  prerequisites: string[];
  guidance: { title: string; instruction: string; observation: string }[];
  flow: FlowDefinition;
}

const tutorialFlow = (kind: FlowNodeKind): FlowDefinition => ({
  id: `tutorial-${kind}`,
  name: `${getNodeKind(kind).label} tutorial`,
  description: `Disposable simulator example for ${getNodeKind(kind).label}.`,
  status: 'draft',
  disabled: false,
  updatedAt: '2026-08-15T00:00:00.000Z',
  nodes: [createDefaultNode(kind, { x: 240, y: 160 }, 0, `tutorial-${kind}-node`)],
  connections: []
});

const createTutorial = (kind: FlowNodeKind): FlowTutorial => {
  const definition = getNodeKind(kind);
  return {
    schemaVersion: 1,
    id: `${kind}-basics`,
    title: `${definition.label} basics`,
    functionKind: kind,
    category: definition.category,
    objective: `Understand how the ${definition.label} block transforms its inputs during a scan.`,
    prerequisites: ['Simulator basics'],
    guidance: [
      {
        title: 'Inspect the block',
        instruction: 'Review the labelled connectors and configuration before running the example.',
        observation: 'Inputs enter on the left and committed outputs leave on the right.'
      },
      {
        title: 'Try an input',
        instruction:
          'Connect interface terminals, enter an input value, then choose Apply inputs and step.',
        observation: 'Connector overlays show the typed value and quality for the committed scan.'
      },
      {
        title: 'Change and compare',
        instruction: 'Change one input or configuration value and step again.',
        observation: `The ${definition.label} output changes according to its portable VM semantics.`
      }
    ],
    flow: tutorialFlow(kind)
  };
};

export const parseTutorial = (value: unknown): FlowTutorial => {
  if (typeof value !== 'object' || value === null)
    throw new TypeError('Tutorial must be an object.');
  const item = value as Partial<FlowTutorial>;
  if (
    item.schemaVersion !== 1 ||
    typeof item.id !== 'string' ||
    typeof item.title !== 'string' ||
    typeof item.functionKind !== 'string' ||
    !flowNodeKinds.includes(item.functionKind as FlowNodeKind) ||
    typeof item.category !== 'string' ||
    typeof item.objective !== 'string' ||
    !Array.isArray(item.prerequisites) ||
    !Array.isArray(item.guidance) ||
    typeof item.flow !== 'object' ||
    item.flow === null
  )
    throw new TypeError('Tutorial fields are invalid or unsupported.');
  if (!item.flow.nodes.some((node) => node.kind === item.functionKind))
    throw new TypeError('Tutorial flow does not contain its function block.');
  return item as FlowTutorial;
};

export const flowTutorials: FlowTutorial[] = flowNodeKinds
  .filter((kind) => getNodeKind(kind).executable)
  .map(createTutorial)
  .map(parseTutorial);

export const tutorialForKind = (kind: FlowNodeKind): FlowTutorial | undefined =>
  flowTutorials.find((tutorial) => tutorial.functionKind === kind);
