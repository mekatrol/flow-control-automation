import { describe, expect, it } from 'vitest';

import { sampleFlows } from '@/features/flows/__tests__/fixtures/sampleFlows';
import {
  flowExportFilename,
  parseFlowExport,
  serializeFlowExport
} from '@/features/flows/flowTransfer';

describe('flow JSON transfer', () => {
  it('round trips a validated flow in a versioned document', () => {
    const flow = sampleFlows[0]!;
    expect(parseFlowExport(serializeFlowExport(flow))).toEqual(flow);
    expect(JSON.parse(serializeFlowExport(flow))).toMatchObject({ formatVersion: 1 });
  });

  it('rejects malformed, unsupported, and structurally invalid documents', () => {
    expect(() => parseFlowExport('{')).toThrow('not valid JSON');
    expect(() =>
      parseFlowExport(JSON.stringify({ formatVersion: 2, flow: sampleFlows[0] }))
    ).toThrow('Unsupported flow export version');
    expect(() =>
      parseFlowExport(JSON.stringify({ formatVersion: 1, flow: { name: 'Bad' } }))
    ).toThrow('flow.nodes');
  });

  it('creates portable filenames', () => {
    expect(flowExportFilename('  Climate / Control  ')).toBe('climate-control.flow.json');
    expect(flowExportFilename('***')).toBe('flow.flow.json');
  });
});
