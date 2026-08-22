// @vitest-environment jsdom
import { mount } from '@vue/test-utils';
import { describe, expect, it } from 'vitest';
import AppFlowCompileResults from '@/features/flows/components/AppFlowCompileResults.vue';

describe('flow compile results', () => {
  /**
   * Purpose: Protects the error-list presentation and affected-node navigation.
   * Description: Renders a compiler diagnostic and selects the node identified by its JSON pointer.
   */
  it('renders errors and emits the affected node', async () => {
    const wrapper = mount(AppFlowCompileResults, {
      props: {
        automation: 'compile-results',
        nodeIds: ['first', 'affected'],
        result: {
          success: false,
          diagnostics: [
            {
              code: 'MissingInput',
              displayCode: 'FLOW001',
              path: '/nodes/1/configuration',
              title: 'Missing input',
              message: 'Input is required.'
            }
          ]
        }
      }
    });

    // Expected outcome: The table exposes the compiler detail and links its row to the graph node.
    // Acceptance criteria: The error code/message are visible and activating the row emits `affected`.
    expect(wrapper.text()).toContain('FLOW001');
    expect(wrapper.text()).toContain('Input is required.');
    await wrapper.get('tbody tr').trigger('click');
    expect(wrapper.emitted('selectDiagnostic')).toEqual([['affected']]);
  });
});
