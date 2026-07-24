import { FlowDtoValidationError, parseFlowDto, type FlowDto } from './flowDto';

export type FlowApiErrorKind = 'cancelled' | 'http' | 'network' | 'validation';

export class FlowApiError extends Error {
  constructor(
    public readonly kind: FlowApiErrorKind,
    message: string,
    public readonly status?: number
  ) {
    super(message);
    this.name = 'FlowApiError';
  }
}

const httpError = async (response: Response): Promise<FlowApiError> => {
  let serverMessage: string | undefined;
  try {
    const payload: unknown = await response.json();
    if (
      typeof payload === 'object' &&
      payload !== null &&
      'message' in payload &&
      typeof payload.message === 'string'
    ) {
      serverMessage = payload.message;
    }
  } catch {
    // Some proxies return an HTML or empty error response. The status remains a
    // useful fallback when no structured backend message is available.
  }
  return new FlowApiError(
    'http',
    serverMessage
      ? `Flow request failed: ${serverMessage}`
      : `Flow request failed with status ${response.status}.`,
    response.status
  );
};

// A successful HTTP status is not enough to trust a flow. The server response is
// still external data, so every response passes through the graph validator before
// it can reach Pinia or any editing component.
const requestFlow = async (url: string, init: RequestInit): Promise<FlowDto> => {
  try {
    const response = await fetch(url, init);
    if (!response.ok) {
      throw await httpError(response);
    }

    let payload: unknown;
    try {
      payload = await response.json();
    } catch {
      throw new FlowApiError('validation', 'The server returned malformed JSON.');
    }
    return parseFlowDto(payload);
  } catch (error) {
    if (error instanceof FlowApiError) throw error;
    if (error instanceof FlowDtoValidationError) {
      throw new FlowApiError('validation', `The server returned an invalid flow: ${error.message}`);
    }
    // Browsers report an aborted fetch as a DOMException rather than a normal
    // network failure. Keeping cancellation distinct prevents route changes from
    // showing a misleading "service unavailable" message.
    if (error instanceof DOMException && error.name === 'AbortError') {
      throw new FlowApiError('cancelled', 'The flow request was cancelled.');
    }
    throw new FlowApiError('network', 'Unable to reach the flow service.');
  }
};

export interface FlowListParameters {
  filter: string;
  statuses: Array<'draft' | 'deployed'>;
  page: number;
  pageSize: number;
  sort: 'ascending' | 'descending';
}

export interface FlowPage {
  items: FlowDto[];
  totalItems: number;
  page: number;
  pageSize: number;
  pageCount: number;
}

const positiveIntegerField = (payload: Record<string, unknown>, key: string): number => {
  const value = payload[key];
  if (!Number.isInteger(value) || (value as number) < 1) {
    throw new FlowApiError('validation', `The server returned an invalid ${key}.`);
  }
  return value as number;
};

const requestFlows = async (url: string, init: RequestInit): Promise<FlowPage> => {
  try {
    const response = await fetch(url, init);
    if (!response.ok) {
      throw await httpError(response);
    }
    let payload: unknown;
    try {
      payload = await response.json();
    } catch {
      throw new FlowApiError('validation', 'The server returned malformed JSON.');
    }
    if (typeof payload !== 'object' || payload === null || !('items' in payload)) {
      throw new FlowApiError('validation', 'The server returned an invalid flow list.');
    }
    const pagePayload = payload as Record<string, unknown>;
    if (!Array.isArray(pagePayload.items) || typeof pagePayload.totalItems !== 'number' || pagePayload.totalItems < 0) {
      throw new FlowApiError('validation', 'The server returned an invalid flow list.');
    }
    // Validate the whole list before the store replaces its current state. One bad
    // graph therefore cannot leave the library half updated.
    return {
      items: pagePayload.items.map(parseFlowDto),
      totalItems: pagePayload.totalItems,
      page: positiveIntegerField(pagePayload, 'page'),
      pageSize: positiveIntegerField(pagePayload, 'pageSize'),
      pageCount: positiveIntegerField(pagePayload, 'pageCount')
    };
  } catch (error) {
    if (error instanceof FlowApiError) throw error;
    if (error instanceof FlowDtoValidationError) {
      throw new FlowApiError('validation', `The server returned an invalid flow: ${error.message}`);
    }
    if (error instanceof DOMException && error.name === 'AbortError') {
      throw new FlowApiError('cancelled', 'The flow request was cancelled.');
    }
    throw new FlowApiError('network', 'Unable to reach the flow service.');
  }
};

const requestEmpty = async (url: string, init: RequestInit): Promise<void> => {
  // Delete responses intentionally have no JSON body, so parsing them like a flow
  // would turn a valid 204 No Content response into a validation error.
  try {
    const response = await fetch(url, init);
    if (!response.ok) {
      throw await httpError(response);
    }
  } catch (error) {
    if (error instanceof FlowApiError) throw error;
    if (error instanceof DOMException && error.name === 'AbortError') {
      throw new FlowApiError('cancelled', 'The flow request was cancelled.');
    }
    throw new FlowApiError('network', 'Unable to reach the flow service.');
  }
};

export interface FlowApiClient {
  listFlows(parameters: FlowListParameters, signal?: AbortSignal): Promise<FlowPage>;
  createFlow(name: string, signal?: AbortSignal): Promise<FlowDto>;
  getFlow(flowId: string, signal?: AbortSignal): Promise<FlowDto>;
  saveFlow(flow: FlowDto, signal?: AbortSignal): Promise<FlowDto>;
  deleteFlow(flowId: string, signal?: AbortSignal): Promise<void>;
}

export const flowApi: FlowApiClient = {
  listFlows: (parameters, signal) => {
    const query = new URLSearchParams({
      filter: parameters.filter,
      page: String(parameters.page),
      pageSize: String(parameters.pageSize),
      sort: parameters.sort
    });
    for (const status of parameters.statuses) query.append('status', status);
    return requestFlows(`/api/flows?${query}`, { method: 'GET', signal });
  },
  createFlow: (name, signal) =>
    requestFlow('/api/flows', {
      method: 'POST',
      headers: { 'content-type': 'application/json' },
      body: JSON.stringify({ name }),
      signal
    }),
  getFlow: (flowId, signal) =>
    requestFlow(`/api/flows/${encodeURIComponent(flowId)}`, { method: 'GET', signal }),
  saveFlow: (flow, signal) =>
    requestFlow(`/api/flows/${encodeURIComponent(flow.id)}`, {
      method: 'PUT',
      headers: { 'content-type': 'application/json' },
      body: JSON.stringify(flow),
      signal
    }),
  deleteFlow: (flowId, signal) =>
    requestEmpty(`/api/flows/${encodeURIComponent(flowId)}`, { method: 'DELETE', signal })
};
