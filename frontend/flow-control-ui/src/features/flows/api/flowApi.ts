import { FlowDtoValidationError, parseFlowDto, type FlowDto } from './flowDto';
import { waitForFetch } from '@/api/waitForFetch';

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
    const response = await waitForFetch(url, init);
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

export interface FlowIlImportResult {
  flow: FlowDto;
  recoveryLevel: 'lossless' | 'normalized';
  warnings: string[];
  provenance: {
    artifactVersion: number;
    artifactSha256: string;
    flowRevision: number;
    controllerTemplateId: string;
    controllerTemplateRevision: number;
  };
  saved: boolean;
}

const parseFlowIlImport = (payload: unknown): FlowIlImportResult => {
  if (typeof payload !== 'object' || payload === null) {
    throw new FlowApiError('validation', 'The server returned an invalid Flow IL import result.');
  }
  const value = payload as Record<string, unknown>;
  if (
    (value.recoveryLevel !== 'lossless' && value.recoveryLevel !== 'normalized') ||
    !Array.isArray(value.warnings) ||
    !value.warnings.every((warning) => typeof warning === 'string') ||
    typeof value.saved !== 'boolean' ||
    typeof value.provenance !== 'object' ||
    value.provenance === null
  ) {
    throw new FlowApiError('validation', 'The server returned an invalid Flow IL import result.');
  }
  return {
    flow: parseFlowDto(value.flow),
    recoveryLevel: value.recoveryLevel,
    warnings: value.warnings,
    provenance: value.provenance as FlowIlImportResult['provenance'],
    saved: value.saved
  };
};

const positiveIntegerField = (payload: Record<string, unknown>, key: string): number => {
  const value = payload[key];
  if (!Number.isInteger(value) || (value as number) < 1) {
    throw new FlowApiError('validation', `The server returned an invalid ${key}.`);
  }
  return value as number;
};

const requestFlows = async (url: string, init: RequestInit): Promise<FlowPage> => {
  try {
    const response = await waitForFetch(url, init);
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
    if (
      !Array.isArray(pagePayload.items) ||
      typeof pagePayload.totalItems !== 'number' ||
      pagePayload.totalItems < 0
    ) {
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
    const response = await waitForFetch(url, init);
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
  getDeployedFlow(flowId: string, signal?: AbortSignal): Promise<FlowDto>;
  saveFlow(flow: FlowDto, signal?: AbortSignal): Promise<FlowDto>;
  revertToDeployed(flowId: string, signal?: AbortSignal): Promise<FlowDto>;
  setFlowDisabled(flowId: string, disabled: boolean, signal?: AbortSignal): Promise<FlowDto>;
  deleteFlow(flowId: string, signal?: AbortSignal): Promise<void>;
  importFlowIl(
    artifactBase64: string,
    name: string | undefined,
    save: boolean,
    signal?: AbortSignal
  ): Promise<FlowIlImportResult>;
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
  getDeployedFlow: (flowId, signal) =>
    requestFlow(`/api/flows/${encodeURIComponent(flowId)}/deployed`, { method: 'GET', signal }),
  saveFlow: (flow, signal) =>
    requestFlow(`/api/flows/${encodeURIComponent(flow.id)}`, {
      method: 'PUT',
      headers: { 'content-type': 'application/json' },
      body: JSON.stringify(flow),
      signal
    }),
  revertToDeployed: (flowId, signal) =>
    requestFlow(`/api/flows/${encodeURIComponent(flowId)}/revert-to-deployed`, {
      method: 'POST',
      signal
    }),
  setFlowDisabled: (flowId, disabled, signal) =>
    requestFlow(`/api/flows/${encodeURIComponent(flowId)}/${disabled ? 'disable' : 'enable'}`, {
      method: 'POST',
      signal
    }),
  deleteFlow: (flowId, signal) =>
    requestEmpty(`/api/flows/${encodeURIComponent(flowId)}`, { method: 'DELETE', signal }),
  importFlowIl: async (artifactBase64, name, save, signal) => {
    try {
      const response = await waitForFetch('/api/flows/import-il', {
        method: 'POST',
        headers: { 'content-type': 'application/json' },
        body: JSON.stringify({ artifactBase64, name, save }),
        signal
      });
      if (!response.ok) throw await httpError(response);
      return parseFlowIlImport(await response.json());
    } catch (error) {
      if (error instanceof FlowApiError) throw error;
      if (error instanceof FlowDtoValidationError) {
        throw new FlowApiError('validation', `The recovered flow is invalid: ${error.message}`);
      }
      if (error instanceof DOMException && error.name === 'AbortError') {
        throw new FlowApiError('cancelled', 'The Flow IL import was cancelled.');
      }
      throw new FlowApiError('network', 'Unable to import the Flow IL artifact.');
    }
  }
};
