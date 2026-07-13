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

// A successful HTTP status is not enough to trust a flow. The server response is
// still external data, so every response passes through the graph validator before
// it can reach Pinia or any editing component.
const requestFlow = async (url: string, init: RequestInit): Promise<FlowDto> => {
  try {
    const response = await fetch(url, init);
    if (!response.ok) {
      throw new FlowApiError(
        'http',
        `Flow request failed with status ${response.status}.`,
        response.status
      );
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

const requestFlows = async (url: string, init: RequestInit): Promise<FlowDto[]> => {
  try {
    const response = await fetch(url, init);
    if (!response.ok) {
      throw new FlowApiError(
        'http',
        `Flow request failed with status ${response.status}.`,
        response.status
      );
    }
    let payload: unknown;
    try {
      payload = await response.json();
    } catch {
      throw new FlowApiError('validation', 'The server returned malformed JSON.');
    }
    if (!Array.isArray(payload)) {
      throw new FlowApiError('validation', 'The server returned an invalid flow list.');
    }
    // Validate the whole list before the store replaces its current state. One bad
    // graph therefore cannot leave the library half updated.
    return payload.map(parseFlowDto);
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
      throw new FlowApiError(
        'http',
        `Flow request failed with status ${response.status}.`,
        response.status
      );
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
  listFlows(signal?: AbortSignal): Promise<FlowDto[]>;
  createFlow(name: string, signal?: AbortSignal): Promise<FlowDto>;
  getFlow(flowId: string, signal?: AbortSignal): Promise<FlowDto>;
  saveFlow(flow: FlowDto, signal?: AbortSignal): Promise<FlowDto>;
  deleteFlow(flowId: string, signal?: AbortSignal): Promise<void>;
}

export const flowApi: FlowApiClient = {
  listFlows: (signal) => requestFlows('/api/flows', { method: 'GET', signal }),
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
