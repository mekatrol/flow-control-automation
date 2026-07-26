// @vitest-environment jsdom

import { mount } from '@vue/test-utils';
import { describe, expect, it, vi } from 'vitest';

import AppDialog from '@/components/AppDialog.vue';

describe('AppDialog', () => {
  /**
   * Purpose: Protects the structural and accessibility contract of the reusable native dialog surface.
   * Description: Exercises rendering content and automation metadata without adding application-specific controls.
   */
  it('renders an accessible native dialog with automation metadata and slotted content', () => {
    const wrapper = mount(AppDialog, {
      props: {
        automation: 'credential-dialog',
        contentLabel: 'Credential details',
        id: 'credential-dialog'
      },
      slots: {
        default: '<p>Dialog content</p>'
      }
    });

    const dialog = wrapper.get('dialog');

    expect(dialog.attributes('id')).toBe('credential-dialog');
    expect(dialog.attributes('aria-label')).toBe('Credential details');
    expect(dialog.attributes('data-automation')).toBe('credential-dialog');
    expect(dialog.text()).toContain('Dialog content');
    expect(wrapper.find('button').exists()).toBe(false);
  });

  /**
   * Purpose: Protects the imperative API used by callers to open and close the native modal.
   * Description: Exercises the exposed methods and verifies that return values are passed to the platform dialog.
   */
  it('exposes methods that open and close the native dialog', () => {
    const wrapper = mount(AppDialog, {
      props: {
        contentLabel: 'Credential details',
        id: 'credential-dialog'
      }
    });
    const dialog = wrapper.get('dialog').element as HTMLDialogElement;
    const showModal = vi.fn<() => void>();
    const close = vi.fn<(returnValue?: string) => void>();
    dialog.showModal = showModal;
    dialog.close = close;

    wrapper.vm.showModal();
    wrapper.vm.close('saved');

    expect(showModal).toHaveBeenCalledOnce();
    expect(close).toHaveBeenCalledOnce();
    expect(close).toHaveBeenCalledWith('saved');
  });

  /**
   * Purpose: Protects the dismissal hooks needed for dirty-state confirmation and close handling.
   * Description: Exercises native cancel and close events and verifies callers receive the original events.
   */
  it('emits native cancel and close events', async () => {
    const wrapper = mount(AppDialog, {
      props: {
        contentLabel: 'Credential details',
        id: 'credential-dialog'
      }
    });
    const dialog = wrapper.get('dialog');
    const cancelEvent = new Event('cancel', { cancelable: true });
    const closeEvent = new Event('close');

    dialog.element.dispatchEvent(cancelEvent);
    dialog.element.dispatchEvent(closeEvent);

    expect(wrapper.emitted('cancel')).toEqual([[cancelEvent]]);
    expect(wrapper.emitted('close')).toEqual([[closeEvent]]);
  });
});
