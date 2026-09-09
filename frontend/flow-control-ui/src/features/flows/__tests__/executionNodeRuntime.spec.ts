import { describe, expect, it } from 'vitest';

import { createExecutionNodeRuntime } from '@/features/flows/executionNodeRuntime';
import type { FlowDefinition } from '@/features/flows/types';
import { DataType, FlowNodeType } from '@/types/serverTypes';

const flow = {
  id: 'flow-a',
  nodes: [
    {
      id: 'function-a',
      nodeType: FlowNodeType.Not,
      configuration: {},
      connectors: [],
      label: 'Not',
      x: 0,
      y: 0,
      zOrder: 0
    }
  ],
  connections: []
} as unknown as FlowDefinition;

describe('createExecutionNodeRuntime', () => {
  it('shows an inspected function value when no completed node snapshot is available', () => {
    const runtime = createExecutionNodeRuntime({
      flow,
      flowId: flow.id,
      inspection: {
        instructionPointer: 1,
        isAtCommit: false,
        slots: [],
        currentState: [],
        stagedNextState: [],
        proposedOutputs: [],
        nodeValues: { 'function-a': { type: DataType.Boolean, value: true } }
      },
      state: 'running'
    });

    expect(runtime.nodes['function-a']?.value).toBe('true');
  });

  it('uses the same snapshot-first value precedence for every execution view', () => {
    const runtime = createExecutionNodeRuntime({
      flow,
      flowId: flow.id,
      snapshot: {
        debugSessionId: 'debug-a',
        flowId: flow.id,
        revision: 1,
        lifecycleState: 'paused',
        mode: 'continuous',
        tickNumber: 1,
        sampledAtMs: 1,
        completedAtMs: 1,
        executionDurationUs: 1,
        inputValidity: [],
        nodes: [
          {
            nodeId: 'function-a',
            state: 'completed',
            quality: 'good',
            typedValue: { type: DataType.Number, number: 42 }
          }
        ],
        proposedOutputs: [],
        overrunCount: 0,
        evaluationFailureCount: 0,
        lastReasonCode: 0,
        lastReason: '',
        lastReasonPath: ''
      },
      inspection: {
        instructionPointer: 1,
        isAtCommit: false,
        slots: [],
        currentState: [],
        stagedNextState: [],
        proposedOutputs: [],
        nodeValues: { 'function-a': { type: DataType.Number, number: 7 } }
      },
      state: 'running'
    });

    expect(runtime.nodes['function-a']?.value).toBe('42');
  });

  it('maps a generated virtual write value back to its visual node', () => {
    const runtime = createExecutionNodeRuntime({
      flow,
      flowId: flow.id,
      inspection: {
        instructionPointer: 1,
        isAtCommit: false,
        slots: [],
        currentState: [],
        stagedNextState: [],
        proposedOutputs: [],
        nodeValues: { 'function-a--write': { type: DataType.Boolean, value: false } }
      },
      state: 'running'
    });

    expect(runtime.nodes['function-a']?.value).toBe('false');
  });

  it('uses a proposed output when the output node has no runtime slot', () => {
    const outputFlow = {
      ...flow,
      nodes: [{ ...flow.nodes[0], configuration: { pointId: 'virtual-a' } }]
    } as FlowDefinition;
    const runtime = createExecutionNodeRuntime({
      flow: outputFlow,
      flowId: flow.id,
      snapshot: {
        debugSessionId: 'debug-a',
        flowId: flow.id,
        revision: 1,
        lifecycleState: 'running',
        mode: 'continuous',
        tickNumber: 1,
        sampledAtMs: 1,
        completedAtMs: 1,
        executionDurationUs: 1,
        inputValidity: [],
        nodes: [],
        proposedOutputs: [
          {
            pointId: 'virtual-a',
            state: 'proposed',
            quality: 'good',
            proposedValue: true,
            typedValue: { type: DataType.Boolean, value: true }
          }
        ],
        overrunCount: 0,
        evaluationFailureCount: 0,
        lastReasonCode: 0,
        lastReason: '',
        lastReasonPath: ''
      },
      state: 'running'
    });

    expect(runtime.nodes['function-a']?.value).toBe('true');
  });
});
