import { createDefaultNode } from '@/features/flows/graph/createNode';
import { flowNodeTypes, getNodeTypeDefinition } from '@/features/flows/nodeTypes';
import type { FlowDefinition, FlowNodeType } from '@/features/flows/types';

export interface FlowTutorial {
  schemaVersion: 1;
  id: string;
  title: string;
  nodeType: FlowNodeType;
  category: string;
  objective: string;
  prerequisites: string[];
  guidance: { title: string; instruction: string; observation: string }[];
  flow: FlowDefinition;
}

const tutorialFlow = (nodeType: FlowNodeType): FlowDefinition => ({
  id: `tutorial-${nodeType}`,
  name: `${getNodeTypeDefinition(nodeType).label} tutorial`,
  description: `Disposable simulator example for ${getNodeTypeDefinition(nodeType).label}.`,
  status: 'draft',
  disabled: false,
  updatedAt: '2026-08-15T00:00:00.000Z',
  nodes: [createDefaultNode(nodeType, { x: 240, y: 160 }, 0, `tutorial-${nodeType}-node`)],
  connections: []
});

const createTutorial = (nodeType: FlowNodeType): FlowTutorial => {
  const definition = getNodeTypeDefinition(nodeType);
  return {
    schemaVersion: 1,
    id: `${nodeType}-basics`,
    title: `${definition.label} basics`,
    nodeType: nodeType,
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
    flow: tutorialFlow(nodeType)
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
    typeof item.nodeType !== 'string' ||
    !flowNodeTypes.some((nodeType) => nodeType === item.nodeType) ||
    typeof item.category !== 'string' ||
    typeof item.objective !== 'string' ||
    !Array.isArray(item.prerequisites) ||
    !Array.isArray(item.guidance) ||
    typeof item.flow !== 'object' ||
    item.flow === null
  )
    throw new TypeError('Tutorial fields are invalid or unsupported.');
  if (!item.flow.nodes.some((node) => node.nodeType === item.nodeType))
    throw new TypeError('Tutorial flow does not contain its function block.');
  return item as FlowTutorial;
};

export const flowTutorials: FlowTutorial[] = flowNodeTypes
  .filter((nodeType) => getNodeTypeDefinition(nodeType).executable)
  .map(createTutorial)
  .map(parseTutorial);

export const tutorialForNodeType = (nodeType: FlowNodeType): FlowTutorial | undefined =>
  flowTutorials.find((tutorial) => tutorial.nodeType === nodeType);
