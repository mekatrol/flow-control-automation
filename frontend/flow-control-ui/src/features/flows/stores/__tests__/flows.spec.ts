import { createPinia, setActivePinia } from 'pinia';
import { beforeEach, describe, expect, it } from 'vitest';

import { sampleFlows } from '@/features/flows/__tests__/fixtures/sampleFlows';
import { createDefaultNode } from '@/features/flows/graph/createNode';
import { useFlowsStore } from '@/features/flows/stores/flows';

describe('flows store', () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    useFlowsStore().replaceAllFlowsFromPayloads(structuredClone(sampleFlows));
  });

  /**
   * Purpose: Protects the behavioral contract that starts empty and atomically replaces and removes confirmed API state.
   * Description: Exercises starts empty and atomically replaces and removes confirmed API state from its arranged starting state and
   * verifies the observable results required by the scenario.
   */
  it('starts empty and atomically replaces and removes confirmed API state', () => {
    setActivePinia(createPinia());
    const store = useFlowsStore();

    // Expected outcome: `store.flows` matches the required structure.
    // Acceptance criteria: `store.flows` must equal `[]`, because this condition proves that
    // starts empty and atomically replaces and removes confirmed API state.
    expect(store.flows).toEqual([]);

    // Expected outcome: `store.replaceAllFlowsFromPayloads(structuredClone(sampleFlows))` contains the required number of entries.
    // Acceptance criteria: `store.replaceAllFlowsFromPayloads(structuredClone(sampleFlows))` must contain exactly 2 entries, because this condition proves that
    // starts empty and atomically replaces and removes confirmed API state.
    expect(store.replaceAllFlowsFromPayloads(structuredClone(sampleFlows))).toHaveLength(2);

    // Expected outcome: `store.flows.map(({ id }) => id)` matches the required structure.
    // Acceptance criteria: `store.flows.map(({ id }) => id)` must equal `['climate-control', 'garden-irrigation']`, because this condition proves that
    // starts empty and atomically replaces and removes confirmed API state.
    expect(store.flows.map(({ id }) => id)).toEqual(['climate-control', 'garden-irrigation']);

    const invalid = structuredClone(sampleFlows);
    invalid[0]!.connections[0]!.end.nodeId = 'missing';

    // Expected outcome: The invalid operation is rejected.
    // Acceptance criteria: the operation must throw the asserted error, because this condition proves that
    // starts empty and atomically replaces and removes confirmed API state.
    expect(() => store.replaceAllFlowsFromPayloads(invalid)).toThrow(/unknown node/);

    // Expected outcome: `store.flows.map(({ id }) => id)` matches the required structure.
    // Acceptance criteria: `store.flows.map(({ id }) => id)` must equal `['climate-control', 'garden-irrigation']`, because this condition proves that
    // starts empty and atomically replaces and removes confirmed API state.
    expect(store.flows.map(({ id }) => id)).toEqual(['climate-control', 'garden-irrigation']);

    store.selectFlow('garden-irrigation');

    // Expected outcome: `store.removeConfirmedFlow('garden-irrigation')` has the required value.
    // Acceptance criteria: `store.removeConfirmedFlow('garden-irrigation')` must be `true`, because this condition proves that
    // starts empty and atomically replaces and removes confirmed API state.
    expect(store.removeConfirmedFlow('garden-irrigation')).toBe(true);

    // Expected outcome: `store.findFlow('garden-irrigation')` is not supplied.
    // Acceptance criteria: `store.findFlow('garden-irrigation')` must be undefined, because this condition proves that
    // starts empty and atomically replaces and removes confirmed API state.
    expect(store.findFlow('garden-irrigation')).toBeUndefined();

    // Expected outcome: `store.activeFlowId` is not supplied.
    // Acceptance criteria: `store.activeFlowId` must be undefined, because this condition proves that
    // starts empty and atomically replaces and removes confirmed API state.
    expect(store.activeFlowId).toBeUndefined();

    // Expected outcome: `store.removeConfirmedFlow('garden-irrigation')` has the required value.
    // Acceptance criteria: `store.removeConfirmedFlow('garden-irrigation')` must be `false`, because this condition proves that
    // starts empty and atomically replaces and removes confirmed API state.
    expect(store.removeConfirmedFlow('garden-irrigation')).toBe(false);
  });

  /**
   * Purpose: Protects the behavioral contract that selects a known flow.
   * Description: Exercises selects a known flow from its arranged starting state and
   * verifies the observable results required by the scenario.
   */
  it('selects a known flow', () => {
    const store = useFlowsStore();

    store.selectFlow('climate-control');

    // Expected outcome: `store.activeFlow?.name` has the required value.
    // Acceptance criteria: `store.activeFlow?.name` must be `'Climate control'`, because this condition proves that
    // selects a known flow.
    expect(store.activeFlow?.name).toBe('Climate control');
  });

  /**
   * Purpose: Protects the behavioral contract that clears the selection for an unknown flow.
   * Description: Exercises clears the selection for an unknown flow from its arranged starting state and
   * verifies the observable results required by the scenario.
   */
  it('clears the selection for an unknown flow', () => {
    const store = useFlowsStore();
    store.selectFlow('climate-control');

    store.selectFlow('missing');

    // Expected outcome: `store.activeFlowId` is not supplied.
    // Acceptance criteria: `store.activeFlowId` must be undefined, because this condition proves that
    // clears the selection for an unknown flow.
    expect(store.activeFlowId).toBeUndefined();

    // Expected outcome: `store.activeFlow` is not supplied.
    // Acceptance criteria: `store.activeFlow` must be undefined, because this condition proves that
    // clears the selection for an unknown flow.
    expect(store.activeFlow).toBeUndefined();
  });

  /**
   * Purpose: Protects the behavioral contract that moves a known node without storing pointer state.
   * Description: Exercises moves a known node without storing pointer state from its arranged starting state and
   * verifies the observable results required by the scenario.
   */
  it('moves a known node without storing pointer state', () => {
    const store = useFlowsStore();

    // Expected outcome: `store.moveNode('climate-control', 'temperature-average', 144, 192)` has the required value.
    // Acceptance criteria: `store.moveNode('climate-control', 'temperature-average', 144, 192)` must be `true`, because this condition proves that
    // moves a known node without storing pointer state.
    expect(store.moveNode('climate-control', 'temperature-average', 144, 192)).toBe(true);

    // Expected outcome: `store.findFlow('climate-control')?.nodes[0]` contains the required object fields.
    // Acceptance criteria: `store.findFlow('climate-control')?.nodes[0]` must match the object `{ x: 144, y: 192 }`, because this condition proves that
    // moves a known node without storing pointer state.
    expect(store.findFlow('climate-control')?.nodes[0]).toMatchObject({ x: 144, y: 192 });

    // Expected outcome: `store.moveNode('missing', 'temperature-average', 0, 0)` has the required value.
    // Acceptance criteria: `store.moveNode('missing', 'temperature-average', 0, 0)` must be `false`, because this condition proves that
    // moves a known node without storing pointer state.
    expect(store.moveNode('missing', 'temperature-average', 0, 0)).toBe(false);

    // Expected outcome: `store.moveNode('climate-control', 'missing', 0, 0)` has the required value.
    // Acceptance criteria: `store.moveNode('climate-control', 'missing', 0, 0)` must be `false`, because this condition proves that
    // moves a known node without storing pointer state.
    expect(store.moveNode('climate-control', 'missing', 0, 0)).toBe(false);

    // Expected outcome: `JSON.stringify(store.flows)` includes the required value.
    // Acceptance criteria: `JSON.stringify(store.flows)` must contain `'pointer'`, because this condition proves that
    // moves a known node without storing pointer state.
    expect(JSON.stringify(store.flows)).not.toContain('pointer');
  });

  /**
   * Purpose: Protects the behavioral contract that applies z-order commands and reports boundary no-ops.
   * Description: Exercises applies z-order commands and reports boundary no-ops from its arranged starting state and
   * verifies the observable results required by the scenario.
   */
  it('applies z-order commands and reports boundary no-ops', () => {
    const store = useFlowsStore();

    // Expected outcome: `store.reorderNode('climate-control', 'comfort-pulse', 'front')` has the required value.
    // Acceptance criteria: `store.reorderNode('climate-control', 'comfort-pulse', 'front')` must be `true`, because this condition proves that
    // applies z-order commands and reports boundary no-ops.
    expect(store.reorderNode('climate-control', 'comfort-pulse', 'front')).toBe(true);

    // Expected outcome: `store.findFlow('climate-control')?.nodes.at(-1)?.id` has the required value.
    // Acceptance criteria: `store.findFlow('climate-control')?.nodes.at(-1)?.id` must be `'comfort-pulse'`, because this condition proves that
    // applies z-order commands and reports boundary no-ops.
    expect(store.findFlow('climate-control')?.nodes.at(-1)?.id).toBe('comfort-pulse');

    // Expected outcome: `store.reorderNode('climate-control', 'comfort-pulse', 'front')` has the required value.
    // Acceptance criteria: `store.reorderNode('climate-control', 'comfort-pulse', 'front')` must be `false`, because this condition proves that
    // applies z-order commands and reports boundary no-ops.
    expect(store.reorderNode('climate-control', 'comfort-pulse', 'front')).toBe(false);

    // Expected outcome: `store.reorderNode('missing', 'comfort-pulse', 'back')` has the required value.
    // Acceptance criteria: `store.reorderNode('missing', 'comfort-pulse', 'back')` must be `false`, because this condition proves that
    // applies z-order commands and reports boundary no-ops.
    expect(store.reorderNode('missing', 'comfort-pulse', 'back')).toBe(false);
  });

  /**
   * Purpose: Protects the behavioral contract that deletes a node and its attached connections.
   * Description: Exercises deletes a node and its attached connections from its arranged starting state and
   * verifies the observable results required by the scenario.
   */
  it('deletes a node and its attached connections', () => {
    const store = useFlowsStore();

    // Expected outcome: `store.deleteNode('climate-control', 'comfort-pulse')` has the required value.
    // Acceptance criteria: `store.deleteNode('climate-control', 'comfort-pulse')` must be `true`, because this condition proves that
    // deletes a node and its attached connections.
    expect(store.deleteNode('climate-control', 'comfort-pulse')).toBe(true);

    // Expected outcome: `store.findFlow('climate-control')?.nodes.some(({ id }) => id === 'comfort-pulse')` has the required value.
    // Acceptance criteria: `store.findFlow('climate-control')?.nodes.some(({ id }) => id === 'comfort-pulse')` must be `false`, because this condition proves that
    // deletes a node and its attached connections.
    expect(store.findFlow('climate-control')?.nodes.some(({ id }) => id === 'comfort-pulse')).toBe(
      false
    );

    // Expected outcome: `store.findFlow('climate-control')?.connections` matches the required structure.
    // Acceptance criteria: `store.findFlow('climate-control')?.connections` must equal `[]`, because this condition proves that
    // deletes a node and its attached connections.
    expect(store.findFlow('climate-control')?.connections).toEqual([]);

    // Expected outcome: `store.deleteNode('climate-control', 'missing')` has the required value.
    // Acceptance criteria: `store.deleteNode('climate-control', 'missing')` must be `false`, because this condition proves that
    // deletes a node and its attached connections.
    expect(store.deleteNode('climate-control', 'missing')).toBe(false);
  });

  /**
   * Purpose: Protects the behavioral contract that replaces state only after payload validation and serialises it for the API.
   * Description: Exercises replaces state only after payload validation and serialises it for the API from its arranged starting state and
   * verifies the observable results required by the scenario.
   */
  it('replaces state only after payload validation and serialises it for the API', () => {
    const store = useFlowsStore();
    const payload = structuredClone(sampleFlows[0]!);
    payload.name = 'Loaded climate flow';

    // Expected outcome: `store.replaceFlowFromPayload(payload` has the required value.
    // Acceptance criteria: `store.replaceFlowFromPayload(payload` must be `'Loaded climate flow'`, because this condition proves that
    // replaces state only after payload validation and serialises it for the API.
    expect(store.replaceFlowFromPayload(payload).name).toBe('Loaded climate flow');

    // Expected outcome: `store.flowPayload('climate-control')` matches the required structure.
    // Acceptance criteria: `store.flowPayload('climate-control')` must equal `payload`, because this condition proves that
    // replaces state only after payload validation and serialises it for the API.
    expect(store.flowPayload('climate-control')).toEqual(payload);

    const invalid = structuredClone(payload);
    invalid.connections[0]!.end.nodeId = 'missing';

    // Expected outcome: The invalid operation is rejected.
    // Acceptance criteria: the operation must throw the asserted error, because this condition proves that
    // replaces state only after payload validation and serialises it for the API.
    expect(() => store.replaceFlowFromPayload(invalid)).toThrow(/unknown node/);

    // Expected outcome: `store.findFlow('climate-control')?.name` has the required value.
    // Acceptance criteria: `store.findFlow('climate-control')?.name` must be `'Loaded climate flow'`, because this condition proves that
    // replaces state only after payload validation and serialises it for the API.
    expect(store.findFlow('climate-control')?.name).toBe('Loaded climate flow');

    // Expected outcome: `store.flowPayload('missing')` is not supplied.
    // Acceptance criteria: `store.flowPayload('missing')` must be undefined, because this condition proves that
    // replaces state only after payload validation and serialises it for the API.
    expect(store.flowPayload('missing')).toBeUndefined();
  });

  /**
   * Purpose: Protects the behavioral contract that adds guarded connections and deletes a known connection.
   * Description: Exercises adds guarded connections and deletes a known connection from its arranged starting state and
   * verifies the observable results required by the scenario.
   */
  it('adds guarded connections and deletes a known connection', () => {
    const store = useFlowsStore();
    const start = { nodeId: 'temperature-average', connectorId: 'output' };
    const end = { nodeId: 'manual-override', connectorId: 'input' };

    // Expected outcome: `store.connectNodes('climate-control', start, end, 'new-link')` matches the required structure.
    // Acceptance criteria: `store.connectNodes('climate-control', start, end, 'new-link')` must equal `{ success: true }`, because this condition proves that
    // adds guarded connections and deletes a known connection.
    expect(store.connectNodes('climate-control', start, end, 'new-link')).toEqual({
      success: true
    });

    // Expected outcome: `store.findFlow('climate-control')?.connections.at(-1)?.id` has the required value.
    // Acceptance criteria: `store.findFlow('climate-control')?.connections.at(-1)?.id` must be `'new-link'`, because this condition proves that
    // adds guarded connections and deletes a known connection.
    expect(store.findFlow('climate-control')?.connections.at(-1)?.id).toBe('new-link');

    // Expected outcome: `store.connectNodes('climate-control', start, end, 'duplicate'` follows the required pattern.
    // Acceptance criteria: `store.connectNodes('climate-control', start, end, 'duplicate'` must match `/already exists/`, because this condition proves that
    // adds guarded connections and deletes a known connection.
    expect(store.connectNodes('climate-control', start, end, 'duplicate').error).toMatch(
      /already exists/
    );

    // Expected outcome: `store.connectNodes('missing', start, end` has the required value.
    // Acceptance criteria: `store.connectNodes('missing', start, end` must be `false`, because this condition proves that
    // adds guarded connections and deletes a known connection.
    expect(store.connectNodes('missing', start, end).success).toBe(false);

    // Expected outcome: `store.deleteConnection('climate-control', 'new-link')` has the required value.
    // Acceptance criteria: `store.deleteConnection('climate-control', 'new-link')` must be `true`, because this condition proves that
    // adds guarded connections and deletes a known connection.
    expect(store.deleteConnection('climate-control', 'new-link')).toBe(true);

    // Expected outcome: `store.deleteConnection('climate-control', 'new-link')` has the required value.
    // Acceptance criteria: `store.deleteConnection('climate-control', 'new-link')` must be `false`, because this condition proves that
    // adds guarded connections and deletes a known connection.
    expect(store.deleteConnection('climate-control', 'new-link')).toBe(false);
  });

  /**
   * Purpose: Protects the behavioral contract that adds a plain node and rejects duplicate IDs.
   * Description: Exercises adds a plain node and rejects duplicate IDs from its arranged starting state and
   * verifies the observable results required by the scenario.
   */
  it('adds a plain node and rejects duplicate IDs', () => {
    const store = useFlowsStore();
    const node = createDefaultNode('split', { x: 48, y: 72 }, 4, 'new-split');

    // Expected outcome: `store.addNode('climate-control', node)` has the required value.
    // Acceptance criteria: `store.addNode('climate-control', node)` must be `true`, because this condition proves that
    // adds a plain node and rejects duplicate IDs.
    expect(store.addNode('climate-control', node)).toBe(true);

    // Expected outcome: `store.findFlow('climate-control')?.nodes.at(-1)` matches the required structure.
    // Acceptance criteria: `store.findFlow('climate-control')?.nodes.at(-1)` must equal `node`, because this condition proves that
    // adds a plain node and rejects duplicate IDs.
    expect(store.findFlow('climate-control')?.nodes.at(-1)).toEqual(node);

    // Expected outcome: `store.addNode('climate-control', node)` has the required value.
    // Acceptance criteria: `store.addNode('climate-control', node)` must be `false`, because this condition proves that
    // adds a plain node and rejects duplicate IDs.
    expect(store.addNode('climate-control', node)).toBe(false);

    // Expected outcome: `store.addNode('missing', node)` has the required value.
    // Acceptance criteria: `store.addNode('missing', node)` must be `false`, because this condition proves that
    // adds a plain node and rejects duplicate IDs.
    expect(store.addNode('missing', node)).toBe(false);
  });

  it('adds a valid interface entry with a new flow input node', () => {
    const store = useFlowsStore();
    const flow = store.findFlow('climate-control')!;
    flow.interface.inputs = [];
    const node = createDefaultNode('flowInput', { x: 48, y: 72 }, 4, 'new-input');

    expect(store.addNode(flow.id, node)).toBe(true);
    expect(flow.interface.inputs).toEqual([
      {
        id: 'input-1',
        name: 'New input',
        dataType: 'boolean',
        defaultValue: false,
        required: false
      }
    ]);
    expect(flow.nodes.at(-1)).toMatchObject({
      id: 'new-input',
      label: 'New input',
      configuration: { interfaceId: 'input-1' },
      connectors: [{ id: 'value', dataType: 'boolean', direction: 'output' }]
    });
    expect(() => store.flowPayload(flow.id)).not.toThrow();
  });

  it('creates a distinct interface entry for each new flow input node', () => {
    const store = useFlowsStore();
    const flow = store.findFlow('climate-control')!;
    flow.interface.inputs = [
      { id: 'temperature', name: 'Temperature', dataType: 'number', units: '°C', required: true }
    ];
    const node = createDefaultNode('flowInput', { x: 48, y: 72 }, 4, 'new-input');

    expect(store.addNode(flow.id, node)).toBe(true);
    expect(flow.interface.inputs).toHaveLength(2);
    expect(flow.nodes.at(-1)).toMatchObject({
      label: 'New input',
      configuration: { interfaceId: 'input-1' },
      connectors: [{ label: 'New input', dataType: 'boolean' }]
    });
  });

  /**
   * Purpose: Protects the behavioral contract that updates validated node labels and known configuration fields.
   * Description: Exercises updates validated node labels and known configuration fields from its arranged starting state and
   * verifies the observable results required by the scenario.
   */
  it('updates validated node labels and known configuration fields', () => {
    const store = useFlowsStore();

    // Expected outcome: `store.updateNodeLabel('climate-control', 'temperature-average', ' Room average ')` has the required value.
    // Acceptance criteria: `store.updateNodeLabel('climate-control', 'temperature-average', ' Room average ')` must be `true`, because this condition proves that
    // updates validated node labels and known configuration fields.
    expect(
      store.updateNodeLabel('climate-control', 'temperature-average', '  Room average  ')
    ).toBe(true);

    // Expected outcome: `store.findFlow('climate-control')?.nodes[0]?.label` has the required value.
    // Acceptance criteria: `store.findFlow('climate-control')?.nodes[0]?.label` must be `'Room average'`, because this condition proves that
    // updates validated node labels and known configuration fields.
    expect(store.findFlow('climate-control')?.nodes[0]?.label).toBe('Room average');

    // Expected outcome: `store.updateNodeLabel('climate-control', 'temperature-average', ' ')` has the required value.
    // Acceptance criteria: `store.updateNodeLabel('climate-control', 'temperature-average', ' ')` must be `false`, because this condition proves that
    // updates validated node labels and known configuration fields.
    expect(store.updateNodeLabel('climate-control', 'temperature-average', '   ')).toBe(false);

    // Expected outcome: `store.updateNodeConfiguration('climate-control', 'temperature-average', 'operation', 'sum')` has the required value.
    // Acceptance criteria: `store.updateNodeConfiguration('climate-control', 'temperature-average', 'operation', 'sum')` must be `true`, because this condition proves that
    // updates validated node labels and known configuration fields.
    expect(
      store.updateNodeConfiguration('climate-control', 'temperature-average', 'operation', 'sum')
    ).toBe(true);

    // Expected outcome: `store.findFlow('climate-control')?.nodes[0]?.configuration.operation` has the required value.
    // Acceptance criteria: `store.findFlow('climate-control')?.nodes[0]?.configuration.operation` must be `'sum'`, because this condition proves that
    // updates validated node labels and known configuration fields.
    expect(store.findFlow('climate-control')?.nodes[0]?.configuration.operation).toBe('sum');

    // Expected outcome: `store.updateNodeConfiguration('climate-control', 'temperature-average', 'missing', 1)` has the required value.
    // Acceptance criteria: `store.updateNodeConfiguration('climate-control', 'temperature-average', 'missing', 1)` must be `false`, because this condition proves that
    // updates validated node labels and known configuration fields.
    expect(
      store.updateNodeConfiguration('climate-control', 'temperature-average', 'missing', 1)
    ).toBe(false);
  });

  /**
   * Purpose: Protects the behavioral contract that tracks graph dirty state, resets from its baseline, and clears after confirmation.
   * Description: Exercises tracks graph dirty state, resets from its baseline, and clears after confirmation from its arranged starting state and
   * verifies the observable results required by the scenario.
   */
  it('tracks graph dirty state, resets from its baseline, and clears after confirmation', () => {
    const store = useFlowsStore();

    // Expected outcome: `store.isFlowDirty('climate-control')` has the required value.
    // Acceptance criteria: `store.isFlowDirty('climate-control')` must be `false`, because this condition proves that
    // tracks graph dirty state, resets from its baseline, and clears after confirmation.
    expect(store.isFlowDirty('climate-control')).toBe(false);

    store.moveNode('climate-control', 'temperature-average', 240, 240);

    // Expected outcome: `store.isFlowDirty('climate-control')` has the required value.
    // Acceptance criteria: `store.isFlowDirty('climate-control')` must be `true`, because this condition proves that
    // tracks graph dirty state, resets from its baseline, and clears after confirmation.
    expect(store.isFlowDirty('climate-control')).toBe(true);

    // Expected outcome: `store.resetFlow('climate-control')` has the required value.
    // Acceptance criteria: `store.resetFlow('climate-control')` must be `true`, because this condition proves that
    // tracks graph dirty state, resets from its baseline, and clears after confirmation.
    expect(store.resetFlow('climate-control')).toBe(true);

    // Expected outcome: `store.findFlow('climate-control')?.nodes[0]` contains the required object fields.
    // Acceptance criteria: `store.findFlow('climate-control')?.nodes[0]` must match the object `{ x: 90, y: 110 }`, because this condition proves that
    // tracks graph dirty state, resets from its baseline, and clears after confirmation.
    expect(store.findFlow('climate-control')?.nodes[0]).toMatchObject({ x: 90, y: 110 });

    // Expected outcome: `store.isFlowDirty('climate-control')` has the required value.
    // Acceptance criteria: `store.isFlowDirty('climate-control')` must be `false`, because this condition proves that
    // tracks graph dirty state, resets from its baseline, and clears after confirmation.
    expect(store.isFlowDirty('climate-control')).toBe(false);

    store.moveNode('climate-control', 'temperature-average', 240, 240);
    store.replaceFlowFromPayload(store.flowPayload('climate-control'));

    // Expected outcome: `store.isFlowDirty('climate-control')` has the required value.
    // Acceptance criteria: `store.isFlowDirty('climate-control')` must be `false`, because this condition proves that
    // tracks graph dirty state, resets from its baseline, and clears after confirmation.
    expect(store.isFlowDirty('climate-control')).toBe(false);

    // Expected outcome: `store.resetFlow('missing')` has the required value.
    // Acceptance criteria: `store.resetFlow('missing')` must be `false`, because this condition proves that
    // tracks graph dirty state, resets from its baseline, and clears after confirmation.
    expect(store.resetFlow('missing')).toBe(false);
  });
});
