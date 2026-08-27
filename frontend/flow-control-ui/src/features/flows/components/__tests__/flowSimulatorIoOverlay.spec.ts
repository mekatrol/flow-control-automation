// @vitest-environment jsdom

import { mount } from '@vue/test-utils';
import { describe, expect, it } from 'vitest';
import AppFlowSimulatorIoOverlay from '@/features/flows/components/AppFlowSimulatorIoOverlay.vue';
import { createDefaultNode } from '@/features/flows/graph/createNode';
import type { FlowDefinition } from '@/features/flows/types';

describe('flow simulator I/O overlay', () => {
  it('edits virtual inputs inside the flow workspace and shows virtual outputs', async () => {
    const input = createDefaultNode('analogInput', { x: 0, y: 0 }, 0);
    input.configuration.pointId = 'virtual-input';
    const output = createDefaultNode('analogOutput', { x: 200, y: 0 }, 1);
    output.configuration.pointId = 'virtual-output';
    const flow: FlowDefinition = {
      id: 'flow-a',
      name: 'Flow',
      description: '',
      status: 'draft',
      disabled: false,
      updatedAt: '2026-01-01T00:00:00Z',
      nodes: [input, output],
      connections: [],
      virtualPointDeclarations: [
        {
          key: 'virtual-input',
          valueType: 'analog',
          units: '°C',
          readable: true,
          commandable: false,
          persistence: 'volatile'
        },
        {
          key: 'virtual-output',
          valueType: 'analog',
          units: '%',
          readable: false,
          commandable: true,
          persistence: 'volatile'
        }
      ]
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
              typedValue: { type: 'number', boolean: false, number: 10, quality: 'good' }
            }
          ],
          outputHistory: [
            {
              scanNumber: 1,
              outputId: 'virtual-output',
              proposedValue: { type: 'number', boolean: false, number: 55, quality: 'good' },
              effectiveValue: { type: 'number', boolean: false, number: 55, quality: 'good' },
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
    await wrapper.get('input[type="number"]').setValue('21.5');
    await wrapper.setProps({
      snapshot: {
        ...wrapper.props('snapshot'),
        scanNumber: 2,
        inputs: [
          {
            pointId: 'virtual-input',
            typedValue: { type: 'number', boolean: false, number: 10, quality: 'good' }
          }
        ]
      }
    });
    expect((wrapper.get('input[type="number"]').element as HTMLInputElement).value).toBe('21.5');
    await wrapper.get('button').trigger('click');
    expect(wrapper.emitted('apply')?.[0]?.[0]).toEqual([
      {
        inputId: 'virtual-input',
        typedValue: { type: 'number', boolean: false, number: 21.5, quality: 'good' }
      }
    ]);
  });
});
