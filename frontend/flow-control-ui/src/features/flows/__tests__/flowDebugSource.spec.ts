import { describe, expect, it } from 'vitest';
import { createDefaultNode } from '@/features/flows/graph/createNode';
import {
  createExecutableFlowSource,
  FlowDebugSourceError,
  graphRevision
} from '@/features/flows/flowDebugSource';
import type { FlowDebugTarget } from '@/features/flows/debugTargets';
import type { FlowDefinition } from '@/features/flows/types';

const target: FlowDebugTarget = {
  id: 'controller:controller-a',
  label: 'Controller A',
  kind: 'controller',
  controllerTemplateId: 'controller-a',
  controllerTemplateRevision: 2
};

const debugFlow = (): FlowDefinition => {
  const input = createDefaultNode('digitalInput', { x: 0, y: 0 }, 0, 'input');
  input.configuration.pointId = 'button-1';
  const inverter = createDefaultNode('not', { x: 200, y: 0 }, 1, 'invert');
  const output = createDefaultNode('digitalOutput', { x: 400, y: 0 }, 2, 'output');
  output.configuration.pointId = 'relay-1';
  return {
    id: 'flow-a',
    name: 'Digital shadow flow',
    description: '',
    status: 'draft',
    disabled: false,
    updatedAt: '2026-01-01T00:00:00Z',
    nodes: [input, inverter, output],
    connections: [
      {
        id: 'input-to-not',
        start: { nodeId: 'input', connectorId: 'value' },
        end: { nodeId: 'invert', connectorId: 'in' }
      },
      {
        id: 'not-to-output',
        start: { nodeId: 'invert', connectorId: 'value' },
        end: { nodeId: 'output', connectorId: 'in' }
      }
    ]
  };
};

describe('designer debug source', () => {
  it('creates the exact schema-1 compiler contract from designer nodes', () => {
    const flow = debugFlow();
    const source = createExecutableFlowSource(flow, target);
    expect(source.nodes).toEqual([
      { id: 'input', kind: 'digitalInput', configuration: { pointId: 'button-1' }, label: 'New Digital Input', x: 0, y: 0, zOrder: 0 },
      { id: 'invert', kind: 'not', configuration: {}, label: 'New Not', x: 200, y: 0, zOrder: 1 },
      { id: 'output', kind: 'digitalOutput', configuration: { pointId: 'relay-1' }, label: 'New Digital Output', x: 400, y: 0, zOrder: 2 }
    ]);
    expect(source.connections[0]).toEqual({
      source: { nodeId: 'input', portId: 'value' },
      target: { nodeId: 'invert', portId: 'in' }
    });
    expect(source.revision).toBe(graphRevision(flow));
  });

  it('rejects unsupported designer nodes before contacting hardware', () => {
    const flow = debugFlow();
    flow.nodes[1] = createDefaultNode('timer', { x: 0, y: 0 }, 1, 'timer');
    expect(() => createExecutableFlowSource(flow, target)).toThrow(FlowDebugSourceError);
    expect(() => createExecutableFlowSource(flow, target)).toThrow(/unsupported debug function/);
  });

  it('changes revision whenever the graph changes', () => {
    const flow = debugFlow();
    const revision = graphRevision(flow);
    flow.nodes[0]!.configuration.pointId = 'button-2';
    expect(graphRevision(flow)).not.toBe(revision);
  });
});
