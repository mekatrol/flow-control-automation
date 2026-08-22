import { afterEach, describe, expect, it, vi } from 'vitest';

import { sampleFlows } from '@/features/flows/__tests__/fixtures/sampleFlows';
import { FlowApiError, flowApi } from '@/features/flows/api/flowApi';

const response = (body: unknown, status = 200): Response =>
  new Response(JSON.stringify(body), { status, headers: { 'content-type': 'application/json' } });
const flowPage = {
  items: sampleFlows,
  totalItems: sampleFlows.length,
  page: 1,
  pageSize: 10,
  pageCount: 1
};

describe('flow API client', () => {
  afterEach(() => vi.unstubAllGlobals());

  /**
   * Purpose: Protects the behavioral contract that validates a successful response and sends a serialised save.
   * Description: Exercises validates a successful response and sends a serialised save from its arranged starting state and
   * verifies the observable results required by the scenario.
   */
  it('validates a successful response and sends a serialised save', async () => {
    const fetchMock = vi
      .fn<typeof fetch>()
      .mockResolvedValueOnce(response(sampleFlows[0]))
      .mockResolvedValueOnce(response(sampleFlows[0]));
    vi.stubGlobal('fetch', fetchMock);

    // Expected outcome: `flowApi.getFlow('climate control')` matches the required structure.
    // Acceptance criteria: `flowApi.getFlow('climate control')` must equal `sampleFlows[0]`, because this condition proves that
    // validates a successful response and sends a serialised save.
    await expect(flowApi.getFlow('climate control')).resolves.toEqual(sampleFlows[0]);

    // Expected outcome: `flowApi.saveFlow(sampleFlows[0]!)` matches the required structure.
    // Acceptance criteria: `flowApi.saveFlow(sampleFlows[0]!)` must equal `sampleFlows[0]`, because this condition proves that
    // validates a successful response and sends a serialised save.
    await expect(flowApi.saveFlow(sampleFlows[0]!)).resolves.toEqual(sampleFlows[0]);

    // Expected outcome: `fetchMock` receives the required call in sequence.
    // Acceptance criteria: `fetchMock` must have the numbered call and arguments `1, '/api/flows/climate%20control', { method: 'GET', signal: undefined }`, because this condition proves that
    // validates a successful response and sends a serialised save.
    expect(fetchMock).toHaveBeenNthCalledWith(1, '/api/flows/climate%20control', {
      method: 'GET',
      signal: undefined
    });

    // Expected outcome: `fetchMock.mock.calls[1]?.[1]` contains the required object fields.
    // Acceptance criteria: `fetchMock.mock.calls[1]?.[1]` must match the object `{ method: 'PUT', body: JSON.stringify(sampleFlows[0]`, because this condition proves that
    // validates a successful response and sends a serialised save.
    expect(fetchMock.mock.calls[1]?.[1]).toMatchObject({
      method: 'PUT',
      body: JSON.stringify(sampleFlows[0])
    });
  });

  /**
   * Purpose: Protects the deployed-version read and draft-revert routes.
   * Description: Reads and restores the last deployed graph through typed API methods.
   */
  it('reads the deployed version and reverts the draft', async () => {
    const fetchMock = vi
      .fn<typeof fetch>()
      .mockResolvedValueOnce(response(sampleFlows[0]))
      .mockResolvedValueOnce(response(sampleFlows[0]));
    vi.stubGlobal('fetch', fetchMock);

    // Expected outcome: Both endpoint responses pass normal flow validation.
    // Acceptance criteria: Both methods return the validated fixture and use the version-specific routes.
    await expect(flowApi.getDeployedFlow('climate control')).resolves.toEqual(sampleFlows[0]);
    await expect(flowApi.revertToDeployed('climate control')).resolves.toEqual(sampleFlows[0]);
    expect(fetchMock).toHaveBeenNthCalledWith(1, '/api/flows/climate%20control/deployed', {
      method: 'GET',
      signal: undefined
    });
    expect(fetchMock).toHaveBeenNthCalledWith(
      2,
      '/api/flows/climate%20control/revert-to-deployed',
      { method: 'POST', signal: undefined }
    );
  });

  /**
   * Purpose: Protects the behavioral contract that lists, creates, disables, enables, and deletes flows through typed endpoints.
   * Description: Exercises lists, creates, disables, enables, and deletes flows through typed endpoints from its arranged starting state and
   * verifies the observable results required by the scenario.
   */
  it('lists, creates, disables, enables, and deletes flows through typed endpoints', async () => {
    const fetchMock = vi
      .fn<typeof fetch>()
      .mockResolvedValueOnce(response(flowPage))
      .mockResolvedValueOnce(response(sampleFlows[0]))
      .mockResolvedValueOnce(response({ ...sampleFlows[1], disabled: true }))
      .mockResolvedValueOnce(response(sampleFlows[1]))
      .mockResolvedValueOnce(new Response(null, { status: 204 }));
    vi.stubGlobal('fetch', fetchMock);

    // Expected outcome: `flowApi.listFlows({ filter: '', statuses: [], page: 1, pageSize: 10, sort: 'ascending' })` matches the required structure.
    // Acceptance criteria: `flowApi.listFlows({ filter: '', statuses: [], page: 1, pageSize: 10, sort: 'ascending' })` must equal `flowPage`, because this condition proves that
    // lists, creates, disables, enables, and deletes flows through typed endpoints.
    await expect(
      flowApi.listFlows({ filter: '', statuses: [], page: 1, pageSize: 10, sort: 'ascending' })
    ).resolves.toEqual(flowPage);

    // Expected outcome: `flowApi.createFlow('Climate control')` matches the required structure.
    // Acceptance criteria: `flowApi.createFlow('Climate control')` must equal `sampleFlows[0]`, because this condition proves that
    // lists, creates, disables, enables, and deletes flows through typed endpoints.
    await expect(flowApi.createFlow('Climate control')).resolves.toEqual(sampleFlows[0]);

    // Expected outcome: `flowApi.setFlowDisabled('garden irrigation', true)` contains the required object fields.
    // Acceptance criteria: `flowApi.setFlowDisabled('garden irrigation', true)` must match the object `{ disabled: true }`, because this condition proves that
    // lists, creates, disables, enables, and deletes flows through typed endpoints.
    await expect(flowApi.setFlowDisabled('garden irrigation', true)).resolves.toMatchObject({
      disabled: true
    });

    // Expected outcome: `flowApi.setFlowDisabled('garden irrigation', false)` contains the required object fields.
    // Acceptance criteria: `flowApi.setFlowDisabled('garden irrigation', false)` must match the object `{ disabled: false }`, because this condition proves that
    // lists, creates, disables, enables, and deletes flows through typed endpoints.
    await expect(flowApi.setFlowDisabled('garden irrigation', false)).resolves.toMatchObject({
      disabled: false
    });

    // Expected outcome: `flowApi.deleteFlow('climate control')` is not supplied.
    // Acceptance criteria: `flowApi.deleteFlow('climate control')` must be undefined, because this condition proves that
    // lists, creates, disables, enables, and deletes flows through typed endpoints.
    await expect(flowApi.deleteFlow('climate control')).resolves.toBeUndefined();

    // Expected outcome: `fetchMock` receives the required call in sequence.
    // Acceptance criteria: `fetchMock` must have the numbered call and arguments `1, '/api/flows?filter=&page=1&pageSize=10&sort=ascending', { method: 'GET', signal: undefined }`, because this condition proves that
    // lists, creates, disables, enables, and deletes flows through typed endpoints.
    expect(fetchMock).toHaveBeenNthCalledWith(
      1,
      '/api/flows?filter=&page=1&pageSize=10&sort=ascending',
      {
        method: 'GET',
        signal: undefined
      }
    );

    // Expected outcome: `fetchMock` receives the required call in sequence.
    // Acceptance criteria: `fetchMock` must have the numbered call and arguments `2, '/api/flows', { method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify({ name: 'Climat`, because this condition proves that
    // lists, creates, disables, enables, and deletes flows through typed endpoints.
    expect(fetchMock).toHaveBeenNthCalledWith(2, '/api/flows', {
      method: 'POST',
      headers: { 'content-type': 'application/json' },
      body: JSON.stringify({ name: 'Climate control' }),
      signal: undefined
    });

    // Expected outcome: `fetchMock` receives the required call in sequence.
    // Acceptance criteria: `fetchMock` must have the numbered call and arguments `3, '/api/flows/garden%20irrigation/disable', { method: 'POST', signal: undefined }`, because this condition proves that
    // lists, creates, disables, enables, and deletes flows through typed endpoints.
    expect(fetchMock).toHaveBeenNthCalledWith(3, '/api/flows/garden%20irrigation/disable', {
      method: 'POST',
      signal: undefined
    });

    // Expected outcome: `fetchMock` receives the required call in sequence.
    // Acceptance criteria: `fetchMock` must have the numbered call and arguments `4, '/api/flows/garden%20irrigation/enable', { method: 'POST', signal: undefined }`, because this condition proves that
    // lists, creates, disables, enables, and deletes flows through typed endpoints.
    expect(fetchMock).toHaveBeenNthCalledWith(4, '/api/flows/garden%20irrigation/enable', {
      method: 'POST',
      signal: undefined
    });

    // Expected outcome: `fetchMock` receives the required call in sequence.
    // Acceptance criteria: `fetchMock` must have the numbered call and arguments `5, '/api/flows/climate%20control', { method: 'DELETE', signal: undefined }`, because this condition proves that
    // lists, creates, disables, enables, and deletes flows through typed endpoints.
    expect(fetchMock).toHaveBeenNthCalledWith(5, '/api/flows/climate%20control', {
      method: 'DELETE',
      signal: undefined
    });
  });

  /**
   * Purpose: Protects the behavioral contract that reports validation and HTTP failures consistently.
   * Description: Exercises reports validation and HTTP failures consistently from its arranged starting state and
   * verifies the observable results required by the scenario.
   */
  it('reports validation and HTTP failures consistently', async () => {
    vi.stubGlobal('fetch', vi.fn<typeof fetch>().mockResolvedValue(response({ nope: true })));

    // Expected outcome: `flowApi.getFlow('bad')` contains the required object fields.
    // Acceptance criteria: `flowApi.getFlow('bad')` must match the object `{ kind: 'validation' }`, because this condition proves that
    // reports validation and HTTP failures consistently.
    await expect(flowApi.getFlow('bad')).rejects.toMatchObject({ kind: 'validation' });

    vi.stubGlobal(
      'fetch',
      vi.fn<typeof fetch>().mockResolvedValue(response({ message: 'runtime unavailable' }, 503))
    );

    // Expected outcome: `flowApi.getFlow('offline')` contains the required object fields.
    // Acceptance criteria: `flowApi.getFlow('offline')` must match the object `{ kind: 'http', status: 503, message: 'Flow request failed: runtime unavailable' }`, because this condition proves that
    // reports validation and HTTP failures consistently.
    await expect(flowApi.getFlow('offline')).rejects.toMatchObject({
      kind: 'http',
      status: 503,
      message: 'Flow request failed: runtime unavailable'
    });

    vi.stubGlobal('fetch', vi.fn<typeof fetch>().mockResolvedValue(response({ nope: true })));

    // Expected outcome: `flowApi.listFlows({ filter: '', statuses: [], page: 1, pageSize: 10, sort: 'ascending' })` contains the required object fields.
    // Acceptance criteria: `flowApi.listFlows({ filter: '', statuses: [], page: 1, pageSize: 10, sort: 'ascending' })` must match the object `{ kind: 'validation' }`, because this condition proves that
    // reports validation and HTTP failures consistently.
    await expect(
      flowApi.listFlows({ filter: '', statuses: [], page: 1, pageSize: 10, sort: 'ascending' })
    ).rejects.toMatchObject({ kind: 'validation' });
  });

  /**
   * Purpose: Protects the behavioral contract that reports network failure and cancellation separately.
   * Description: Exercises reports network failure and cancellation separately from its arranged starting state and
   * verifies the observable results required by the scenario.
   */
  it('reports network failure and cancellation separately', async () => {
    vi.stubGlobal('fetch', vi.fn<typeof fetch>().mockRejectedValue(new TypeError('network down')));

    // Expected outcome: `flowApi.getFlow('offline')` contains the required object fields.
    // Acceptance criteria: `flowApi.getFlow('offline')` must match the object `{ kind: 'network' }`, because this condition proves that
    // reports network failure and cancellation separately.
    await expect(flowApi.getFlow('offline')).rejects.toMatchObject({ kind: 'network' });

    vi.stubGlobal(
      'fetch',
      vi.fn<typeof fetch>().mockRejectedValue(new DOMException('request aborted', 'AbortError'))
    );

    // Expected outcome: `flowApi.getFlow('cancelled')` matches the required structure.
    // Acceptance criteria: `flowApi.getFlow('cancelled')` must equal `new FlowApiError('cancelled', 'The flow request was cancelled.'`, because this condition proves that
    // reports network failure and cancellation separately.
    await expect(flowApi.getFlow('cancelled')).rejects.toEqual(
      new FlowApiError('cancelled', 'The flow request was cancelled.')
    );
  });
});
