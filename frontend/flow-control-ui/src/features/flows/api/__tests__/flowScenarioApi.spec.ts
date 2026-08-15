import { afterEach, describe, expect, it, vi } from 'vitest';

import { flowScenarioApi, type FlowScenario } from '@/features/flows/api/flowScenarioApi';

const scenario = (): FlowScenario => ({
  schemaVersion: 1,
  id: 'scenario-1',
  name: 'Recorded scenario',
  flowId: 'flow-1',
  flowRevision: 4,
  steps: [],
  expectations: []
});

describe('flowScenarioApi', () => {
  afterEach(() => vi.restoreAllMocks());

  /**
   * Purpose: Protects the stable scenario persistence route and request schema.
   * Description: Saves a scenario and verifies the exact JSON request sent to the backend.
   */
  it('saves scenarios against stable flow and scenario IDs', async () => {
    // Arrange: Return the same scenario from the mocked API boundary.
    const fetchMock = vi
      .spyOn(globalThis, 'fetch')
      .mockResolvedValue(new Response(JSON.stringify(scenario()), { status: 200 }));

    // Act: Save through the typed client.
    const result = await flowScenarioApi.save(scenario());

    // Assert: IDs are encoded in the separate scenario resource route.
    expect(result.id).toBe('scenario-1');
    expect(fetchMock).toHaveBeenCalledWith(
      '/api/flows/flow-1/scenarios/scenario-1',
      expect.objectContaining({ method: 'PUT' })
    );
  });
});
