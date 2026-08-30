// @vitest-environment jsdom

import { mount } from '@vue/test-utils';
import { describe, expect, it } from 'vitest';
import AppFlowSimulatorIoOverlay from '@/features/flows/components/AppFlowSimulatorIoOverlay.vue';
import { createDefaultNode } from '@/features/flows/graph/createNode';
import type { FlowDefinition } from '@/features/flows/types';

describe('flow simulator I/O overlay', () => {
  it('edits virtual inputs inside the flow workspace and shows virtual outputs', async () => {
    const input = createDefaultNode('analogVirtual', { x: 0, y: 0 }, 0);
    input.configuration.pointId = 'virtual-input';
    input.configuration.units = '°C';
    const output = createDefaultNode('analogVirtual', { x: 200, y: 0 }, 1);
    output.configuration.pointId = 'virtual-output';
    output.configuration.units = '%';
    const flow: FlowDefinition = {
      id: 'flow-a',
      name: 'Flow',
      description: '',
      status: 'draft',
      disabled: false,
      updatedAt: '2026-01-01T00:00:00Z',
      nodes: [input, output],
      connections: []
    };
    const wrapper = mount(AppFlowSimulatorIoOverlay, {
      props: {
        flow,
        snapshot: {
          emulatorId: 'one',
          flowId: flow.id,
          controllerTemplateId: 'server',
          lifecycleState: 'ready',
          virtualTimeMilliseconds: 0,
          scanNumber: 1,
          inputs: [
            {
              pointId: 'virtual-input',
              typedValue: {
                dataType: 'number',
                boolean: false,
                number: 10,
                quality: 'good'
              }
            }
          ],
          outputHistory: [
            {
              scanNumber: 1,
              outputId: 'virtual-output',
              proposedValue: {
                dataType: 'number',
                boolean: false,
                number: 55,
                quality: 'good'
              },
              effectiveValue: {
                dataType: 'number',
                boolean: false,
                number: 55,
                quality: 'good'
              },
              quality: 'good',
              units: '%',
              lastChangeScan: 1
            }
          ]
        }
      }
    });

    expect(wrapper.text()).toContain('virtual-output');
    expect(wrapper.text()).toContain('55 %');
    await wrapper.get('input[type="text"]').setValue('21.5');
    await wrapper.setProps({
      snapshot: {
        ...wrapper.props('snapshot')!,
        scanNumber: 2,
        inputs: [
          {
            pointId: 'virtual-input',
            typedValue: {
              dataType: 'number',
              boolean: false,
              number: 10,
              quality: 'good'
            }
          }
        ]
      }
    });
    expect((wrapper.get('input[type="text"]').element as HTMLInputElement).value).toBe('21.5');
    await wrapper.get('button').trigger('click');
    expect(wrapper.emitted('apply')?.[0]?.[0]).toEqual([
      {
        inputId: 'virtual-input',
        typedValue: { dataType: 'number', boolean: false, number: 21.5, quality: 'good' }
      }
    ]);
  });
});
