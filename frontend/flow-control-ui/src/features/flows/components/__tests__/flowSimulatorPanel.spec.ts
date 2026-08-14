// @vitest-environment jsdom

import { mount } from '@vue/test-utils';
import { describe, expect, it } from 'vitest';
import AppFlowSimulatorPanel from '@/features/flows/components/AppFlowSimulatorPanel.vue';

describe('flow simulator panel', () => {
  it('exposes deterministic controls and announces lifecycle state', async () => {
    const wrapper = mount(AppFlowSimulatorPanel, {
      props: {
        automation: 'simulator',
        lifecycle: 'ready',
        session: {
          sessionId: 'one',
          flowId: 'flow-a',
          sourceRevision: 1,
          sourceDigest: 'abc',
          lifecycleState: 'ready',
          leaseRemainingMilliseconds: 900000,
          breakpoints: [],
          capabilities: {
            stepTick: true,
            stepNode: true,
            stepInstruction: true,
            continue: true,
            pause: true,
            runTo: true,
            maximumBreakpoints: 32,
            maximumInspectableSlots: 256
          }
        }
      }
    });

    await wrapper.get('[data-automation="simulator-step-tick"]').trigger('click');

    expect(wrapper.emitted('step-tick')).toHaveLength(1);
    expect(wrapper.get('[role="status"]').text()).toBe('Ready');
    expect(wrapper.text()).toContain('Physical equipment cannot be commanded');
  });

  it('requires recompilation after an edit and disables execution', () => {
    const wrapper = mount(AppFlowSimulatorPanel, {
      props: { automation: 'simulator', lifecycle: 'stale' }
    });

    expect(wrapper.get('[role="alert"]').text()).toContain('Start simulation again');
    expect(
      wrapper.get('[data-automation="simulator-step-tick"]').attributes('disabled')
    ).toBeDefined();
    expect(
      wrapper.get('[data-automation="simulator-start"]').attributes('disabled')
    ).toBeUndefined();
  });

  /**
   * Purpose: Protects the primary typed simulator workflow from regressing to Boolean point toggles.
   * Description: Renders a numeric interface terminal, changes its value and quality, and applies one coherent scan.
   */
  it('applies a typed interface input and exposes committed output metadata', async () => {
    const wrapper = mount(AppFlowSimulatorPanel, {
      props: {
        automation: 'simulator',
        lifecycle: 'ready',
        flowInterface: {
          schemaVersion: 1,
          inputs: [
            {
              id: 'temperature',
              name: 'Temperature',
              dataType: 'number',
              units: '°C',
              required: true
            }
          ],
          outputs: [{ id: 'result', name: 'Result', dataType: 'number', units: '°C' }]
        },
        session: {
          sessionId: 'one',
          flowId: 'flow-a',
          sourceRevision: 1,
          sourceDigest: 'abc',
          lifecycleState: 'ready',
          leaseRemainingMilliseconds: 900000,
          breakpoints: [],
          capabilities: {
            stepTick: true,
            stepNode: true,
            stepInstruction: true,
            continue: true,
            pause: true,
            runTo: true,
            maximumBreakpoints: 32,
            maximumInspectableSlots: 256
          },
          io: {
            emulatorId: 'io',
            flowId: 'flow-a',
            controllerTemplateId: 'server',
            lifecycleState: 'ready',
            virtualTimeMilliseconds: 0,
            scanNumber: 1,
            inputs: [
              {
                pointId: 'temperature',
                isInterface: true,
                typedValue: { type: 'number', boolean: false, number: 12.5, quality: 'good' }
              }
            ],
            outputHistory: [
              {
                scanNumber: 1,
                outputId: 'result',
                isInterface: true,
                proposedValue: { type: 'number', boolean: false, number: 12.5, quality: 'good' },
                effectiveValue: { type: 'number', boolean: false, number: 12.5, quality: 'good' },
                quality: 'good',
                units: '°C',
                lastChangeScan: 1
              }
            ]
          }
        }
      }
    });

    await wrapper.get('input[type="number"]').setValue('21.5');
    await wrapper.get('[data-automation="emulator-apply-step"]').trigger('click');

    // Expected outcome: One atomic typed request is emitted using the stable interface ID.
    // Acceptance criteria: The emitted value is numeric 21.5 for `temperature`, proving the workbench does not use a display label or Boolean coercion.
    expect(wrapper.emitted('apply-inputs-step')?.[0]?.[0]).toEqual([
      {
        inputId: 'temperature',
        typedValue: { type: 'number', boolean: false, number: 21.5, quality: 'good' }
      }
    ]);
    // Expected outcome: Output history clearly identifies committed simulator state and units.
    // Acceptance criteria: Visible text contains the committed label and °C, distinguishing it from a physical output.
    expect(wrapper.text()).toContain('committed simulator 12.5 °C');
  });
});
