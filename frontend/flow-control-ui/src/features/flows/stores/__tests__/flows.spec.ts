import { createPinia, setActivePinia } from 'pinia';
import { beforeEach, describe, expect, it } from 'vitest';

import { sampleFlows } from '../../__tests__/fixtures/sampleFlows';
import { createDefaultNode } from '../../graph/createNode';
import { useFlowsStore } from '../flows';

describe('flows store', () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    useFlowsStore().replaceAllFlowsFromPayloads(structuredClone(sampleFlows));
  });

  it('starts empty and atomically replaces and removes confirmed API state', () => {
    setActivePinia(createPinia());
    const store = useFlowsStore();
    expect(store.flows).toEqual([]);

    expect(store.replaceAllFlowsFromPayloads(structuredClone(sampleFlows))).toHaveLength(2);
    expect(store.flows.map(({ id }) => id)).toEqual(['climate-control', 'garden-irrigation']);

    const invalid = structuredClone(sampleFlows);
    invalid[0]!.connections[0]!.end.nodeId = 'missing';
    expect(() => store.replaceAllFlowsFromPayloads(invalid)).toThrow(/unknown node/);
    expect(store.flows.map(({ id }) => id)).toEqual(['climate-control', 'garden-irrigation']);

    store.selectFlow('garden-irrigation');
    expect(store.removeConfirmedFlow('garden-irrigation')).toBe(true);
    expect(store.findFlow('garden-irrigation')).toBeUndefined();
    expect(store.activeFlowId).toBeUndefined();
    expect(store.removeConfirmedFlow('garden-irrigation')).toBe(false);
  });

  it('selects a known flow', () => {
    const store = useFlowsStore();

    store.selectFlow('climate-control');

    expect(store.activeFlow?.name).toBe('Climate control');
  });

  it('clears the selection for an unknown flow', () => {
    const store = useFlowsStore();
    store.selectFlow('climate-control');

    store.selectFlow('missing');

    expect(store.activeFlowId).toBeUndefined();
    expect(store.activeFlow).toBeUndefined();
  });

  it('moves a known node without storing pointer state', () => {
    const store = useFlowsStore();

    expect(store.moveNode('climate-control', 'temperature-average', 144, 192)).toBe(true);
    expect(store.findFlow('climate-control')?.nodes[0]).toMatchObject({ x: 144, y: 192 });
    expect(store.moveNode('missing', 'temperature-average', 0, 0)).toBe(false);
    expect(store.moveNode('climate-control', 'missing', 0, 0)).toBe(false);
    expect(JSON.stringify(store.flows)).not.toContain('pointer');
  });

  it('applies z-order commands and reports boundary no-ops', () => {
    const store = useFlowsStore();

    expect(store.reorderNode('climate-control', 'comfort-pulse', 'front')).toBe(true);
    expect(store.findFlow('climate-control')?.nodes.at(-1)?.id).toBe('comfort-pulse');
    expect(store.reorderNode('climate-control', 'comfort-pulse', 'front')).toBe(false);
    expect(store.reorderNode('missing', 'comfort-pulse', 'back')).toBe(false);
  });

  it('deletes a node and its attached connections', () => {
    const store = useFlowsStore();

    expect(store.deleteNode('climate-control', 'comfort-pulse')).toBe(true);
    expect(store.findFlow('climate-control')?.nodes.some(({ id }) => id === 'comfort-pulse')).toBe(
      false
    );
    expect(store.findFlow('climate-control')?.connections).toEqual([]);
    expect(store.deleteNode('climate-control', 'missing')).toBe(false);
  });

  it('replaces state only after payload validation and serialises it for the API', () => {
    const store = useFlowsStore();
    const payload = structuredClone(sampleFlows[0]!);
    payload.name = 'Loaded climate flow';

    expect(store.replaceFlowFromPayload(payload).name).toBe('Loaded climate flow');
    expect(store.flowPayload('climate-control')).toEqual(payload);

    const invalid = structuredClone(payload);
    invalid.connections[0]!.end.nodeId = 'missing';
    expect(() => store.replaceFlowFromPayload(invalid)).toThrow(/unknown node/);
    expect(store.findFlow('climate-control')?.name).toBe('Loaded climate flow');
    expect(store.flowPayload('missing')).toBeUndefined();
  });

  it('adds guarded connections and deletes a known connection', () => {
    const store = useFlowsStore();
    const start = { nodeId: 'temperature-average', connectorId: 'output' };
    const end = { nodeId: 'manual-override', connectorId: 'input' };

    expect(store.connectNodes('climate-control', start, end, 'new-link')).toEqual({
      success: true
    });
    expect(store.findFlow('climate-control')?.connections.at(-1)?.id).toBe('new-link');
    expect(store.connectNodes('climate-control', start, end, 'duplicate').error).toMatch(
      /already exists/
    );
    expect(store.connectNodes('missing', start, end).success).toBe(false);
    expect(store.deleteConnection('climate-control', 'new-link')).toBe(true);
    expect(store.deleteConnection('climate-control', 'new-link')).toBe(false);
  });

  it('adds a plain node and rejects duplicate IDs', () => {
    const store = useFlowsStore();
    const node = createDefaultNode('split', { x: 48, y: 72 }, 4, 'new-split');

    expect(store.addNode('climate-control', node)).toBe(true);
    expect(store.findFlow('climate-control')?.nodes.at(-1)).toEqual(node);
    expect(store.addNode('climate-control', node)).toBe(false);
    expect(store.addNode('missing', node)).toBe(false);
  });

  it('updates validated node labels and known configuration fields', () => {
    const store = useFlowsStore();

    expect(
      store.updateNodeLabel('climate-control', 'temperature-average', '  Room average  ')
    ).toBe(true);
    expect(store.findFlow('climate-control')?.nodes[0]?.label).toBe('Room average');
    expect(store.updateNodeLabel('climate-control', 'temperature-average', '   ')).toBe(false);
    expect(
      store.updateNodeConfiguration('climate-control', 'temperature-average', 'operation', 'sum')
    ).toBe(true);
    expect(store.findFlow('climate-control')?.nodes[0]?.configuration.operation).toBe('sum');
    expect(
      store.updateNodeConfiguration('climate-control', 'temperature-average', 'missing', 1)
    ).toBe(false);
  });

  it('tracks graph dirty state, resets from its baseline, and clears after confirmation', () => {
    const store = useFlowsStore();
    expect(store.isFlowDirty('climate-control')).toBe(false);

    store.moveNode('climate-control', 'temperature-average', 240, 240);
    expect(store.isFlowDirty('climate-control')).toBe(true);
    expect(store.resetFlow('climate-control')).toBe(true);
    expect(store.findFlow('climate-control')?.nodes[0]).toMatchObject({ x: 90, y: 110 });
    expect(store.isFlowDirty('climate-control')).toBe(false);

    store.moveNode('climate-control', 'temperature-average', 240, 240);
    store.replaceFlowFromPayload(store.flowPayload('climate-control'));
    expect(store.isFlowDirty('climate-control')).toBe(false);
    expect(store.resetFlow('missing')).toBe(false);
  });
});
