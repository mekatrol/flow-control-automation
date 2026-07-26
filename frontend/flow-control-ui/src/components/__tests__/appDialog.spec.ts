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

    // Expected outcome: The reusable dialog keeps the caller-provided DOM identity.
    // Acceptance criteria: The native dialog ID is `credential-dialog` because labels,
    // automation hooks, and imperative callers must target the same dialog instance.
    expect(dialog.attributes('id')).toBe('credential-dialog');

    // Expected outcome: The native dialog exposes its caller-provided accessible name.
    // Acceptance criteria: `aria-label` is "Credential details" because the modal has
    // no required built-in heading and still needs an announced purpose.
    expect(dialog.attributes('aria-label')).toBe('Credential details');

    // Expected outcome: The dialog exposes stable automation metadata when requested.
    // Acceptance criteria: `data-automation` is `credential-dialog` because consumers
    // need the supplied component identity rather than a generated selector.
    expect(dialog.attributes('data-automation')).toBe('credential-dialog');

    // Expected outcome: Caller content is rendered inside the native dialog surface.
    // Acceptance criteria: The dialog contains "Dialog content" because AppDialog must
    // present its default slot rather than impose workflow-specific content.
    expect(dialog.text()).toContain('Dialog content');

    // Expected outcome: The base dialog does not invent a dismissal control.
    // Acceptance criteria: No button is rendered because explicit workflow actions
    // belong to callers and some guarded dialogs must not be freely dismissible.
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

    // Expected outcome: The exposed open operation delegates to the native modal API.
    // Acceptance criteria: `showModal` is called once because one imperative open
    // request must create one modal presentation.
    expect(showModal).toHaveBeenCalledOnce();

    // Expected outcome: The exposed close operation delegates once to the native dialog.
    // Acceptance criteria: `close` is called once because one completed workflow must
    // dismiss the dialog without duplicate native close events.
    expect(close).toHaveBeenCalledOnce();

    // Expected outcome: The close result is preserved for native dialog consumers.
    // Acceptance criteria: `close` receives "saved" because callers use that return
    // value to distinguish the operation that completed the modal.
    expect(close).toHaveBeenCalledWith('saved');
  });

  /**
   * Purpose: Protects the dismissal hooks needed for dirty-state confirmation and close handling.
   * Description: Exercises native cancel and close events and verifies callers receive the original events.
   */
  it('emits native cancel and close events', () => {
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

    // Expected outcome: Callers receive the original cancellable platform event.
    // Acceptance criteria: The `cancel` emission contains the arranged `cancelEvent`
    // because callers must be able to prevent that exact native dismissal attempt.
    expect(wrapper.emitted('cancel')).toEqual([[cancelEvent]]);

    // Expected outcome: Callers receive the original native close event.
    // Acceptance criteria: The `close` emission contains the arranged `closeEvent`
    // because workflow cleanup must correspond to the platform event that ended it.
    expect(wrapper.emitted('close')).toEqual([[closeEvent]]);
  });

  /**
   * Purpose: Protects dialogs whose workflows require an explicit action before closing.
   * Description: Exercises a non-dismissible dialog and verifies its native cancel event is prevented.
   */
  it('prevents native cancellation when dismissal is disabled', () => {
    const wrapper = mount(AppDialog, {
      props: {
        contentLabel: 'Credential details',
        dismissible: false,
        id: 'credential-dialog'
      }
    });
    const cancelEvent = new Event('cancel', { cancelable: true });

    wrapper.get('dialog').element.dispatchEvent(cancelEvent);

    // Expected outcome: A non-dismissible dialog blocks Escape-driven cancellation.
    // Acceptance criteria: The native cancel event is default-prevented because an
    // explicit workflow decision is required before this dialog may close.
    expect(cancelEvent.defaultPrevented).toBe(true);

    // Expected outcome: Blocking native dismissal still notifies the caller.
    // Acceptance criteria: The `cancel` emission contains the prevented event because
    // callers may need to present their own discard confirmation in response.
    expect(wrapper.emitted('cancel')).toEqual([[cancelEvent]]);
  });
});
