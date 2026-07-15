import { ref, type Ref } from 'vue';

import type { Point } from '../geometry/connectorLayout';
import type { FlowConnectionEndpoint } from '../types';

export interface ConnectionEditing {
  connectionStart: Ref<FlowConnectionEndpoint | undefined>;
  previewEnd: Ref<Point | undefined>;
  connectionError: Ref<string | undefined>;
  beginConnection: (endpoint: FlowConnectionEndpoint, point: Point) => void;
  updatePreview: (point: Point) => void;
  reportConnectionError: (message: string) => void;
  cancelConnection: () => void;
}

export const useConnectionEditing = (): ConnectionEditing => {
  // An unfinished connection is interaction state, not part of the persisted
  // flow. Keeping it separate prevents a pointer gesture from making the flow
  // dirty until both endpoints pass validation.
  const connectionStart = ref<FlowConnectionEndpoint>();
  const previewEnd = ref<Point>();
  const connectionError = ref<string>();

  const beginConnection = (endpoint: FlowConnectionEndpoint, point: Point): void => {
    connectionStart.value = { ...endpoint };
    previewEnd.value = { ...point };
    connectionError.value = undefined;
  };

  const updatePreview = (point: Point): void => {
    if (connectionStart.value) previewEnd.value = { ...point };
  };

  const reportConnectionError = (message: string): void => {
    connectionError.value = message;
  };

  const cancelConnection = (): void => {
    // Cancellation clears the preview and any validation message together so a
    // later gesture cannot inherit feedback from the previous attempt.
    connectionStart.value = undefined;
    previewEnd.value = undefined;
    connectionError.value = undefined;
  };

  return {
    connectionStart,
    previewEnd,
    connectionError,
    beginConnection,
    updatePreview,
    reportConnectionError,
    cancelConnection
  };
};
