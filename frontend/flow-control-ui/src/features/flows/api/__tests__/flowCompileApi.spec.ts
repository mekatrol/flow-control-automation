import { afterEach, describe, expect, it, vi } from 'vitest';
import { flowCompileApi } from '@/features/flows/api/flowCompileApi';
import type { ExecutableFlowSource } from '@/features/flows/api/flowDebugApi';

const source = {
  schemaVersion: 1,
  id: 'draft-flow',
  revision: 3,
  controllerTemplateId: 'server',
  controllerTemplateRevision: 1,
  execution: { mode: 'manual', intervalMs: 0, inputQualityPolicy: 'requireGood' },
  nodes: [],
  connections: [],
  virtualPointDeclarations: []
} as ExecutableFlowSource;

describe('flow compile API', () => {
  afterEach(() => vi.unstubAllGlobals());

  /**
   * Purpose: Protects compile success and structured diagnostic responses.
   * Description: Sends an unsaved source and accepts compiler errors as a normal compile result.
   */
  it('returns success metadata and compiler diagnostics', async () => {
    const fetchMock = vi
      .fn<typeof fetch>()
      .mockResolvedValueOnce(
        new Response(JSON.stringify({ success: true, flowRevision: 7, diagnostics: [] }))
      )
      .mockResolvedValueOnce(
        new Response(
          JSON.stringify({
            success: false,
            diagnostics: [
              {
                code: 'MissingInput',
                displayCode: 'FLOW001',
                path: '/nodes/2',
                title: 'Missing input',
                message: 'Input is required.'
              }
            ]
          }),
          { status: 422 }
        )
      );
    vi.stubGlobal('fetch', fetchMock);

    // Expected outcome: Successful and unsuccessful compilations both return typed results.
    // Acceptance criteria: Diagnostics retain their stable code and source path without throwing for HTTP 422.
    await expect(flowCompileApi.compile(source)).resolves.toMatchObject({
      success: true,
      flowRevision: 7
    });
    await expect(flowCompileApi.compile(source)).resolves.toMatchObject({
      success: false,
      diagnostics: [{ displayCode: 'FLOW001', path: '/nodes/2' }]
    });
    expect(fetchMock).toHaveBeenCalledWith(
      '/api/flows/draft-flow/compile',
      expect.objectContaining({ method: 'POST', body: JSON.stringify(source) })
    );
  });
});
