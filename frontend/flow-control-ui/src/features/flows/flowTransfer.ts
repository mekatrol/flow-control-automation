import { parseFlowDto, type FlowDto } from '@/features/flows/api/flowDto';

export const FLOW_EXPORT_VERSION = 1;

export interface FlowExport {
  formatVersion: typeof FLOW_EXPORT_VERSION;
  flow: FlowDto;
}

export const serializeFlowExport = (flow: FlowDto): string =>
  `${JSON.stringify({ formatVersion: FLOW_EXPORT_VERSION, flow } satisfies FlowExport, null, 2)}\n`;

export const parseFlowExport = (text: string): FlowDto => {
  let payload: unknown;
  try {
    payload = JSON.parse(text);
  } catch {
    throw new Error('The selected file is not valid JSON.');
  }

  if (typeof payload !== 'object' || payload === null || Array.isArray(payload)) {
    throw new Error('The selected file is not a flow export.');
  }

  const exported = payload as Record<string, unknown>;
  if (exported.formatVersion !== FLOW_EXPORT_VERSION) {
    throw new Error(`Unsupported flow export version “${String(exported.formatVersion)}”.`);
  }

  return parseFlowDto(exported.flow);
};

export const flowExportFilename = (name: string): string => {
  const safeName = name
    .trim()
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-|-$/g, '');
  return `${safeName || 'flow'}.flow.json`;
};
